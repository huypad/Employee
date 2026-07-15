using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PerformanceTestClient
{

    public static class Program
    {
        private const string Host = "https://localhost:1404";
        private static readonly string LogFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        private static readonly string LogFilePath = Path.Combine(LogFolder, "performance_log.csv");

        public static async Task Main(string[] args)
        {
            // Cách chạy: dotnet run -- <algorithm> <soLanGoi>
            // Ví dụ:     dotnet run -- aes 20   => gọi AES 20 lần liên tiếp, đo từng lần
            string algorithm = args.Length > 0 ? args[0].ToLower() : "aes"; // plaintext / aes / rsa / fpe
            int soLanGoi = args.Length > 1 ? int.Parse(args[1]) : 20;

            Console.WriteLine($"=== Đo thời gian thực thi: Algorithm={algorithm.ToUpper()}, Số lần gọi={soLanGoi} ===\n");

            EnsureLogFileExists();

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
            };
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(Host) };
            
            Console.WriteLine("Đang warm-up (không tính vào kết quả)...");
            try
            {
                string warmupField = "CMND";
                string warmupValue = "000000000000";

                if (algorithm == "plaintext")
                    await httpClient.PostAsJsonAsync("/api/encryptiontest/plaintext/field", new { fieldName = warmupField, value = warmupValue });
                else if (algorithm == "hash")
                    await httpClient.PostAsJsonAsync("/api/encryptiontest/hmacsha256/field/hash", new { fieldName = warmupField, value = warmupValue });
                else
                    await httpClient.PostAsJsonAsync($"/api/encryptiontest/{algorithm}/field/encrypt", new { fieldName = warmupField, value = warmupValue });
            }
            catch { /* bỏ qua lỗi warm-up nếu có */ }
            Console.WriteLine("Warm-up xong, bắt đầu đo thật:\n");


            var encryptTimes = new List<double>();
            var decryptTimes = new List<double>();

            for (int i = 1; i <= soLanGoi; i++)
            {
                var (fieldName, value) = GetRecord(i);
                Console.Write($"Lần {i,3}/{soLanGoi} - field={fieldName,-8} ... ");

                if (algorithm == "plaintext")
                {
                    var sw = Stopwatch.StartNew();
                    var res = await httpClient.PostAsJsonAsync("/api/encryptiontest/plaintext/field", new { fieldName, value });
                    sw.Stop();

                    LogResult("PLAINTEXT", "None", Encoding.UTF8.GetByteCount(value), sw.Elapsed.TotalMilliseconds, res.IsSuccessStatusCode);
                    encryptTimes.Add(sw.Elapsed.TotalMilliseconds);
                    Console.WriteLine($"{sw.Elapsed.TotalMilliseconds:F2} ms");
                    continue;
                }

                //  HASH (HMAC-SHA256): chỉ 1 chiều, không có Decrypt vì hash không đảo ngược được 
                if (algorithm == "hash")
                {
                    var swHash = Stopwatch.StartNew(); // ← System.Diagnostics.Stopwatch
                    var hashRes = await httpClient.PostAsJsonAsync("/api/encryptiontest/hmacsha256/field/hash", new { fieldName, value });
                    swHash.Stop();

                    LogResult("HMAC-SHA256", "Hash", Encoding.UTF8.GetByteCount(value), swHash.Elapsed.TotalMilliseconds, hashRes.IsSuccessStatusCode);
                    encryptTimes.Add(swHash.Elapsed.TotalMilliseconds); // dùng chung danh sách encryptTimes để tính thống kê
                    Console.WriteLine($"{swHash.Elapsed.TotalMilliseconds:F2} ms");
                    continue;
                }

                //  ENCRYPT 
                var swEncrypt = Stopwatch.StartNew(); // System.Diagnostics.Stopwatch bắt đầu
                var encryptRes = await httpClient.PostAsJsonAsync($"/api/encryptiontest/{algorithm}/field/encrypt", new { fieldName, value });
                swEncrypt.Stop(); // ← dừng lại ngay khi có phản hồi

                LogResult(algorithm.ToUpper(), "Encrypt", Encoding.UTF8.GetByteCount(value), swEncrypt.Elapsed.TotalMilliseconds, encryptRes.IsSuccessStatusCode);
                encryptTimes.Add(swEncrypt.Elapsed.TotalMilliseconds);

                if (!encryptRes.IsSuccessStatusCode)
                {
                    Console.WriteLine("Encrypt lỗi, bỏ qua Decrypt");
                    continue;
                }

                // var body = await encryptRes.Content.ReadAsStringAsync();
                // using var doc = JsonDocument.Parse(body);
                // string encryptedValue = doc.RootElement.GetProperty("data").GetProperty("OutputValue").GetString();
                var body = await encryptRes.Content.ReadAsStringAsync();
                string? encryptedValue = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                        dataEl.TryGetProperty("OutputValue", out var outEl))
                    {
                        encryptedValue = outEl.GetString();
                    }
                }
                catch (JsonException)
                {
                    // body không phải JSON hợp lệ, encryptedValue giữ nguyên null
                }

                if (encryptedValue == null)
                {
                    Console.WriteLine($"Encrypt trả về không đúng định dạng, bỏ qua Decrypt. Response: {body}");
                    continue;
                }

                //  DECRYPT 
                var swDecrypt = Stopwatch.StartNew(); // ← System.Diagnostics.Stopwatch bắt đầu
                var decryptRes = await httpClient.PostAsJsonAsync($"/api/encryptiontest/{algorithm}/field/decrypt", new { fieldName, value = encryptedValue });
                swDecrypt.Stop(); // ← dừng lại

                LogResult(algorithm.ToUpper(), "Decrypt", Encoding.UTF8.GetByteCount(encryptedValue ?? ""), swDecrypt.Elapsed.TotalMilliseconds, decryptRes.IsSuccessStatusCode);
                decryptTimes.Add(swDecrypt.Elapsed.TotalMilliseconds);

                Console.WriteLine($"Encrypt={swEncrypt.Elapsed.TotalMilliseconds:F2}ms  Decrypt={swDecrypt.Elapsed.TotalMilliseconds:F2}ms");
            }

            string tenThaoTac = algorithm == "hash" ? "Hash" : "Encrypt";
            PrintSummary(tenThaoTac, encryptTimes);
            if (algorithm != "plaintext" && algorithm != "hash") PrintSummary("Decrypt", decryptTimes);

            Console.WriteLine($"\nLog chi tiết tại: {LogFilePath}");
        }

        // Đọc bộ dữ liệu giả lập cố định 200 dòng (dùng chung với K6),
        // để tái lập được kết quả, so sánh công bằng giữa các lần chạy.
        // File test-data-200.json cần đặt cùng thư mục với Program.cs
        // (hoặc chỉ đường dẫn khác trong DataFilePath bên dưới).
        private static readonly string DataFilePath = Path.Combine(Directory.GetCurrentDirectory(), "test-data-200.json");
        private static List<(string fieldName, string value)>? _testData;

        private static (string fieldName, string value) GetRecord(int lanThu)
        {
            if (_testData == null)
            {
                var json = File.ReadAllText(DataFilePath);
                using var doc = JsonDocument.Parse(json);
                _testData = doc.RootElement.EnumerateArray()
                    .Select(e => (e.GetProperty("fieldName").GetString()!, e.GetProperty("value").GetString()!))
                    .ToList();
            }

            // Lấy tuần tự, xoay vòng nếu số lần gọi > 200 - đảm bảo tái lập được kết quả
            int idx = (lanThu - 1) % _testData.Count;
            return _testData[idx];
        }

        private static void EnsureLogFileExists()
        {
            if (!Directory.Exists(LogFolder)) Directory.CreateDirectory(LogFolder);

            if (!File.Exists(LogFilePath))
            {
                var header = "Timestamp,Algorithm,Operation,DataSizeBytes,ExecutionTimeMs,Success" + Environment.NewLine;
                File.WriteAllText(LogFilePath, header, Encoding.UTF8);
            }
        }

        private static void LogResult(string algorithm, string operation, long dataSizeBytes, double executionTimeMs, bool success)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"{timestamp},{algorithm},{operation},{dataSizeBytes},{executionTimeMs:F3},{success}" + Environment.NewLine;
            File.AppendAllText(LogFilePath, line, Encoding.UTF8);
        }

        // private static void PrintSummary(string operation, List<double> times)
        // {
        //     if (times.Count == 0)
        //     {
        //         Console.WriteLine($"[{operation}] Không có dữ liệu.");
        //         return;
        //     }

        //     var sorted = times.OrderBy(x => x).ToArray();
        //     Console.WriteLine($"\n=== {operation} - Thống kê ({sorted.Length} lần) ===");
        //     Console.WriteLine($"Trung bình : {sorted.Average():F2} ms");
        //     Console.WriteLine($"Nhỏ nhất   : {sorted.First():F2} ms");
        //     Console.WriteLine($"Lớn nhất   : {sorted.Last():F2} ms");
        // }
        private static void PrintSummary(string operation, List<double> times)
        {
            if (times.Count == 0)
            {
                Console.WriteLine($"[{operation}] Không có dữ liệu.");
                return;
            }

            var sorted = times.OrderBy(x => x).ToArray();
            double median = sorted.Length % 2 == 0
                ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2.0
                : sorted[sorted.Length / 2];

            Console.WriteLine($"\n=== {operation} - Thống kê ({sorted.Length} lần) ===");
            Console.WriteLine($"Trung bình : {sorted.Average():F2} ms");
            Console.WriteLine($"Trung vị   : {median:F2} ms   (ít bị lệch bởi outlier hơn Trung bình)");
            Console.WriteLine($"Nhỏ nhất   : {sorted.First():F2} ms");
            Console.WriteLine($"Lớn nhất   : {sorted.Last():F2} ms");
        }
    }
}