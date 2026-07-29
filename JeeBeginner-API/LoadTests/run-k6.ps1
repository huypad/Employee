# run-k6.ps1 - Chạy K6 và TỰ ĐỘNG đánh số lần chạy (_lan1, _lan2, _lan3...)
# Người dùng không cần tự nhớ/tự gõ số lần - script tự tìm số tiếp theo.
#
# Cách dùng (gõ y hệt mỗi lần, không cần đổi gì):
#   .\run-k6.ps1 -Algo aes -Load 50
#   .\run-k6.ps1 -Algo aes -Load 50      <- chạy lại lần nữa, tự động ra "_lan2"
#   .\run-k6.ps1 -Algo rsa -Load 100
#
# Ai pull code về, chạy đúng lệnh này là tự động hoạt động đúng,
# không cần biết/nhớ gì về "_lan1, _lan2" cả.

param(
    [Parameter(Mandatory=$true)][string]$Algo,
    [Parameter(Mandatory=$true)][int]$Load
)

$resultsDir = "../LoadTestResults"

if (!(Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir | Out-Null
}

# Tìm các file đã có cho đúng cấu hình này (algo + load), lấy số "lan" lớn nhất đang có
$pattern = "results_${Algo}_${Load}vu_lan*.json"
$existingFiles = Get-ChildItem -Path $resultsDir -Filter $pattern -ErrorAction SilentlyContinue

$maxLan = 0
foreach ($f in $existingFiles) {
    if ($f.Name -match "_lan(\d+)\.json$") {
        $n = [int]$matches[1]
        if ($n -gt $maxLan) { $maxLan = $n }
    }
}

$nextLan = $maxLan + 1
$outputFile = "$resultsDir/results_${Algo}_${Load}vu_lan${nextLan}.json"

Write-Host "=== Chạy K6: Algorithm=$Algo, LoadLevel=$Load, Lần thứ $nextLan ===" -ForegroundColor Cyan
Write-Host "Kết quả sẽ lưu vào: $outputFile`n"

k6 run --env LOAD_LEVEL=$Load --env ALGO=$Algo --out json=$outputFile test.js