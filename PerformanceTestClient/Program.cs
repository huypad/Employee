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
    /// <summary>
    /// CV3: Dùng System.Diagnostics.Stopwatch để đo thời gian Encrypt/Decrypt
    /// khi gọi vào endpoint thật của CV2. Chạy TUẦN TỰ, KHÔNG mô phỏng tải đồng thời
    /// (việc giả lập tải 50/100/200 request là của CV4, dùng K6, đã làm riêng ở đó).
    /// </summary>
    public static class Program
    {
        private const string Host = "https://localhost:1404";
        private static readonly string LogFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        private static readonly string LogFilePath = Path.Combine(LogFolder, "performance_log.csv");
        private static readonly Random Rng = new Random();

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

            var encryptTimes = new List<double>();
            var decryptTimes = new List<double>();

            for (int i = 1; i <= soLanGoi; i++)
            {
                var (fieldName, value) = RandomField();
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
                var swEncrypt = Stopwatch.StartNew(); // ← System.Diagnostics.Stopwatch bắt đầu
                var encryptRes = await httpClient.PostAsJsonAsync($"/api/encryptiontest/{algorithm}/field/encrypt", new { fieldName, value });
                swEncrypt.Stop(); // ← dừng lại ngay khi có phản hồi

                LogResult(algorithm.ToUpper(), "Encrypt", Encoding.UTF8.GetByteCount(value), swEncrypt.Elapsed.TotalMilliseconds, encryptRes.IsSuccessStatusCode);
                encryptTimes.Add(swEncrypt.Elapsed.TotalMilliseconds);

                if (!encryptRes.IsSuccessStatusCode)
                {
                    Console.WriteLine("Encrypt lỗi, bỏ qua Decrypt");
                    continue;
                }

                var body = await encryptRes.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                string encryptedValue = doc.RootElement.GetProperty("data").GetProperty("OutputValue").GetString();

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

        private static (string fieldName, string value) RandomField()
        {
            var fields = new[] { "SDT", "CCCD", "HoTen", "DiaChi" };
            var fieldName = fields[Rng.Next(fields.Length)];

            string value = fieldName switch
            {
                "SDT" => "09" + Rng.Next(10000000, 99999999),
                "CCCD" => Rng.NextInt64(100000000000, 899999999999).ToString(),
                "HoTen" => "Nguyen Van " + (char)('A' + Rng.Next(26)),
                "DiaChi" => $"{Rng.Next(1, 999)} Le Loi, Q.1, TP.HCM",
                _ => "test"
            };

            return (fieldName, value);
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

        private static void PrintSummary(string operation, List<double> times)
        {
            if (times.Count == 0)
            {
                Console.WriteLine($"[{operation}] Không có dữ liệu.");
                return;
            }

            var sorted = times.OrderBy(x => x).ToArray();
            Console.WriteLine($"\n=== {operation} - Thống kê ({sorted.Length} lần) ===");
            Console.WriteLine($"Trung bình : {sorted.Average():F2} ms");
            Console.WriteLine($"Nhỏ nhất   : {sorted.First():F2} ms");
            Console.WriteLine($"Lớn nhất   : {sorted.Last():F2} ms");
        }
    }
}