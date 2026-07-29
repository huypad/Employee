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
using Serilog;

namespace PerformanceTestClient
{
    /// <summary>
    /// Dùng System.Diagnostics.Stopwatch để đo thời gian Encrypt/Decrypt.
    /// Ghi lại SONG SONG 2 con số cho mỗi lần gọi:
    ///   - ClientRoundTripMs: đo từ phía client (Stopwatch bọc quanh HTTP call) -
    ///     bao gồm cả network, serialize JSON, overhead ASP.NET Core.
    ///   - ServerExecutionMs: server tự đo (Stopwatch bọc SÁT quanh đúng dòng gọi
    ///     thuật toán trong ProcessField() bên EncryptionTestController.cs), trả về
    ///     kèm trong response - đây là thời gian THUẦN của riêng thuật toán,
    ///     không lẫn network/overhead.
    ///
    /// Ghi log: dùng Serilog + Async sink (hàng đợi trong bộ nhớ, ghi đĩa ở luồng nền).
    /// </summary>
    public static class Program
    {
        private const string Host = "https://localhost:1404";
        private static readonly string LogFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        private static readonly string LogFilePath = Path.Combine(LogFolder, "performance_log.csv");

        public static async Task Main(string[] args)
        {
            string algorithm = args.Length > 0 ? args[0].ToLower() : "aes"; // plaintext / aes / rsa / fpe / hash
            int soLanGoi = args.Length > 1 ? int.Parse(args[1]) : 20;

            Console.WriteLine($"=== Đo thời gian thực thi: Algorithm={algorithm.ToUpper()}, Số lần gọi={soLanGoi} ===\n");

            EnsureLogFileExists();
            ConfigureSerilog();

            try
            {
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

                var encryptClientTimes = new List<double>();
                var encryptServerTimes = new List<double>();
                var decryptClientTimes = new List<double>();
                var decryptServerTimes = new List<double>();

                for (int i = 1; i <= soLanGoi; i++)
                {
                    var (fieldName, value) = GetRecord(i);
                    Console.Write($"Lần {i,3}/{soLanGoi} - field={fieldName,-8} ... ");

                    if (algorithm == "plaintext")
                    {
                        var sw = Stopwatch.StartNew();
                        var res = await httpClient.PostAsJsonAsync("/api/encryptiontest/plaintext/field", new { fieldName, value });
                        sw.Stop();

                        double serverMs = await ReadServerExecutionTimeMs(res);
                        LogResult("PLAINTEXT", "None", Encoding.UTF8.GetByteCount(value), sw.Elapsed.TotalMilliseconds, serverMs, res.IsSuccessStatusCode);
                        encryptClientTimes.Add(sw.Elapsed.TotalMilliseconds);
                        encryptServerTimes.Add(serverMs);
                        Console.WriteLine($"Client={sw.Elapsed.TotalMilliseconds:F2}ms  Server={serverMs:F3}ms");
                        continue;
                    }

                    //  HASH (HMAC-SHA256): chỉ 1 chiều, không có Decrypt 
                    if (algorithm == "hash")
                    {
                        var swHash = Stopwatch.StartNew();
                        var hashRes = await httpClient.PostAsJsonAsync("/api/encryptiontest/hmacsha256/field/hash", new { fieldName, value });
                        swHash.Stop();

                        double serverMs = await ReadServerExecutionTimeMs(hashRes);
                        LogResult("HMAC-SHA256", "Hash", Encoding.UTF8.GetByteCount(value), swHash.Elapsed.TotalMilliseconds, serverMs, hashRes.IsSuccessStatusCode);
                        encryptClientTimes.Add(swHash.Elapsed.TotalMilliseconds);
                        encryptServerTimes.Add(serverMs);
                        Console.WriteLine($"Client={swHash.Elapsed.TotalMilliseconds:F2}ms  Server={serverMs:F3}ms");
                        continue;
                    }
                    //  HASH INDEX (tìm kiếm gần đúng): 1 chiều, có chuẩn hóa chuỗi trước khi hash 
                    if (algorithm == "hashindex")
                    {
                        var swHashIndex = Stopwatch.StartNew();
                        var hashIndexRes = await httpClient.PostAsJsonAsync("/api/encryptiontest/hmacsha256/field/index", new { fieldName, value });
                        swHashIndex.Stop();

                        double serverMs = await ReadServerExecutionTimeMs(hashIndexRes);
                        LogResult("HMAC-SHA256", "IndexHash", Encoding.UTF8.GetByteCount(value), swHashIndex.Elapsed.TotalMilliseconds, serverMs, hashIndexRes.IsSuccessStatusCode);
                        encryptClientTimes.Add(swHashIndex.Elapsed.TotalMilliseconds);
                        encryptServerTimes.Add(serverMs);
                        Console.WriteLine($"Client={swHashIndex.Elapsed.TotalMilliseconds:F2}ms  Server={serverMs:F3}ms");
                        continue;
                    }

                    //  ENCRYPT 
                    var swEncrypt = Stopwatch.StartNew();
                    var encryptRes = await httpClient.PostAsJsonAsync($"/api/encryptiontest/{algorithm}/field/encrypt", new { fieldName, value });
                    swEncrypt.Stop();

                    var body = await encryptRes.Content.ReadAsStringAsync();
                    string? encryptedValue = null;
                    double encryptServerMs = 0;
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("data", out var dataEl))
                        {
                            if (dataEl.TryGetProperty("OutputValue", out var outEl))
                                encryptedValue = outEl.GetString();
                            if (dataEl.TryGetProperty("ServerExecutionTimeMs", out var timeEl))
                                encryptServerMs = timeEl.GetDouble();
                        }
                    }
                    catch (JsonException) { /* body không phải JSON hợp lệ */ }

                    LogResult(algorithm.ToUpper(), "Encrypt", Encoding.UTF8.GetByteCount(value), swEncrypt.Elapsed.TotalMilliseconds, encryptServerMs, encryptRes.IsSuccessStatusCode);
                    encryptClientTimes.Add(swEncrypt.Elapsed.TotalMilliseconds);
                    encryptServerTimes.Add(encryptServerMs);

                    if (!encryptRes.IsSuccessStatusCode || encryptedValue == null)
                    {
                        Console.WriteLine("Encrypt lỗi/không đúng định dạng, bỏ qua Decrypt");
                        continue;
                    }

                    //  DECRYPT 
                    var swDecrypt = Stopwatch.StartNew();
                    var decryptRes = await httpClient.PostAsJsonAsync($"/api/encryptiontest/{algorithm}/field/decrypt", new { fieldName, value = encryptedValue });
                    swDecrypt.Stop();

                    double decryptServerMs = await ReadServerExecutionTimeMs(decryptRes);
                    LogResult(algorithm.ToUpper(), "Decrypt", Encoding.UTF8.GetByteCount(encryptedValue ?? ""), swDecrypt.Elapsed.TotalMilliseconds, decryptServerMs, decryptRes.IsSuccessStatusCode);
                    decryptClientTimes.Add(swDecrypt.Elapsed.TotalMilliseconds);
                    decryptServerTimes.Add(decryptServerMs);

                    Console.WriteLine($"Encrypt[Client={swEncrypt.Elapsed.TotalMilliseconds:F2}ms Server={encryptServerMs:F3}ms]  Decrypt[Client={swDecrypt.Elapsed.TotalMilliseconds:F2}ms Server={decryptServerMs:F3}ms]");
                }

