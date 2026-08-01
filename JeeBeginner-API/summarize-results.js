// TỔNG HỢP KẾT QUẢ K6
// Cách chạy: node summarize-results.js
//
// FILE THẬT trên đĩa KHÔNG bị đụng vào, không xóa, không đổi tên.
// Script chỉ ĐỌC các file .json trong LoadTestResults/, rồi GOM NHÓM
// những file cùng thuật toán + cùng mức tải lại để tính chung 1 kết quả.
//
// Ví dụ 3 file trên đĩa:
//   results_aes_50vu_lan1.json
//   results_aes_50vu_lan2.json
//   results_aes_50vu_lan3.json
// -> Cả 3 được coi là CÙNG 1 NHÓM tên "aes_50vu" (chỉ khác số lần chạy)
// -> Script cộng dồn dữ liệu của cả 3 lại, tính trung bình chung.
//
// HTTPavg phải đại diện cho TOÀN BỘ 1 chu trình (1 lần Encrypt NỐI TIẾP
// 1 lần Decrypt) = Network + DB + Mã hóa (Encrypt) + Network + DB + Giải mã (Decrypt)
// => HTTPavg = Encrypt avg + Decrypt avg (CỘNG LẠI, không phải trộn chung
// 2 loại request rồi chia đôi như cách tính cũ).
// Với Hash/HashIndex/Plaintext (không có Decrypt): HTTPavg = chính avg của nó.

const fs = require('fs');
const path = require('path');

const RESULTS_DIR = path.join(__dirname, 'LoadTestResults');

//  Các hàm tính toán thống kê cơ bản 

function summarizeMetric(points) {
  if (points.length === 0) return null;
  const sorted = [...points].sort((a, b) => a - b);
  const sum = sorted.reduce((a, b) => a + b, 0);
  const p95Index = Math.min(Math.floor(sorted.length * 0.95), sorted.length - 1);

  return {
    avg: sum / sorted.length,
    min: sorted[0],
    max: sorted[sorted.length - 1],
    p95: sorted[p95Index],
  };
}

function fmt(n) {
  return n === null || n === undefined ? '-' : n.toFixed(2);
}

//  Đọc 1 file JSON thô của K6 

function docFile(filePath) {
  const lines = fs.readFileSync(filePath, 'utf8').split('\n').filter(Boolean);

  const values = {
    plaintext_duration_ms: [],
    encrypt_duration_ms: [],
    decrypt_duration_ms: [],
    hash_duration_ms: [],
    hashindex_duration_ms: [],
    plaintext_server_ms: [],
    encrypt_server_ms: [],
    decrypt_server_ms: [],
    hash_server_ms: [],
    create_duration_ms: [],
  };

  let checksTotal = 0;
  let checksFailed = 0;
  let totalRequests = 0;
  let firstTimeMs = null;
  let lastTimeMs = null;

  for (const line of lines) {
    let obj;
    try {
      obj = JSON.parse(line);
    } catch {
      continue; // dòng lỗi, bỏ qua
    }

    if (obj.type !== 'Point') continue;

    if (values[obj.metric] !== undefined && obj.data && typeof obj.data.value === 'number') {
      values[obj.metric].push(obj.data.value);
    }

    if (obj.metric === 'checks') {
      checksTotal++;
      if (obj.data.value === 0) checksFailed++;
    }
    if (obj.metric === 'http_reqs' && obj.data) {
      totalRequests++;
      const t = Date.parse(obj.data.time);
      if (!Number.isNaN(t)) {
        if (firstTimeMs === null || t < firstTimeMs) firstTimeMs = t;
        if (lastTimeMs === null || t > lastTimeMs) lastTimeMs = t;
      }
    } 
  }

  return { values, checksTotal, checksFailed, totalRequests, firstTimeMs, lastTimeMs };
}

//  Tên nhóm: bỏ "_lan1", "_lan2"... để nhận ra các file cùng 1 cấu hình 
// LƯU Ý: đây chỉ là 1 biến TẠM trong lúc chạy code, KHÔNG đổi tên file thật trên đĩa.

