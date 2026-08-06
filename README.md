# JeeBeginner — Hướng dẫn chạy toàn bộ hệ thống

Tài liệu này hướng dẫn từ A-Z: setup database, chạy API, chạy giao diện (UI), và chạy 2 công cụ đo hiệu năng (mã hóa, tạo nhân viên, tìm kiếm)

---

## 0. Cấu trúc thư mục tổng thể

```
Employee/
├── JeeBeginner-API/                       ← API backend (.NET) — Nhân viên + Mã hóa
│   ├── .env                                ← khóa bí mật - KHÔNG push git
│   ├── Controllers/
│   │   ├── NhanVienManagementController.cs ← CRUD nhân viên + SearchTest (đo Hash/Db)
│   │   └── EncryptionTestController.cs     ← endpoint test 5 thuật toán mã hóa
│   ├── Services/Encryption/EncryptionService.cs   ← logic AES/RSA/FPE/Hash (RSA đã tối ưu bằng Lazy<RSA>, dùng chung 1 object thay vì tạo mới mỗi lần)
│   ├── Reponsitories/NhanVienManagement/   ← gọi mã hóa khi lưu/đọc nhân viên (CreateNhanVien có đo DbCheck/Encrypt/Insert)
│   ├── Scripts/                            ← các script SQL (xem mục 1)
│   ├── LoadTests/
│   │   ├── test.js                          ← K6: test 5 thuật toán mã hóa
│   │   ├── test-create-nhanvien.js          ← K6: test tạo nhân viên (ghi thật vào DB)
│   │   ├── test-search.js                   ← K6: test tìm kiếm (đọc CCCD thật)
│   │   ├── test-data-200.json               ← 30.000 bản ghi test cố định (tên file giữ nguyên dù không còn 200 dòng)
│   │   ├── Danh_sach_CMND.csv               ← 200.000+ CCCD thật, export từ DB test, dùng cho test-search.js
│   │   ├── run-k6.ps1                       ← script chạy nhanh test mã hóa (tự đặt tên file, tự tăng _lanN)
│   │   ├── run-k6-create.ps1                ← script chạy nhanh test tạo nhân viên
│   │   └── run-k6-search.ps1                ← script chạy nhanh test tìm kiếm
│   ├── LoadTestResults/                    ← log thô K6 xuất ra - KHÔNG push git (.gitignore: *.json)
│   └── summarize-results.js                ← script tổng hợp toàn bộ 3 loại test thành 1 bảng
│
├── JeeBeginner-BE/JeeBeginner/             ← Giao diện (Angular)
│
├── PerformanceTestClient/                  ← chương trình C# riêng, đo tuần tự bằng Stopwatch (không tải cao)
│   ├── Program.cs                          ← hỗ trợ: plaintext/aes/rsa/fpe/hash + create + search
│   ├── test-data-200.json                  ← bản copy riêng, 30.000 bản ghi (PHẢI khớp nội dung với bản trong LoadTests/)
│   ├── Danh_sach_CMND.csv                  ← bản copy riêng, dùng cho nhánh `search`
│   └── Logs/performance_log.csv            ← log thô Stopwatch - KHÔNG push git
│
└── README.md                               ← chính là file này
```

⚠️ **Lưu ý dữ liệu test dùng chung**: `test-data-200.json` và `Danh_sach_CMND.csv` tồn tại ở **2 nơi riêng biệt** trên đĩa (`LoadTests/` cho K6, `PerformanceTestClient/` cho Program.cs) — đây là 2 bản copy vật lý, không tự đồng bộ. Nếu cập nhật dữ liệu test, nhớ copy sang **cả 2 nơi**, không chỉ 1.

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
Xác nhận thấy `Now listening on: https://localhost:1404`. Giữ terminal này chạy xuyên suốt — **API phải đang chạy** trước khi thực hiện mục 5 và 6 bên dưới.

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
- Có thể **Thêm / Import Excel / Khóa-Mở khóa** nhân viên — mỗi lần Thêm/Sửa sẽ tự động gọi mã hóa (AES/RSA/FPE tùy trường) trước khi lưu xuống DB, và tự giải mã khi hiển thị lại lên UI.

---

## 5. Đo tuần tự bằng `System.Diagnostics.Stopwatch` (`PerformanceTestClient`)

Công cụ debug đơn lẻ, chạy **tuần tự từng lần gọi** (không phải tải cao nhiều luồng như K6) — dùng để xem chi tiết từng lần gọi, hoặc debug nhanh khi nghi ngờ có lỗi.

```powershell
cd PerformanceTestClient
dotnet build
```