                string tenThaoTac = algorithm == "hash" ? "Hash" : "Encrypt";
                PrintSummary($"{tenThaoTac} (Client round-trip)", encryptClientTimes);
                PrintSummary($"{tenThaoTac} (Server thuần thuật toán)", encryptServerTimes);
                if (algorithm != "plaintext" && algorithm != "hash" && algorithm != "hashindex")
                {
                    PrintSummary("Decrypt (Client round-trip)", decryptClientTimes);
                    PrintSummary("Decrypt (Server thuần thuật toán)", decryptServerTimes);
                }

                Console.WriteLine($"\nLog chi tiết tại: {LogFilePath}");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Đọc field "ServerExecutionTimeMs" mà server trả về trong response -
        /// đây là thời gian server tự đo (Stopwatch bọc sát quanh dòng gọi thuật toán).
        /// </summary>
        private static async Task<double> ReadServerExecutionTimeMs(HttpResponseMessage res)
        {
            try
            {
                var body = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                    dataEl.TryGetProperty("ServerExecutionTimeMs", out var timeEl))
                {
                    return timeEl.GetDouble();
                }
            }
            catch { /* bỏ qua, trả về 0 nếu không đọc được */ }
            return 0;
        }

        private static void ConfigureSerilog()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(a => a.File(
                    path: LogFilePath,
                    outputTemplate: "{Message:lj}{NewLine}"))
                .CreateLogger();
        }

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
            int idx = (lanThu - 1) % _testData.Count;
            return _testData[idx];
        }

        private static void EnsureLogFileExists()
        {
            if (!Directory.Exists(LogFolder)) Directory.CreateDirectory(LogFolder);

            if (!File.Exists(LogFilePath))
            {
                var header = "Timestamp,Algorithm,Operation,DataSizeBytes,ClientRoundTripMs,ServerExecutionMs,Success" + Environment.NewLine;
                File.WriteAllText(LogFilePath, header, Encoding.UTF8);
            }
        }

        private static void LogResult(string algorithm, string operation, long dataSizeBytes, double clientMs, double serverMs, bool success)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"{timestamp},{algorithm},{operation},{dataSizeBytes},{clientMs:F3},{serverMs:F3},{success}";
            Log.Information(line);
        }

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
            Console.WriteLine($"Trung bình : {sorted.Average():F3} ms");
            Console.WriteLine($"Trung vị   : {median:F3} ms   (ít bị lệch bởi outlier hơn Trung bình)");
            Console.WriteLine($"Nhỏ nhất   : {sorted.First():F3} ms");
            Console.WriteLine($"Lớn nhất   : {sorted.Last():F3} ms");
        }
    }
}