@echo off
setlocal

echo ============================================================
echo   CHAY TOAN BO (Stopwatch) + (K6 Load Test)
echo   Truoc khi chay file nay, dam bao JeeBeginner-API dang chay
echo   (dotnet run --launch-profile "JeeBeginner") o mot terminal khac
echo ============================================================
echo.
pause

REM ============================================================
REM  Do thoi gian bang System.Diagnostics.Stopwatch
REM ============================================================
echo.
echo ===  PerformanceTestClient ===
cd PerformanceTestClient

for %%A in (plaintext aes rsa fpe hash) do (
    echo.
    echo ---: %%A ---
    dotnet run -- %%A 20
)

cd ..

REM ============================================================
REM  Gia lap tai bang K6
REM ============================================================
echo.
echo === K6 Load Test ===
cd JeeBeginner-API\LoadTests

for %%A in (plaintext aes rsa fpe hash) do (
    for %%L in (50 100 200) do (
        echo.
        echo ---  %%A - %%L VUs ---
        k6 run --env LOAD_LEVEL=%%L --env ALGO=%%A --out json=../LoadTestResults/results_%%A_%%Lvu.json test.js
    )
)

cd ..

REM ============================================================
REM Tong hop ket qua
REM ============================================================
echo.
echo === Tong hop ket qua (summarize-results.js) ===
node summarize-results.js

cd ..

echo.
echo ============================================================
echo   HOAN TAT! Xem ket qua tai:
echo   - JeeBeginner-API\LoadTestResults\summary.csv  
echo   - PerformanceTestClient\Logs\performance_log.csv 
echo ============================================================
pause
