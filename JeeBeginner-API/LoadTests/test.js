import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { SharedArray } from 'k6/data';

// CẤU HÌNH: chọn mức tải (LOAD_LEVEL) và thuật toán (ALGO) khi chạy
//   k6 run --env LOAD_LEVEL=50 --env ALGO=aes   test.js
//   k6 run --env LOAD_LEVEL=100 --env ALGO=rsa  test.js
//   k6 run --env LOAD_LEVEL=200 --env ALGO=fpe  test.js
//   k6 run --env LOAD_LEVEL=50  --env ALGO=hash test.js

const LOAD_LEVEL = __ENV.LOAD_LEVEL || '50';
const ALGO = (__ENV.ALGO || 'aes').toLowerCase(); // plaintext / aes / rsa / fpe / hash 

export const options = {
  scenarios: {
    load_test: {
      executor: 'constant-vus',
      vus: parseInt(LOAD_LEVEL),
      duration: '30s',
    },
  },
  // THRESHOLDS - tiêu chuẩn để K6 tự đánh giá PASS/FAIL cho cả đợt test.
  // Nếu vi phạm bất kỳ ngưỡng nào, K6 báo "✗ THRESHOLDS" ở cuối kết quả
  // và thoát chương trình với exit code khác 0 (hữu ích để tự động hóa
  // kiểm tra pass/fail sau này, ví dụ trong CI/CD).
  thresholds: {
    // Ngưỡng chung cho MỌI request, bất kể thuật toán nào 
    http_req_failed: ['rate<0.01'],      // dưới 1% request bị lỗi (status không phải 2xx)
    checks: ['rate>0.99'],                // trên 99% các check (status 200, round-trip đúng...) phải đạt
    http_req_duration: ['p(95)<200'],     // 95% request phải xong dưới 200ms (dựa theo số liệu thực tế đã đo, kể cả RSA ở 200 VUs vẫn thường dưới ngưỡng này)

    // Ngưỡng riêng cho từng loại thao tác (đo từ CLIENT, round-trip) 
    // Dựa theo số liệu thực tế đã đo được trước đó cho từng thuật toán
    'plaintext_duration_ms': ['p(95)<150'],
    'encrypt_duration_ms': ['p(95)<150'],
    'decrypt_duration_ms': ['p(95)<150'],
    'hash_duration_ms': ['p(95)<100'],

    // Ngưỡng riêng cho thời gian THUẦN thuật toán (đo từ SERVER, bọc sát) 
    // Ngưỡng này rất chặt vì bản chất thuật toán chạy cực nhanh (dưới 1ms hầu hết trường hợp)
    'encrypt_server_ms': ['p(95)<50'],
    'decrypt_server_ms': ['p(95)<50'],
    'hash_server_ms': ['p(95)<50'],
  },
};

const HOST = 'https://localhost:1404';

// Custom metrics - đo từ phía CLIENT (round-trip, gồm cả network/serialize)
const plaintextTrend = new Trend('plaintext_duration_ms');
const encryptTrend = new Trend('encrypt_duration_ms');
const decryptTrend = new Trend('decrypt_duration_ms');
const hashTrend = new Trend('hash_duration_ms');

// Custom metrics - đo từ phía SERVER (Stopwatch bọc sát quanh đúng dòng gọi
// thuật toán trong ProcessField(), KHÔNG tính network/serialize/overhead HTTP)
const plaintextServerTrend = new Trend('plaintext_server_ms');
const encryptServerTrend = new Trend('encrypt_server_ms');
const decryptServerTrend = new Trend('decrypt_server_ms');
const hashServerTrend = new Trend('hash_server_ms');

// BỘ DỮ LIỆU GIẢ LẬP CỐ ĐỊNH, đọc từ file test-data-200.json
const testData = new SharedArray('test-data-200', function () {
  return JSON.parse(open('./test-data-200.json'));
});

function pickRecord() {
  const idx = (__VU * 1000 + __ITER) % testData.length;
  return { fieldName: testData[idx].fieldName, value: testData[idx].value };
}

function commonParams() {
  return {
    headers: { 'Content-Type': 'application/json' },
    insecureSkipTLSVerify: true,
  };
}

// Lấy ServerExecutionTimeMs mà server trả về trong response (Stopwatch bọc sát thuật toán)
function getServerTimeMs(res) {
  try {
    return JSON.parse(res.body).data.ServerExecutionTimeMs;
  } catch {
    return null;
  }
}
const WARMUP_CALLS = 30;
 
