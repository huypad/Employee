# JeeBeginner — Hướng dẫn chạy toàn bộ hệ thống

Tài liệu này hướng dẫn từ A-Z: setup database, chạy API, chạy giao diện (UI), và chạy 2 công cụ đo hiệu năng mã hóa 

---

## 0. Cấu trúc thư mục tổng thể

```
Employee/
├── JeeBeginner-API/              ← API backend (.NET) —  (Nhân viên) +  (Mã hóa)
│   ├── .env                       ← khóa bí mật - KHÔNG push git
│   ├── Controllers/
│   │   ├── NhanVienManagementController.cs   ← CRUD nhân viên
│   │   └── EncryptionTestController.cs        ← endpoint test mã hóa 
│   ├── Services/Encryption/EncryptionService.cs   ← logic AES/RSA/FPE/Hash 
│   ├── Reponsitories/NhanVienManagement/          ← gọi mã hóa khi lưu/đọc nhân viên
│   ├── Scripts/                   ← các script SQL (xem mục 1)
│   ├── LoadTests/test.js          ← script K6 
│   ├── LoadTestResults/           ← log thô K6 xuất ra - KHÔNG push git
│   └── summarize-results.js       ← script tổng hợp bảng so sánh
│
├── JeeBeginner-BE/JeeBeginner/    ← Giao diện (Angular)
│
├── PerformanceTestClient/         ← chương trình C# riêng
│   ├── Program.cs                 ← dùng Stopwatch gọi API, đo thời gian
│   ├── test-data-200.json         ← 200 bản ghi test cố định
│   └── Logs/performance_log.csv   ← log thô Stopwatch - KHÔNG push git
│
└── README.md                      ← chính là file này
```

---

## 1. Setup Database

Database dùng file backup `.bak` do người quản lý DB (Kieu Oanh) cung cấp — **không dùng script SQL cũ trong `Scripts/` để tạo bảng `Tbl_Nhanvien` từ đầu**, vì các script đó chỉ là ALTER (thêm cột), giả định bảng đã tồn tại sẵn.

### Cách restore:
1. Mở SSMS → chuột phải **Databases** → **Restore Database...**
2. Chọn **Device** → **Add** → trỏ tới file `.bak` mới nhất được cung cấp
3. Nếu máy đã có sẵn database `JeeBeginner` từ trước, **restore đè lên** để lấy đúng schema mới nhất
4. Vào tab **Files**, sửa lại đường dẫn `.mdf`/`.ldf` cho khớp ổ đĩa máy bạn
5. Kiểm tra lại bằng câu lệnh:
```sql
SELECT TOP 5 Id_NV, MaNV, Holot, Ten, Mobile, CMND, CMND_Enc, CMNDHash
FROM dbo.Tbl_Nhanvien;
```
Nếu ra kết quả (không lỗi "Invalid object name") là restore đúng.

---

## 2. File `.env`

Đặt tại `JeeBeginner-API/.env`, cần đủ các khóa sau (xin từ người quản lý khóa):
```
ConnectionStrings__DefaultConnection=Data Source=...;Initial Catalog=JeeBeginner;...
JWT__Secret=...
JWT__JwtExpireHours=24
Encryption__AesKey=...
Encryption__FpeKey=...
Encryption__FpeTweak=...
Encryption__HmacKey=...
Encryption__RsaPrivateKey=...
Encryption__RsaPublicKey=...
```
Thiếu bất kỳ khóa `Encryption__*` nào, API **sẽ không khởi động được** (chủ đích, để tránh chạy với khóa không an toàn).

---

## 3. Chạy API

```powershell
cd JeeBeginner-API
dotnet build
dotnet run --launch-profile "JeeBeginner"
```
Xác nhận thấy `Now listening on: https://localhost:1404`. Giữ terminal này chạy xuyên suốt.

Test nhanh qua Swagger: `https://localhost:1404/swagger`

---

## 4. Chạy giao diện (UI - Angular)

### Yêu cầu: Node 16.x (project Angular 11 cũ, không tương thích Node bản quá mới)
```powershell
nvm install 16.20.2
nvm use 16.20.2
```

### Chạy:
```powershell
cd JeeBeginner-BE\JeeBeginner
npm install
npm start
```
Mở `http://localhost:4002`.

### Đăng nhập:
Dùng tài khoản có sẵn trong bảng `AccountList` (ví dụ `huytran`).