### 5.1. Test 5 thuật toán mã hóa
```powershell
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
Có warm-up **30 lần** trước khi đo thật (không tính vào kết quả) — tránh bị lệch do cold-start (JIT compile lần đầu, RSA object chưa khởi tạo...).

### 5.2. Test tạo nhân viên (ghi thật vào DB)
```powershell
dotnet run -- create <số_lần_gọi> <username> <password>
```
Ví dụ:
```powershell
dotnet run -- create 20 huytran P@sswordJ33Beginn3r
```
- Bắt buộc truyền username/password trực tiếp trên dòng lệnh (không hardcode trong code, không dùng biến môi trường, để tránh lộ mật khẩu khi push code lên Git).
- Tự sinh `MaNV`/`CCCD` duy nhất mỗi lần chạy (dựa theo timestamp), không lo trùng dữ liệu giữa các lần chạy.
- **Không có warm-up** (mỗi lần gọi là 1 lần ghi thật vào DB — warm-up sẽ tạo thêm nhân viên rác không cần thiết). Do đó **Lần 1 thường bị cold-start** (chậm hơn hẳn các lần sau, do JIT/khởi tạo RSA lần đầu) — khi đọc kết quả nên ưu tiên xem **Trung vị** thay vì Trung bình, hoặc tự bỏ qua Lần 1 khi phân tích.
- Đo 4 giá trị mỗi lần: Client round-trip, DbCheck (kiểm tra trùng MaNV/CCCD), Encrypt (mã hóa), Insert (ghi SQL).

### 5.3. Test tìm kiếm (đọc CCCD thật)
```powershell
dotnet run -- search <số_lần_gọi>
```
Ví dụ:
```powershell
dotnet run -- search 20
```
- Không cần đăng nhập.
- Đọc từ `Danh_sach_CMND.csv` (nhớ đã copy file này vào `PerformanceTestClient/` — xem mục 0).
- Có warm-up 30 lần trước khi đo thật (dataset 200k+ dòng, cần làm nóng SQL Server buffer pool).
- Đo 3 giá trị mỗi lần: Client round-trip, Hash (tính blind-index), Db (truy vấn SELECT thật).

Kết quả cả 3 nhánh in ra terminal + ghi chung vào `PerformanceTestClient/Logs/performance_log.csv`.

---

## 6. Giả lập tải bằng K6

Cài K6 (nếu chưa có): `winget install k6 --source winget`

Có 3 kịch bản K6 tương ứng 3 nghiệp vụ, mỗi kịch bản có 1 script PowerShell (`.ps1`) đi kèm — **dùng script `.ps1` thay vì gõ tay `k6 run --env...`**, vì script tự động đặt tên file kết quả đúng chuẩn và tự tăng số `_lanN` nếu chạy lại cùng cấu hình (không lo ghi đè/trùng tên file).

```powershell
cd JeeBeginner-API\LoadTests
```

### 6.1. Test 5 thuật toán mã hóa (`run-k6.ps1`)
```powershell
.\run-k6.ps1 -Algo <thuật_toán> -Load <số_VU>
```
`<thuật_toán>`: `plaintext`, `aes`, `rsa`, `fpe`, `hash`. `<số_VU>`: `50`, `100`, `200`.

Bộ lệnh đầy đủ khuyến nghị (mỗi cấu hình chạy **2 lần** để `summarize-results.js` gộp nhóm, tăng độ tin cậy số liệu trung bình):
```powershell
.\run-k6.ps1 -Algo plaintext -Load 50
.\run-k6.ps1 -Algo plaintext -Load 50
.\run-k6.ps1 -Algo plaintext -Load 100
.\run-k6.ps1 -Algo plaintext -Load 100
.\run-k6.ps1 -Algo plaintext -Load 200
.\run-k6.ps1 -Algo plaintext -Load 200

.\run-k6.ps1 -Algo aes -Load 50
.\run-k6.ps1 -Algo aes -Load 50
.\run-k6.ps1 -Algo aes -Load 100
.\run-k6.ps1 -Algo aes -Load 100
.\run-k6.ps1 -Algo aes -Load 200
.\run-k6.ps1 -Algo aes -Load 200

.\run-k6.ps1 -Algo rsa -Load 50
.\run-k6.ps1 -Algo rsa -Load 50
.\run-k6.ps1 -Algo rsa -Load 100
.\run-k6.ps1 -Algo rsa -Load 100
.\run-k6.ps1 -Algo rsa -Load 200
.\run-k6.ps1 -Algo rsa -Load 200

.\run-k6.ps1 -Algo fpe -Load 50
.\run-k6.ps1 -Algo fpe -Load 50
.\run-k6.ps1 -Algo fpe -Load 100
.\run-k6.ps1 -Algo fpe -Load 100
.\run-k6.ps1 -Algo fpe -Load 200
.\run-k6.ps1 -Algo fpe -Load 200