export function setup() {
  const params = commonParams();
  for (let i = 0; i < WARMUP_CALLS; i++) {
    const idx = i % testData.length;
    const { fieldName, value } = testData[idx];
 
    if (ALGO === 'plaintext') {
      http.post(`${HOST}/api/encryptiontest/plaintext/field`, JSON.stringify({ fieldName, value }), params);
      continue;
    }
    if (ALGO === 'hash') {
      http.post(`${HOST}/api/encryptiontest/hmacsha256/field/hash`, JSON.stringify({ fieldName, value }), params);
      continue;
    }
   
 
    // aes / rsa / fpe: warm-up cả encrypt lẫn decrypt
    const encRes = http.post(`${HOST}/api/encryptiontest/${ALGO}/field/encrypt`, JSON.stringify({ fieldName, value }), params);
    try {
      const encryptedValue = JSON.parse(encRes.body).data.OutputValue;
      http.post(`${HOST}/api/encryptiontest/${ALGO}/field/decrypt`, JSON.stringify({ fieldName, value: encryptedValue }), params);
    } catch {
      // encrypt lỗi lúc warm-up thì bỏ qua, không phải lúc đo thật nên không sao
    }
  }
  // sleep nhỏ để chắc chắn warm-up và đo thật không lẫn vào cùng 1 nhịp
  sleep(1);
}
export default function () {
  const { fieldName, value } = pickRecord();
  const params = commonParams();

  // TRƯỜNG HỢP PLAINTEXT
  if (ALGO === 'plaintext') {
    const url = `${HOST}/api/encryptiontest/plaintext/field`;
    const res = http.post(url, JSON.stringify({ fieldName, value }), params);
    check(res, { 'plaintext status 200': (r) => r.status === 200 });
    plaintextTrend.add(res.timings.duration, { algorithm: 'plaintext', field: fieldName });
    const serverMs = getServerTimeMs(res);
    if (serverMs !== null) plaintextServerTrend.add(serverMs, { algorithm: 'plaintext', field: fieldName });
    sleep(1);
    return;
  }

  // TRƯỜNG HỢP HASH (HMAC-SHA256): CHỈ 1 CHIỀU
  if (ALGO === 'hash') {
    const url = `${HOST}/api/encryptiontest/hmacsha256/field/hash`;
    const res = http.post(url, JSON.stringify({ fieldName, value }), params);

    check(res, { 'hash status 200': (r) => r.status === 200 });
    hashTrend.add(res.timings.duration, { algorithm: 'hash', field: fieldName });

    const serverMs = getServerTimeMs(res);
    if (serverMs !== null) hashServerTrend.add(serverMs, { algorithm: 'hash', field: fieldName });

    sleep(1);
    return;
  }

  // AES / RSA / FPE: có cả 2 chiều Encrypt + Decrypt
  const encryptUrl = `${HOST}/api/encryptiontest/${ALGO}/field/encrypt`;
  const encryptRes = http.post(encryptUrl, JSON.stringify({ fieldName, value }), params);

  const encryptOk = check(encryptRes, {
    'encrypt status 200': (r) => r.status === 200,
  });
  encryptTrend.add(encryptRes.timings.duration, { algorithm: ALGO, field: fieldName });

  const encryptServerMs = getServerTimeMs(encryptRes);
  if (encryptServerMs !== null) encryptServerTrend.add(encryptServerMs, { algorithm: ALGO, field: fieldName });

  if (!encryptOk) {
    sleep(1);
    return;
  }

  // Response bị bọc thêm 1 lớp { status, data } bởi JsonResultCommon.ThanhCong()
  const encryptedValue = JSON.parse(encryptRes.body).data.OutputValue;

  const decryptUrl = `${HOST}/api/encryptiontest/${ALGO}/field/decrypt`;
  const decryptRes = http.post(decryptUrl, JSON.stringify({ fieldName, value: encryptedValue }), params);

  check(decryptRes, {
    'decrypt status 200': (r) => r.status === 200,
    'decrypt round-trip khớp giá trị gốc': (r) => {
      try {
        return JSON.parse(r.body).data.OutputValue === value;
      } catch {
        return false;
      }
    },
  });
  decryptTrend.add(decryptRes.timings.duration, { algorithm: ALGO, field: fieldName });

  const decryptServerMs = getServerTimeMs(decryptRes);
  if (decryptServerMs !== null) decryptServerTrend.add(decryptServerMs, { algorithm: ALGO, field: fieldName });

  sleep(1);
}

// CÁCH CHẠY (đủ 5 loại: plaintext, aes, rsa, fpe, hash):
//   k6 run --env LOAD_LEVEL=50  --env ALGO=hash --out json=results_hash_50vu.json  test.js
// (tương tự cho các thuật toán khác)
//
// Kết quả sẽ có thêm dòng "THRESHOLDS" ở cuối, đánh dấu ✓ (đạt) hoặc ✗ (không đạt)
// cho từng ngưỡng đã cấu hình ở trên.
