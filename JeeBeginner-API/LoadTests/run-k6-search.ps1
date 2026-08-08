# run-k6-search.ps1 - Chạy K6 cho test-search.js (Tìm kiếm nhân viên)
# và TỰ ĐỘNG đánh số lần chạy (_lan1, _lan2, _lan3...) giống run-k6.ps1 / run-k6-create.ps1.
#
# Cách dùng:
#   .\run-k6-search.ps1 -Rate 50
#   .\run-k6-search.ps1 -Rate 50    <- chạy lại lần nữa, tự động ra "_lan2"
#   .\run-k6-search.ps1 -Rate 100
#   .\run-k6-search.ps1 -Rate 200

param(
    [Parameter(Mandatory=$true)][int]$Rate
)

$resultsDir = "../LoadTestResults"

if (!(Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir | Out-Null
}

# Tìm các file đã có cho đúng cấu hình này (rate), lấy số "lan" lớn nhất đang có
$pattern = "results_search_${Rate}rps_lan*.json"
$existingFiles = Get-ChildItem -Path $resultsDir -Filter $pattern -ErrorAction SilentlyContinue

$maxLan = 0
foreach ($f in $existingFiles) {
    if ($f.Name -match "_lan(\d+)\.json$") {
        $n = [int]$matches[1]
        if ($n -gt $maxLan) { $maxLan = $n }
    }
}

$nextLan = $maxLan + 1
$outputFile = "$resultsDir/results_search_${Rate}rps_lan${nextLan}.json"
$dashboardFile = "$resultsDir/dashboard_search_${Rate}rps_lan${nextLan}.html"
Write-Host "=== Chạy K6: Tìm kiếm nhân viên (Search), Rate=$Rate req/s, Lần thứ $nextLan ===" -ForegroundColor Cyan
Write-Host "Kết quả sẽ lưu vào: $outputFile`n"
Write-Host "Dashboard biểu đồ sẽ lưu vào: $dashboardFile`n"

k6 run --env RATE=$Rate --out json=$outputFile --out web-dashboard=export=$dashboardFile test-search.js