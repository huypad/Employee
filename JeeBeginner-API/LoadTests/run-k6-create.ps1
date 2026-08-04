# run-k6-create.ps1 - Chạy K6 cho test-create-nhanvien.js (INSERT nhân viên)
# và TỰ ĐỘNG đánh số lần chạy (_lan1, _lan2, _lan3...) giống run-k6.ps1.
#
# Cách dùng:
#   .\run-k6-create.ps1 -Rate 50 -Username huytran -Password xxx
#   .\run-k6-create.ps1 -Rate 50 -Username huytran -Password xxx   <- chạy lại lần nữa, tự động ra "_lan2"
#   .\run-k6-create.ps1 -Rate 100 -Username huytran -Password xxx
#
# LƯU Ý QUAN TRỌNG: chỉ chạy khi HOST trong test-create-nhanvien.js đang trỏ
# vào DB TEST (không phải DB thật/production) - vì script này ghi (INSERT)
# dữ liệu thật vào database.

param(
    [Parameter(Mandatory=$true)][int]$Rate,
    [Parameter(Mandatory=$true)][string]$Username,
    [Parameter(Mandatory=$true)][string]$Password
)

$resultsDir = "../LoadTestResults"

if (!(Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir | Out-Null
}

# Tìm các file đã có cho đúng cấu hình này (rate), lấy số "lan" lớn nhất đang có
$pattern = "results_create_${Rate}rps_lan*.json"
$existingFiles = Get-ChildItem -Path $resultsDir -Filter $pattern -ErrorAction SilentlyContinue

$maxLan = 0
foreach ($f in $existingFiles) {
    if ($f.Name -match "_lan(\d+)\.json$") {
        $n = [int]$matches[1]
        if ($n -gt $maxLan) { $maxLan = $n }
    }
}

$nextLan = $maxLan + 1
$outputFile = "$resultsDir/results_create_${Rate}rps_lan${nextLan}.json"

Write-Host "=== Chạy K6: Tạo nhân viên (Insert), Rate=$Rate req/s, Lần thứ $nextLan ===" -ForegroundColor Cyan
Write-Host "Kết quả sẽ lưu vào: $outputFile`n"
Write-Host "NHỚ KIỂM TRA: HOST trong test-create-nhanvien.js đang trỏ đúng DB TEST, không phải DB thật!" -ForegroundColor Yellow

k6 run --env USERNAME=$Username --env PASSWORD=$Password --env RATE=$Rate --out json=$outputFile test-create-nhanvien.js