.\run-k6.ps1 -Algo hash -Load 50
.\run-k6.ps1 -Algo hash -Load 50
.\run-k6.ps1 -Algo hash -Load 100
.\run-k6.ps1 -Algo hash -Load 100
.\run-k6.ps1 -Algo hash -Load 200
.\run-k6.ps1 -Algo hash -Load 200
```
Có warm-up 30 request/lần chạy (không tính vào kết quả).

> **Cách chạy trực tiếp bằng `k6 run` (không dùng script)** vẫn hoạt động nếu cần, nhưng phải tự đặt tên file `--out json=...` khác nhau cho mỗi lần chạy để tránh trùng, ví dụ:
> ```powershell
> k6 run --env LOAD_LEVEL=200 --env ALGO=aes --out json=../LoadTestResults/results_aes_200vu_lan1.json test.js
> ```

### 6.2. Test tạo nhân viên (`run-k6-create.ps1`)
```powershell
.\run-k6-create.ps1 -Rate <request/giây> -Username <tk> -Password <mk>
```
Ví dụ:
```powershell
.\run-k6-create.ps1 -Rate 50  -Username huytran -Password P@sswordJ33Beginn3r
.\run-k6-create.ps1 -Rate 100 -Username huytran -Password P@sswordJ33Beginn3r
.\run-k6-create.ps1 -Rate 200 -Username huytran -Password P@sswordJ33Beginn3r
```
- Dùng executor `constant-arrival-rate` (giữ đúng tốc độ request/giây, khác `constant-vus` bên test mã hóa).
- Tự sinh `MaNV`/`CCCD` duy nhất mỗi lần chạy (không trùng, kể cả chạy song song nhiều VU).
- Threshold lỏng hơn test mã hóa (`http_req_failed < 5%`, `checks > 90%`) vì đây là thao tác ghi.
- Ghi dữ liệu **thật** vào DB — mỗi lần chạy tạo ra `Rate × 30` nhân viên mới trong DB test.

### 6.3. Test tìm kiếm (`run-k6-search.ps1`)
```powershell
.\run-k6-search.ps1 -Rate <request/giây>
```
Ví dụ:
```powershell
.\run-k6-search.ps1 -Rate 50
.\run-k6-search.ps1 -Rate 100
.\run-k6-search.ps1 -Rate 200
```
- Không cần đăng nhập.
- Dùng CCCD thật từ `Danh_sach_CMND.csv`, xoay vòng round-robin.
- Có warm-up 30 request, nhưng **với `Rate=50` (mức tải thấp nhất, thường chạy đầu tiên trong phiên), nên chạy dư 1 lượt "xả" trước** (chạy `-Rate 50` một lần bỏ đi, không tính) — vì warm-up 30 request là quá nhỏ so với 200k+ dòng CCCD thật, SQL Server Buffer Pool chưa kịp "ấm" nếu đây là lượt tải đầu tiên trong phiên làm việc. Đã kiểm chứng thực nghiệm: lần chạy đầu tiên chậm hơn ~4.5 lần so với lần chạy sau khi đã qua ít nhất 1 lượt tải trước đó. Nếu chạy `Rate=50` sau khi đã chạy `Rate=100`/`Rate=200` trước đó thì không cần lượt xả riêng.

### Tổng hợp kết quả (dùng chung cho cả 3 loại test):
```powershell
cd JeeBeginner-API
node summarize-results.js
```
In ra 1 bảng duy nhất (gộp cả kết quả mã hóa/tạo/tìm kiếm) + xuất `LoadTestResults/summary.csv`.

---

## 7. Giải thích các chỉ số kết quả

| Chỉ số | Ý nghĩa |
|---|---|
| `checks_succeeded` / `checks_failed` | % request đúng/sai — mong muốn luôn 100%/0% |
| `avg` | Trung bình cộng tất cả các lần đo |
| `med` (median) | Giá trị đứng giữa khi xếp các lần đo theo thứ tự tăng dần |
| `p(90)`, `p(95)` | Percentile — `p(95)=19ms` nghĩa là 95% số lần gọi ≤ 19ms, chỉ 5% chậm hơn. Ít bị lệch bởi giá trị đột biến hơn `avg`/`max` |
| `http_req_duration` | Thời gian toàn bộ 1 request (K6 tự đo) = `http_req_sending + http_req_waiting + http_req_receiving` |
| `http_reqs` | Tổng số request HTTP đã gửi (K6 tự đếm) — dùng để tính Throughput (request/giây) |
| `encrypt_duration_ms` / `decrypt_duration_ms` / `hash_duration_ms` / `plaintext_duration_ms` | Client round-trip từng thao tác mã hóa, tự định nghĩa trong `test.js` |
| `encrypt_server_ms` / `decrypt_server_ms` / `hash_server_ms` / `plaintext_server_ms` | Thời gian **thuần thuật toán** phía server (Stopwatch nội bộ, không gồm network) |
| `create_duration_ms` | Client round-trip của request tạo nhân viên (`test-create-nhanvien.js`) |
| `create_db_check_ms` / `create_encrypt_ms` / `create_insert_ms` | 3 mốc Stopwatch phía server khi tạo nhân viên: kiểm tra trùng / mã hóa / ghi SQL |
| `search_duration_ms` | Client round-trip của request tìm kiếm (`test-search.js`) |
| `search_hash_ms` / `search_db_ms` | 2 mốc Stopwatch phía server khi tìm kiếm: tính blind-index hash / truy vấn DB |
| `vus` | Số "người dùng ảo" chạy đồng thời (chỉ áp dụng test mã hóa, dùng `constant-vus`) |
| `iterations` | Tổng số lượt gọi hoàn thành |

### Bảng tổng hợp (`summarize-results.js`):
| Cột | Ý nghĩa |
|---|---|
| `SoLanChay` | Số file cùng cấu hình đã gộp lại (chạy `_lan1`, `_lan2`...) |
| `ChecksFail` | Số lượt kiểm tra thất bại / tổng số lượt |
| `HTTPavg`/`HTTPp95` | Độ trễ toàn bộ chu trình = Encrypt avg + Decrypt avg (nếu có 2 chiều), hoặc chính Encrypt/Hash/Create/Search avg (nếu chỉ 1 chiều) |
| `Encrypt/Hash avg` | Client round-trip bước Encrypt (hoặc Hash/Plaintext/Create/Search, tùy nhóm) |
| `Decrypt avg` | Client round-trip bước Decrypt (chỉ có ở aes/rsa/fpe) |
| `EncServer avg`/`DecServer avg` | Thời gian thuần thuật toán phía server (Encrypt/Decrypt) |
| `Req/s` | Throughput = tổng số request / tổng thời lượng thực tế đã chạy |
| `DbCheck avg`/`CrEncrypt avg`/`Insert avg` | 3 bước con phía server khi tạo nhân viên |
| `SearchHash avg`/`SearchDb avg` | 2 bước con phía server khi tìm kiếm |

---

## 8. Kết luận rút ra từ số liệu đã chạy

- Cả 5 cách xử lý mã hóa (Plaintext/AES/RSA/FPE/Hash) đều ổn định, không lỗi ở mọi mức tải (50-200 VUs).
- **Thứ tự tốc độ (nhanh → chậm):** Hash ≈ AES ≈ FPE (nhanh, ổn định, encrypt/decrypt gần đối xứng) → RSA (chậm nhất, đặc biệt Decrypt chậm hơn Encrypt khoảng 10-20 lần do bản chất khóa riêng tư). Sau khi tối ưu `EncryptionService` dùng chung 1 object RSA (`Lazy<RSA>` thay vì tạo mới mỗi lần), thời gian thuần thuật toán RSA đã giảm khoảng **50%** so với bản trước tối ưu.
- RSA nên hạn chế dùng cho khối lượng lớn/tải cao; nếu bắt buộc dùng, ưu tiên mô hình hybrid (RSA chỉ mã hóa 1 khóa AES ngắn, không mã hóa trực tiếp dữ liệu lớn) — đã áp dụng đúng cách này trong `EncryptionService`.
- **Tạo nhân viên**: bottleneck chủ yếu ở bước kiểm tra trùng (DbCheck) và ghi SQL (Insert), bước mã hóa (Encrypt) gần như không đáng kể so với 2 bước còn lại.
- **Tìm kiếm**: bottleneck gần như hoàn toàn nằm ở tầng truy vấn DB (SearchDb), bước tính blind-index hash (SearchHash) nhanh hơn hàng chục lần, không phải điểm cần tối ưu.
- Cold-start (lần gọi đầu tiên trong phiên) luôn chậm hơn đáng kể các lần sau — cần warm-up đủ số lần, và với dataset lớn (search) cần warm-up nhiều hơn nữa hoặc chạy 1 lượt "xả" trước khi lấy số liệu chính thức.
- Hệ thống thật (UI Quản lý Nhân viên) đã tích hợp đúng: mã hóa khi lưu, giải mã khi hiển thị, đã kiểm chứng qua giao diện thực tế.

---

## 9. Lưu ý bảo mật

- File `.env` và mọi file log (`Logs/*.csv`, `LoadTestResults/*.json`) đều đã đưa vào `.gitignore` — không được push lên git.
- **Không hardcode username/password vào code** (kể cả `Program.cs`) — luôn truyền qua tham số dòng lệnh lúc chạy (`dotnet run -- create ... <username> <password>`, `.\run-k6-create.ps1 -Username ... -Password ...`).
- Không restart API giữa lúc test RSA nếu khóa RSA chưa cấu hình cố định trong `.env` (nay đã bắt buộc cấu hình, không còn tự sinh ngẫu nhiên nữa).