function tenNhom(filename) {
  return filename
    .replace(/^results_/, '')
    .replace(/\.json$/, '')
    .replace(/_lan\d+$/i, '')
    .replace(/_run\d+$/i, '');
}

// CHẠY CHÍNH

function main() {
  if (!fs.existsSync(RESULTS_DIR)) {
    console.error(`Không tìm thấy thư mục: ${RESULTS_DIR}`);
    return;
  }

  const files = fs.readdirSync(RESULTS_DIR).filter((f) => f.endsWith('.json'));
  if (files.length === 0) {
    console.error('Không có file .json nào trong LoadTestResults/');
    return;
  }

  console.log(`Tìm thấy ${files.length} file. Đang gộp nhóm...\n`);

  // BƯỚC 1: Gom dữ liệu của các file CÙNG NHÓM lại chung 1 rổ
  const groups = {};

  for (const filename of files) {
    const nhom = tenNhom(filename);
    // const { values, checksTotal, checksFailed } = docFile(path.join(RESULTS_DIR, filename));
    const { values, checksTotal, checksFailed, totalRequests, firstTimeMs, lastTimeMs } = docFile(path.join(RESULTS_DIR, filename));

    if (!groups[nhom]) {
      groups[nhom] = {
        values: {
          plaintext_duration_ms: [],
          encrypt_duration_ms: [], decrypt_duration_ms: [],
          hash_duration_ms: [], hashindex_duration_ms: [],
          plaintext_server_ms: [],
          encrypt_server_ms: [], decrypt_server_ms: [], hash_server_ms: [],
          create_duration_ms: [],
        },
        checksTotal: 0,
        checksFailed: 0,
        files: [],
        totalRequests: 0,
        totalDurationSec: 0,//cộng dồn thời lượng thực tế của TỪNG file (không lấy min-max giữa các file, vì mỗi file là 1 lần chạy 30s riêng biệt, không liên tục)
      };
    }

    for (const key of Object.keys(values)) {
      groups[nhom].values[key].push(...values[key]);
    }
    groups[nhom].checksTotal += checksTotal;
    groups[nhom].checksFailed += checksFailed;
    groups[nhom].files.push(filename);

    groups[nhom].totalRequests += totalRequests;
    if (firstTimeMs !== null && lastTimeMs !== null) {
      groups[nhom].totalDurationSec += (lastTimeMs - firstTimeMs) / 1000;
    }
  }

  // BƯỚC 2: Tính thống kê cho từng nhóm (dựa trên dữ liệu ĐÃ GỘP)
  const ketQua = Object.keys(groups).sort().map((nhom) => {
    const g = groups[nhom];
    return {
      nhom,
      soLanChay: g.files.length,
      files: g.files,
      checksTotal: g.checksTotal,
      checksFailed: g.checksFailed,
      plaintext: summarizeMetric(g.values.plaintext_duration_ms),
      encrypt: summarizeMetric(g.values.encrypt_duration_ms),
      decrypt: summarizeMetric(g.values.decrypt_duration_ms),
      hash: summarizeMetric(g.values.hash_duration_ms),
      hashIndex: summarizeMetric(g.values.hashindex_duration_ms),
      plaintextServer: summarizeMetric(g.values.plaintext_server_ms),
      encryptServer: summarizeMetric(g.values.encrypt_server_ms),
      decryptServer: summarizeMetric(g.values.decrypt_server_ms),
      hashServer: summarizeMetric(g.values.hash_server_ms),
      create: summarizeMetric(g.values.create_duration_ms),
      totalRequests: g.totalRequests,
      throughput: g.totalDurationSec > 0 ? g.totalRequests / g.totalDurationSec : null,
    };
  });

  // BƯỚC 3: In bảng ra màn hình
  // HTTPavg/HTTPp95 = tổng Encrypt + Decrypt (đại diện TOÀN BỘ 1 chu trình,
  // luôn LỚN HƠN riêng Encrypt/Hash avg - đúng công thức Network+DB+Mã hóa)
  console.log('='.repeat(150));
  console.log(
    'Nhóm'.padEnd(20) + 'SoLanChay'.padEnd(11) + 'ChecksFail'.padEnd(14) +
    'HTTPavg'.padEnd(10) + 'HTTPp95'.padEnd(10) +
    'Encrypt/Hash avg'.padEnd(18) + 'Decrypt avg'.padEnd(14) + 'EncServer avg'.padEnd(15) + 'DecServer avg'.padEnd(15) + 'Req/s'.padEnd(10) 
  );
  console.log('='.repeat(150));

  for (const r of ketQua) {
    const clientChinh = r.plaintext ?? r.encrypt ?? r.hash ?? r.hashIndex ?? r.create;
    const serverChinh = r.plaintextServer ?? r.encryptServer ?? r.hashServer;

    // HTTPavg = tổng Encrypt + Decrypt (nếu có Decrypt), còn Plaintext/Hash/
    // HashIndex chỉ có 1 chiều nên HTTPavg = chính nó
    const httpAvg = r.decrypt
      ? (clientChinh?.avg ?? 0) + (r.decrypt?.avg ?? 0)
      : clientChinh?.avg;
    const httpP95 = r.decrypt
      ? (clientChinh?.p95 ?? 0) + (r.decrypt?.p95 ?? 0) // xấp xỉ: tổng 2 p95, không phải p95 thật của tổng
      : clientChinh?.p95;

    console.log(
      r.nhom.padEnd(20) +
      String(r.soLanChay).padEnd(11) +
      `${r.checksFailed}/${r.checksTotal}`.padEnd(14) +
      fmt(httpAvg).padEnd(10) +
      fmt(httpP95).padEnd(10) +
      fmt(clientChinh?.avg).padEnd(18) +
      fmt(r.decrypt?.avg).padEnd(14) +
      fmt(serverChinh?.avg).padEnd(15) +
      fmt(r.decryptServer?.avg).padEnd(15) +
      fmt(r.throughput).padEnd(10)
    );
  }
  console.log('='.repeat(150));

  console.log('\nFile nào thuộc nhóm nào:');
  for (const r of ketQua) {
    console.log(`  ${r.nhom}: ${r.files.join(', ')}`);
  }

  // BƯỚC 4: Xuất ra CSV để mở Excel
  const csv = ['Nhom,SoLanChay,ChecksFailed,ChecksTotal,HttpAvg,HttpP95,EncryptOrHashAvg,DecryptAvg,EncryptServerAvg,DecryptServerAvg,TotalRequests,RequestsPerSec'];
  for (const r of ketQua) {
    const clientChinh = r.plaintext ?? r.encrypt ?? r.hash ?? r.hashIndex ?? r.create;
    const serverChinh = r.plaintextServer ?? r.encryptServer ?? r.hashServer;
    const httpAvg = r.decrypt ? (clientChinh?.avg ?? 0) + (r.decrypt?.avg ?? 0) : clientChinh?.avg;
    const httpP95 = r.decrypt ? (clientChinh?.p95 ?? 0) + (r.decrypt?.p95 ?? 0) : clientChinh?.p95;
    csv.push([
      r.nhom, r.soLanChay, r.checksFailed, r.checksTotal,
      fmt(httpAvg), fmt(httpP95),
      fmt(clientChinh?.avg), fmt(r.decrypt?.avg),
      fmt(serverChinh?.avg), fmt(r.decryptServer?.avg),
      r.totalRequests, fmt(r.throughput), 
    ].join(','));
  }

  const csvPath = path.join(RESULTS_DIR, 'summary.csv');
  fs.writeFileSync(csvPath, csv.join('\n'), 'utf8');
  console.log(`\nĐã lưu: ${csvPath}`);
}

main();