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
    http_req_duration: [],
    encrypt_duration_ms: [],
    decrypt_duration_ms: [],
    hash_duration_ms: [],
    hashindex_duration_ms: [],
    encrypt_server_ms: [],
    decrypt_server_ms: [],
    hash_server_ms: [],
  };

  let checksTotal = 0;
  let checksFailed = 0;

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
  }

  return { values, checksTotal, checksFailed };
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
  const groups = {}; // { "aes_50vu": { values: {...gộp...}, checksTotal, checksFailed, files: [...] } }

  for (const filename of files) {
    const nhom = tenNhom(filename);
    const { values, checksTotal, checksFailed } = docFile(path.join(RESULTS_DIR, filename));

    if (!groups[nhom]) {
      groups[nhom] = {
        values: {
          http_req_duration: [], encrypt_duration_ms: [], decrypt_duration_ms: [],
          hash_duration_ms: [], hashindex_duration_ms: [],
          encrypt_server_ms: [], decrypt_server_ms: [], hash_server_ms: [],
        },
        checksTotal: 0,
        checksFailed: 0,
        files: [],
      };
    }

    // Nối dữ liệu thô của file này vào rổ chung của nhóm
    for (const key of Object.keys(values)) {
      groups[nhom].values[key].push(...values[key]);
    }
    groups[nhom].checksTotal += checksTotal;
    groups[nhom].checksFailed += checksFailed;
    groups[nhom].files.push(filename);
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
      http: summarizeMetric(g.values.http_req_duration),
      encrypt: summarizeMetric(g.values.encrypt_duration_ms),
      decrypt: summarizeMetric(g.values.decrypt_duration_ms),
      hash: summarizeMetric(g.values.hash_duration_ms),
      hashIndex: summarizeMetric(g.values.hashindex_duration_ms),
      encryptServer: summarizeMetric(g.values.encrypt_server_ms),
      decryptServer: summarizeMetric(g.values.decrypt_server_ms),
      hashServer: summarizeMetric(g.values.hash_server_ms),
    };
  });

  // BƯỚC 3: In bảng ra màn hình
  console.log('='.repeat(140));
  console.log(
    'Nhóm'.padEnd(20) + 'SoLanChay'.padEnd(12) + 'ChecksFail'.padEnd(14) +
    'HTTPavg'.padEnd(10) + 'HTTPp95'.padEnd(10) +
    'Encrypt/Hash avg'.padEnd(18) + 'Decrypt avg'.padEnd(14) + 'Server avg'.padEnd(12)
  );
  console.log('='.repeat(140));

  for (const r of ketQua) {
    const clientChinh = r.encrypt ?? r.hash ?? r.hashIndex;
    const serverChinh = r.encryptServer ?? r.hashServer;

    console.log(
      r.nhom.padEnd(20) +
      String(r.soLanChay).padEnd(12) +
      `${r.checksFailed}/${r.checksTotal}`.padEnd(14) +
      fmt(r.http?.avg).padEnd(10) +
      fmt(r.http?.p95).padEnd(10) +
      fmt(clientChinh?.avg).padEnd(18) +
      fmt(r.decrypt?.avg).padEnd(14) +
      fmt(serverChinh?.avg).padEnd(12)
    );
  }
  console.log('='.repeat(140));

  console.log('\nFile nào thuộc nhóm nào:');
  for (const r of ketQua) {
    console.log(`  ${r.nhom}: ${r.files.join(', ')}`);
  }

  // BƯỚC 4: Xuất ra CSV để mở Excel
  const csv = ['Nhom,SoLanChay,ChecksFailed,ChecksTotal,HttpAvg,HttpP95,EncryptOrHashAvg,DecryptAvg,ServerAvg'];
  for (const r of ketQua) {
    const clientChinh = r.encrypt ?? r.hash ?? r.hashIndex;
    const serverChinh = r.encryptServer ?? r.hashServer;
    csv.push([
      r.nhom, r.soLanChay, r.checksFailed, r.checksTotal,
      fmt(r.http?.avg), fmt(r.http?.p95),
      fmt(clientChinh?.avg), fmt(r.decrypt?.avg), fmt(serverChinh?.avg),
    ].join(','));
  }

  const csvPath = path.join(RESULTS_DIR, 'summary.csv');
  fs.writeFileSync(csvPath, csv.join('\n'), 'utf8');
  console.log(`\nĐã lưu: ${csvPath}`);
}

main();