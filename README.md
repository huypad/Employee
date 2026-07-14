# — Đo hiệu năng mã hóa (Performance Testing)

Tài liệu này hướng dẫn chạy và giải thích kết quả của 2 công việc:
- Dùng `System.Diagnostics.Stopwatch` (C#) đo thời gian mã hóa/giải mã/hash.
- Dùng **K6** giả lập nhiều người dùng gọi cùng lúc (50/100/200), đo độ trễ (Latency) và thông lượng (Throughput).

Cả 2 đều gọi vào endpoint mã hóa thật do  viết (`EncryptionTestController`), không đụng vào database — dữ liệu test là tự sinh ngẫu nhiên (giả lập).

---

## 1. Cấu trúc thư mục liên quan

```
Employee/
├── JeeBeginner-API/              ← API chính 
│   ├── .env                       ← khóa bí mật (connection string, JWT, khóa mã hóa) - KHÔNG push git
│   ├── Controllers/EncryptionTestController.cs   ← endpoint mã hóa 
│   ├── Services/Encryption/EncryptionService.cs  ← logic thuật toán AES/RSA/FPE/Hash 
│   ├── LoadTests/
│   │   └── test.js                ← script K6 
│   ├── LoadTestResults/           ← log thô do K6 xuất ra (.json) - KHÔNG push git
│   └── summarize-results.js       ← script tổng hợp bảng so sánh từ LoadTestResults/
│
├── PerformanceTestClient/         ← chương trình C# riêng 
│   ├── Program.cs                 ← dùng Stopwatch gọi API, đo thời gian
│   └── Logs/performance_log.csv   ← log thô do Stopwatch ghi - KHÔNG push git
│
└── README.md                      ← chính là file này
```

---

## 2. Chuẩn bị trước khi chạy

### 2.1. File `.env`
Đảm bảo `JeeBeginner-API/.env` có đủ các khóa sau (xin từ người quản lý khóa, không tự bịa):
```
ConnectionStrings__DefaultConnection=...
JWT__Secret=...
JWT__JwtExpireHours=...
Encryption__AesKey=...
Encryption__FpeKey=...
Encryption__FpeTweak=...
Encryption__HmacKey=...
Encryption__RsaPrivateKey=...
Encryption__RsaPublicKey=...
```
Thiếu bất kỳ khóa nào trong nhóm `Encryption__*`, API **sẽ không khởi động được** (báo lỗi ngay, đây là chủ đích để tránh dùng khóa không an toàn).

### 2.2. Cài đặt cần có
- .NET SDK (8.0)
- [K6](https://k6.io/) — cài bằng `winget install k6 --source winget`
- Node.js — để chạy script tổng hợp `summarize-results.js`

---

## 3. Cách chạy

### Bước 1: Chạy API (bắt buộc, để mọi test bên dưới gọi vào)
```powershell
cd JeeBeginner-API
dotnet run --launch-profile "JeeBeginner"
```
Giữ nguyên terminal này chạy xuyên suốt. Xác nhận thấy dòng `Now listening on: https://localhost:1404`.

### Bước 2: Chạy — đo bằng Stopwatch (C#)
Mở terminal khác:
```powershell
cd PerformanceTestClient
dotnet run -- <thuật_toán> <số_lần_gọi>
```
Ví dụ:
```powershell
dotnet run -- aes 20
dotnet run -- rsa 20
dotnet run -- fpe 20
dotnet run -- hash 20
dotnet run -- plaintext 20
```
`<thuật_toán>` là 1 trong 5 giá trị: `plaintext`, `aes`, `rsa`, `fpe`, `hash`.
Kết quả in ra ngay trên terminal, đồng thời ghi chi tiết vào `PerformanceTestClient/Logs/performance_log.csv`.

### Bước 3: Chạy — giả lập tải bằng K6
```powershell
cd JeeBeginner-API\LoadTests
k6 run --env LOAD_LEVEL=<so_VU> --env ALGO=<thuật_toán> --out json=../LoadTestResults/results_<ten>.json test.js
```
Chạy đủ **5 thuật toán × 3 mức tải (50/100/200)** = 15 lệnh:
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

k6 run --env LOAD_LEVEL=50  --env ALGO=hash --out json=../LoadTestResults/results_hash_50vu.json  test.js
k6 run --env LOAD_LEVEL=100 --env ALGO=hash --out json=../LoadTestResults/results_hash_100vu.json test.js
k6 run --env LOAD_LEVEL=200 --env ALGO=hash --out json=../LoadTestResults/results_hash_200vu.json test.js
```
Mỗi lệnh chạy khoảng 30 giây, xuất ra 1 file `.json` trong `LoadTestResults/`.

### Bước 4: Tổng hợp kết quả thành bảng dễ đọc
```powershell
cd JeeBeginner-API
node summarize-results.js
```
In ra bảng so sánh 15 dòng, đồng thời xuất `LoadTestResults/summary.csv` (mở bằng Excel).

---

## 4. Giải thích các chỉ số trong kết quả K6

Khi chạy xong 1 lệnh `k6 run`, terminal in ra 1 khối kết quả dạng:

```
checks_total.......: 6000     197.38/s
checks_succeeded...: 100.00%  6000 out of 6000
checks_failed......: 0.00%    0 out of 6000

hash_duration_ms...: avg=4.66  min=0  med=2.10  max=72.67  p(90)=10.59  p(95)=19.07

http_req_duration..: avg=4.65ms  min=0s  med=2.1ms  max=72.66ms  p(90)=10.58ms  p(95)=19.07ms
http_req_failed....: 0.00%  0 out of 6000
http_reqs..........: 6000   197.38/s

vus................: 200    min=200  max=200
iterations.........: 6000   197.38/s
```

| Chỉ số | Ý nghĩa |
|---|---|
| `checks_total` | Tổng số lần kiểm tra điều kiện (VD "status phải là 200") đã chạy |
| `checks_succeeded` | % số lần kiểm tra đúng — **mong muốn luôn 100%** |
| `checks_failed` | % số lần kiểm tra sai — **mong muốn luôn 0%**, khác 0 nghĩa là có request lỗi/hệ thống quá tải |
| `avg` | Trung bình cộng của tất cả các lần đo |
| `med` (median) | Giá trị đứng giữa nếu xếp tất cả các lần đo theo thứ tự tăng dần — ít bị ảnh hưởng bởi vài giá trị bất thường hơn `avg` |
| `min` / `max` | Lần nhanh nhất / chậm nhất trong toàn bộ các lần đo |
| `p(90)`, `p(95)` | **Percentile** — `p(95)=19.07ms` nghĩa là 95% số lần gọi có thời gian ≤ 19.07ms, chỉ 5% số lần chậm hơn con số này. Đây là chỉ số quan trọng để đánh giá "trường hợp xấu nhưng vẫn thường xảy ra", ít bị lệch bởi 1-2 giá trị đột biến như `max` |
| `hash_duration_ms` (hoặc `encrypt_duration_ms`/`decrypt_duration_ms`) | Chỉ số **tự định nghĩa riêng** trong `test.js`, đo thời gian gọi đúng 1 loại thao tác (Hash/Encrypt/Decrypt), tách biệt khỏi `http_req_duration` chung |
| `http_req_duration` | Thời gian toàn bộ 1 request HTTP (gửi → nhận phản hồi), do K6 tự động đo cho mọi request |
| `vus` | Số "người dùng ảo" (Virtual Users) đang chạy đồng thời — chính là mức tải 50/100/200 |
| `iterations` | Tổng số lượt gọi hoàn thành. Số phía sau (VD `197.38/s`) là **Throughput** — số lượt xử lý được mỗi giây |

---

## 5. Giải thích bảng tổng hợp (`summarize-results.js` xuất ra)

```
File                      Checks Fail  HTTP avg(ms)  HTTP p95(ms)  Encrypt/Hash avg  Decrypt avg
results_aes_100vu.json    0/9000       3.54          12.33         3.93              3.14
results_rsa_200vu.json    0/17619      14.73         38.56         16.33             13.13
```

| Cột | Lấy từ đâu | Ý nghĩa |
|---|---|---|
| `File` | Tên file `.json` trong `LoadTestResults/` | Cho biết đây là kết quả của thuật toán nào, mức tải bao nhiêu (đọc từ tên file, VD `aes_100vu` = AES, 100 VUs) |
| `Checks Fail` | Đếm số dòng `metric: "checks"` có giá trị `0` trong file `.json`, chia cho tổng | Số lượt kiểm tra thất bại / tổng số lượt — **luôn cần là `0/...`** |
| `HTTP avg(ms)` / `HTTP p95(ms)` | Gom toàn bộ giá trị `metric: "http_req_duration"` trong file, tính trung bình và percentile 95% | Độ trễ tổng của cả request (bao gồm mã hóa + network) |
| `Encrypt/Hash avg` | Gom giá trị `metric: "encrypt_duration_ms"` (AES/RSA/FPE) hoặc `"hash_duration_ms"` (Hash) | Thời gian riêng của bước mã hóa/hash, tách khỏi decrypt |
| `Decrypt avg` | Gom giá trị `metric: "decrypt_duration_ms"` | Thời gian riêng của bước giải mã. Plaintext/Hash không có cột này (hiện dấu `-`) vì bản chất không có bước giải mã |

**Vì sao có dòng `results_rsa_200vu.json` chỉ có `17619` request thay vì `18000`:** RSA xử lý chậm hơn AES/FPE, nên trong cùng 30 giây, mỗi VU hoàn thành được ít vòng lặp hơn một chút → tổng số request thấp hơn. Đây là bằng chứng cụ thể cho thấy RSA chậm hơn các thuật toán khác, không phải lỗi.

---

## 6. Kết luận rút ra được (từ dữ liệu đã chạy)

- Cả 5 thuật toán đều **ổn định**, không có request nào lỗi ở bất kỳ mức tải nào (0-200 VUs).
- **Thứ tự tốc độ (nhanh → chậm):** Hash ≈ AES ≈ FPE (nhóm nhanh, ổn định) → RSA (chậm nhất, tăng rõ rệt khi tải cao — chi phí xử lý gần như tăng gấp đôi khi tải tăng gấp đôi).
- Hash (HMAC-SHA256) nhanh nhất vì chỉ xử lý 1 chiều, không có bước giải mã.
- RSA nên hạn chế dùng cho khối lượng lớn/tải cao do chi phí xử lý nặng hơn hẳn AES/FPE/Hash.
