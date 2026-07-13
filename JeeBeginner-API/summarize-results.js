// Script tổng hợp kết quả K6 - đọc toàn bộ file results_*.json trong LoadTestResults/
// Cách chạy: node summarize-results.js
// (Yêu cầu: đã cài Node.js - nếu chưa có, dùng bản Node bạn từng cài lúc setup Angular)

const fs = require('fs');
const path = require('path');

const RESULTS_DIR = path.join(__dirname, 'LoadTestResults');

function percentile(sortedArr, p) {
  if (sortedArr.length === 0) return 0;
  const idx = Math.floor(sortedArr.length * p);
  return sortedArr[Math.min(idx, sortedArr.length - 1)];
}

function summarizeMetric(points) {
  if (points.length === 0) return null;
  const sorted = [...points].sort((a, b) => a - b);
  const sum = sorted.reduce((a, b) => a + b, 0);
  return {
    count: sorted.length,
    avg: sum / sorted.length,
    min: sorted[0],
    max: sorted[sorted.length - 1],
    p95: percentile(sorted, 0.95),
  };
}

function analyzeFile(filePath) {
  const lines = fs.readFileSync(filePath, 'utf8').split('\n').filter(Boolean);

  const metrics = {
    http_req_duration: [],
    encrypt_duration_ms: [],
    decrypt_duration_ms: [],
  };

  let checksTotal = 0;
  let checksFailed = 0;

  for (const line of lines) {
    let obj;
    try {
      obj = JSON.parse(line);
    } catch {
      continue;
    }

    if (obj.type !== 'Point') continue;

    const metricName = obj.metric;
    if (metrics[metricName] !== undefined && obj.data && typeof obj.data.value === 'number') {
      metrics[metricName].push(obj.data.value);
    }

    if (metricName === 'checks') {
      checksTotal++;
      if (obj.data.value === 0) checksFailed++;
    }
  }

  return {
    file: path.basename(filePath),
    httpReqDuration: summarizeMetric(metrics.http_req_duration),
    encryptDuration: summarizeMetric(metrics.encrypt_duration_ms),
    decryptDuration: summarizeMetric(metrics.decrypt_duration_ms),
    checksTotal,
    checksFailed,
  };
}

function fmt(n) {
  return n === null || n === undefined ? '-' : n.toFixed(2);
}

function main() {
  if (!fs.existsSync(RESULTS_DIR)) {
    console.error(`Không tìm thấy thư mục: ${RESULTS_DIR}`);
    console.error('Chạy script này từ trong thư mục JeeBeginner-API (ngang hàng với LoadTestResults/)');
    return;
  }

  const files = fs.readdirSync(RESULTS_DIR).filter((f) => f.endsWith('.json'));

  if (files.length === 0) {
    console.error('Không tìm thấy file .json nào trong LoadTestResults/');
    return;
  }

  console.log(`Tìm thấy ${files.length} file. Đang phân tích...\n`);

  const results = files.map((f) => analyzeFile(path.join(RESULTS_DIR, f)));

  // In bảng tổng hợp
  console.log('='.repeat(120));
  console.log(
    'File'.padEnd(35) +
    'Checks Fail'.padEnd(14) +
    'HTTP avg(ms)'.padEnd(14) +
    'HTTP p95(ms)'.padEnd(14) +
    'Encrypt avg'.padEnd(14) +
    'Decrypt avg'.padEnd(14)
  );
  console.log('='.repeat(120));

  for (const r of results) {
    console.log(
      r.file.padEnd(35) +
      `${r.checksFailed}/${r.checksTotal}`.padEnd(14) +
      fmt(r.httpReqDuration?.avg).padEnd(14) +
      fmt(r.httpReqDuration?.p95).padEnd(14) +
      fmt(r.encryptDuration?.avg).padEnd(14) +
      fmt(r.decryptDuration?.avg).padEnd(14)
    );
  }
  console.log('='.repeat(120));

  // Xuất ra file CSV để mở bằng Excel dễ nhìn hơn
  const csvLines = ['File,ChecksFailed,ChecksTotal,HttpAvg,HttpP95,HttpMax,EncryptAvg,EncryptP95,DecryptAvg,DecryptP95'];
  for (const r of results) {
    csvLines.push([
      r.file,
      r.checksFailed,
      r.checksTotal,
      fmt(r.httpReqDuration?.avg),
      fmt(r.httpReqDuration?.p95),
      fmt(r.httpReqDuration?.max),
      fmt(r.encryptDuration?.avg),
      fmt(r.encryptDuration?.p95),
      fmt(r.decryptDuration?.avg),
      fmt(r.decryptDuration?.p95),
    ].join(','));
  }

  const csvPath = path.join(RESULTS_DIR, 'summary.csv');
  fs.writeFileSync(csvPath, csvLines.join('\n'), 'utf8');
  console.log(`\nĐã xuất bảng tổng hợp ra: ${csvPath}`);
}

main();