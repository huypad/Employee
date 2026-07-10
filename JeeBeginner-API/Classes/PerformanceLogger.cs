using System;
using System.IO;
using System.Text;

namespace JeeBeginner.Classes
{
    /// <summary>
    /// Class dùng để đo và ghi log thời gian thực thi (Encrypt/Decrypt) của các thuật toán mã hóa.
    /// Sử dụng System.Diagnostics.Stopwatch để đo thời gian xử lý phía server.
    /// đo lường hiệu năng mã hóa/giải mã (AES, RSA, FPE...)
    /// </summary>
    public static class PerformanceLogger
    {
        // Đường dẫn file log - dùng Directory.GetCurrentDirectory() để luôn nằm ngay trong gốc project
        private static readonly string _logFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        private static readonly string _logFilePath = Path.Combine(_logFolder, "performance_log.csv");

        // Khóa để tránh nhiều request ghi file cùng lúc bị đụng nhau (race condition)
        private static readonly object _lockObj = new object();

        /// <summary>
        /// Khởi tạo file log (tạo folder + ghi header nếu file chưa tồn tại).
        /// Gọi 1 lần khi ứng dụng start (ví dụ trong Startup.cs), hoặc để tự động check mỗi lần ghi.
        /// </summary>
        private static void EnsureLogFileExists()
        {
            if (!Directory.Exists(_logFolder))
            {
                Directory.CreateDirectory(_logFolder);
            }

            if (!File.Exists(_logFilePath))
            {
                var header = "Timestamp,Algorithm,Operation,DataSizeBytes,ExecutionTimeMs,LoadLevel" + Environment.NewLine;
                File.WriteAllText(_logFilePath, header, Encoding.UTF8);
            }
        }

        /// <summary>
        /// Ghi 1 dòng log kết quả đo thời gian thực thi.
        /// </summary>
        /// <param name="algorithm">Tên thuật toán: Plaintext, AES, RSA, FPE...</param>
        /// <param name="operation">Encrypt hoặc Decrypt</param>
        /// <param name="dataSizeBytes">Kích thước dữ liệu đầu vào (byte)</param>
        /// <param name="executionTimeMs">Thời gian thực thi đo được (ms), lấy từ sw.Elapsed.TotalMilliseconds</param>
        /// <param name="loadLevel">Mức tải đang test (ví dụ: 50, 100, 200) - optional, dùng để lọc khi ghép với log K6</param>
        public static void Log(string algorithm, string operation, long dataSizeBytes, double executionTimeMs, string loadLevel = "")
        {
            try
            {
                EnsureLogFileExists();

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var line = $"{timestamp},{algorithm},{operation},{dataSizeBytes},{executionTimeMs:F3},{loadLevel}" + Environment.NewLine;

                // Lock để đảm bảo an toàn khi nhiều request ghi file đồng thời (quan trọng khi test tải 50-200 request cùng lúc)
                lock (_lockObj)
                {
                    File.AppendAllText(_logFilePath, line, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Không throw exception ra ngoài - lỗi ghi log không được làm crash API chính
                // (nếu cần debug, có thể ghi ra Console hoặc log riêng ở đây)
            }
        }
    }
}