### Kiểm tra menu "Quản lý nhân viên":
- Danh sách nhân viên phải hiển thị **đúng Họ Tên và CCCD dạng đọc được** (VD "Trần Văn An", "100000000001"), **không phải** chuỗi mã hóa dạng `AESGCM:v1:...`
- Có thể **Thêm / Import Excel / Khóa-Mở khóa** nhân viên — mỗi lần Thêm/Sửa sẽ tự động gọi mã hóa (AES) trước khi lưu xuống DB, và tự giải mã khi hiển thị lại lên UI.

---

## 5. Đo thời gian bằng `System.Diagnostics.Stopwatch`

```powershell
cd PerformanceTestClient
dotnet build
dotnet run -- <thuật_toán> <số_lần_gọi>
```
`<thuật_toán>`: `plaintext`, `aes`, `rsa`, `fpe`, `hash`.

Ví dụ:
```powershell
dotnet run -- aes 20
dotnet run -- rsa 20
dotnet run -- fpe 20
dotnet run -- hash 20
dotnet run -- plaintext 20
```
Kết quả in ra terminal + ghi vào `PerformanceTestClient/Logs/performance_log.csv`. Dữ liệu test lấy tuần tự từ `test-data-200.json` (200 bản ghi cố định, không phải random), đảm bảo tái lập được kết quả giữa các lần chạy.

---

## 6. Giả lập tải bằng K6

Cài K6 (nếu chưa có): `winget install k6 --source winget`

```powershell
cd JeeBeginner-API\LoadTests
k6 run --env LOAD_LEVEL=<so_VU> --env ALGO=<thuật_toán> --out json=../LoadTestResults/results_<ten>.json test.js
```

Chạy đủ 5 thuật toán × 3 mức tải (50/100/200) = 15 lệnh:
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

### Tổng hợp kết quả:
```powershell
cd JeeBeginner-API
node summarize-results.js
```
In ra bảng so sánh 15 dòng + xuất `LoadTestResults/summary.csv`.

---

## 7. Giải thích các chỉ số kết quả

| Chỉ số | Ý nghĩa |
|---|---|
| `checks_succeeded` / `checks_failed` | % request đúng/sai — mong muốn luôn 100%/0% |
| `avg` | Trung bình cộng tất cả các lần đo |
| `med` (median) | Giá trị đứng giữa khi xếp các lần đo theo thứ tự tăng dần |
| `p(90)`, `p(95)` | Percentile — `p(95)=19ms` nghĩa là 95% số lần gọi ≤ 19ms, chỉ 5% chậm hơn. Ít bị lệch bởi giá trị đột biến hơn `avg`/`max` |
| `http_req_duration` | Thời gian toàn bộ 1 request (K6 tự đo) |
| `encrypt_duration_ms` / `decrypt_duration_ms` / `hash_duration_ms` | Chỉ số tự định nghĩa trong `test.js`, tách riêng từng loại thao tác |
| `vus` | Số "người dùng ảo" chạy đồng thời — mức tải 50/100/200 |
| `iterations` | Tổng số lượt gọi hoàn thành; số kèm theo (VD `197/s`) là **Throughput** |

### Bảng tổng hợp (`summarize-results.js`):
| Cột | Ý nghĩa |
|---|---|
| `Checks Fail` | Số lượt kiểm tra thất bại / tổng số lượt |
| `HTTP avg/p95` | Độ trễ tổng của cả request (mã hóa + network) |
| `Encrypt/Hash avg` | Thời gian riêng bước mã hóa/hash |
| `Decrypt avg` | Thời gian riêng bước giải mã (Plaintext/Hash không có, hiện `-`) |

---

## 8. Kết luận rút ra từ số liệu đã chạy

- Cả 5 cách xử lý (Plaintext/AES/RSA/FPE/Hash) đều ổn định, không lỗi ở mọi mức tải (0-200 VUs).
- **Thứ tự tốc độ (nhanh → chậm):** Hash ≈ AES ≈ FPE (nhanh, ổn định) → RSA (chậm nhất, tăng rõ rệt khi tải cao).
- RSA nên hạn chế dùng cho khối lượng lớn/tải cao do chi phí xử lý nặng hơn hẳn.
- Hệ thống thật (UI Quản lý Nhân viên) đã tích hợp đúng: mã hóa khi lưu, giải mã khi hiển thị, đã kiểm chứng qua giao diện thực tế.

---

## 9. Lưu ý bảo mật

- File `.env` và mọi file log (`Logs/*.csv`, `LoadTestResults/*.json`) đều đã đưa vào `.gitignore` — không được push lên git.
- Không restart API giữa lúc test RSA nếu khóa RSA chưa cấu hình cố định trong `.env` (nay đã bắt buộc cấu hình, không còn tự sinh ngẫu nhiên nữa).