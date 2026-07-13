# Hướng dẫn chạy Performance Test (CV3 + CV4)

Hướng dẫn này dùng để test hiệu năng các endpoint mã hóa (Plaintext/AES/RSA/FPE) của `EncryptionTestController`.

## 1. Chuẩn bị

### 1.1. Cấu hình `.env`
Đảm bảo file `JeeBeginner-API/.env` đã có đủ:
```
ConnectionStrings__DefaultConnection=...
JWT__Secret=...
Encryption__AesKey=...
Encryption__FpeKey=...
Encryption__FpeTweak=...
Encryption__RsaPrivateKey=...
Encryption__RsaPublicKey=...
```

### 1.2. Cài K6 (nếu chưa có)
```powershell
winget install k6 --source winget
```
Kiểm tra: `k6 version`

## 2. Chạy API

Mở 1 terminal, **để chạy xuyên suốt** trong lúc test:
```powershell
cd JeeBeginner-API
dotnet run --launch-profile "JeeBeginner"
```
Xác nhận thấy dòng `Now listening on: https://localhost:1404` là API đã sẵn sàng.

## 3. Chạy Load Test bằng K6 (CV4)

Mở **terminal khác** (giữ nguyên terminal chạy API ở bước 2):
```powershell
cd JeeBeginner-API\LoadTests
```

### 3.1. Test nhanh (kiểm tra script chạy đúng chưa)
```powershell
k6 run --env LOAD_LEVEL=2 --env ALGO=aes test.js
```
Kỳ vọng: `checks_succeeded` gần 100%.

### 3.2. Chạy đầy đủ 4 thuật toán x 3 mức tải (12 lệnh)

```powershell
k6 run --env LOAD_LEVEL=50  --env ALGO=plaintext --out json=../LoadTestResults/results_plaintext_50vu.json  test.js
k6 run --env LOAD_LEVEL=100 --env ALGO=plaintext --out json=../LoadTestResults/results_plaintext_100vu.json test.js
k6 run --env LOAD_LEVEL=200 --env ALGO=plaintext --out json=../LoadTestResults/results_plaintext_200vu.json test.js

k6 run --env LOAD_LEVEL=50  --env ALGO=aes --out json=../LoadTestResults/results_aes_50vu.json  test.js
k6 run --env LOAD_LEVEL=100 --env ALGO=aes --out json=../LoadTestResults/results_aes_100vu.json test.js
k6 run --env LOAD_LEVEL=200 --env ALGO=aes --out json=../LoadTestResults/results_aes_200vu.json test.js

k6 run --env LOAD_LEVEL=50  --env ALGO=rsa --out json=../LoadTestResults/results_rsa_50vu.json  test.js
k6 run --env LOAD_LEVEL=100 --env ALGO=rsa --out json=../LoadTestResults/results_rsa_100vu.json test.js
k6 run --env LOAD_LEVEL=200 --env ALGO=rsa --out json=../LoadTestResults/results_rsa_200vu.json test.js

k6 run --env LOAD_LEVEL=50  --env ALGO=fpe --out json=../LoadTestResults/results_fpe_50vu.json  test.js
k6 run --env LOAD_LEVEL=100 --env ALGO=fpe --out json=../LoadTestResults/results_fpe_100vu.json test.js
k6 run --env LOAD_LEVEL=200 --env ALGO=fpe --out json=../LoadTestResults/results_fpe_200vu.json test.js
```

Kết quả: 12 file `.json` trong `JeeBeginner-API/LoadTestResults/` — mỗi file là log thô (raw) của 1 lượt test (1 thuật toán x 1 mức tải), chứa Latency (`http_req_duration`, `encrypt_duration_ms`, `decrypt_duration_ms`) và Throughput (`http_reqs`).

### 3.3. Đọc kết quả nhanh trên terminal (không cần mở file)
Sau mỗi lệnh, K6 tự in bảng thống kê ngay trên terminal:
- `encrypt_duration_ms` / `decrypt_duration_ms` — Latency riêng biệt của Encrypt/Decrypt (avg, p95, max)
- `http_reqs` — tổng số request + tốc độ request/giây (Throughput)
- `checks_succeeded` — % request thành công (100% là tốt, thấp hơn nghĩa là có lỗi/hệ thống quá tải)

## 4. Chạy đo bằng System.Diagnostics.Stopwatch (CV3)

Đây là Console App riêng (`PerformanceTestClient`), độc lập với `JeeBeginner-API`, dùng `Stopwatch` đo thời gian round-trip khi gọi API.

```powershell
cd PerformanceTestClient
dotnet run -- aes 2 10        # test nhanh: AES, 2 luồng, chạy 10 giây
```

Chạy đủ khi đã xác nhận ổn:
```powershell
dotnet run -- aes 50 30
dotnet run -- aes 100 30
dotnet run -- aes 200 30
```
Đổi `aes` thành `rsa`, `fpe`, `plaintext` để chạy hết các thuật toán còn lại.

Log ghi ra: `PerformanceTestClient/Logs/performance_log.csv`

## 5. Cấu trúc thư mục liên quan

```
Employee/
├── JeeBeginner-API/
│   ├── .env                        
│   ├── Classes/PerformanceLogger.cs
│   ├── Controllers/EncryptionTestController.cs
│   ├── Services/Encryption/EncryptionService.cs
│   ├── LoadTests/
│   │   └── test.js                 # script K6
│   └── LoadTestResults/            # log thô K6 xuất ra, KHÔNG push git
├── PerformanceTestClient/
│   ├── Program.cs                  # Console App dùng Stopwatch
│   └── Logs/performance_log.csv    # log Stopwatch, KHÔNG push git
```

## 6. Lưu ý quan trọng

- **Không restart API giữa lúc encrypt/decrypt RSA** — khóa RSA hiện đang tự sinh ngẫu nhiên mỗi lần app khởi động lại (`Encryption:RsaPrivateKey` cần cấu hình cố định trong `.env` để tránh vấn đề này).
- File log (`.csv`, `.json`) đều đã được thêm vào `.gitignore`, không bị push lên git.
- Field `SDT`/`CCCD` dùng chế độ FPE `FpeDigits`, các field khác (`HoTen`, `DiaChi`) dùng `FpeAlphaNumeric` — quyết định bởi `IsDigitField()` trong `EncryptionTestController.cs`.
