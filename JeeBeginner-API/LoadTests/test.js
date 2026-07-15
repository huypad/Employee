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
};

const HOST = 'https://localhost:1404';

// Custom metrics - tách riêng latency của từng loại thao tác để so sánh
const encryptTrend = new Trend('encrypt_duration_ms');
const decryptTrend = new Trend('decrypt_duration_ms');
const hashTrend = new Trend('hash_duration_ms');

const testData = new SharedArray('test-data-200', function () {
  return JSON.parse(open('./test-data-200.json'));
});

function pickRecord() {
  // Chọn xoay vòng tuần tự (round-robin) theo __VU (số thứ tự VU) và __ITER (số lần lặp)
  // thay vì random - đảm bảo tái lập được y hệt thứ tự dữ liệu qua nhiều lần chạy khác nhau
  const idx = (__VU * 997 + __ITER) % testData.length;
  return { fieldName: testData[idx].fieldName, value: testData[idx].value };
}

function commonParams() {
  return {
    headers: { 'Content-Type': 'application/json' },
    insecureSkipTLSVerify: true,
  };
}

export default function () {
  const { fieldName, value } = pickRecord();
  const params = commonParams();

  // TRƯỜNG HỢP PLAINTEXT: không mã hóa, chỉ 1 endpoint
  if (ALGO === 'plaintext') {
    const url = `${HOST}/api/encryptiontest/plaintext/field`;
    const res = http.post(url, JSON.stringify({ fieldName, value }), params);
    check(res, { 'plaintext status 200': (r) => r.status === 200 });
    sleep(1);
    return;
  }

  // TRƯỜNG HỢP HASH (HMAC-SHA256): CHỈ 1 CHIỀU, không có decrypt/round-trip
  // vì hash không thể đảo ngược lại giá trị gốc theo đúng bản chất thuật toán
  if (ALGO === 'hash') {
    const url = `${HOST}/api/encryptiontest/hmacsha256/field/hash`;
    const res = http.post(url, JSON.stringify({ fieldName, value }), params);

    check(res, { 'hash status 200': (r) => r.status === 200 });
    hashTrend.add(res.timings.duration, { algorithm: 'hash', field: fieldName });

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

  sleep(1);
}

// CÁCH CHẠY (đủ 5 loại: plaintext, aes, rsa, fpe, hash):
//   k6 run --env LOAD_LEVEL=50  --env ALGO=hash --out json=results_hash_50vu.json  test.js
//   k6 run --env LOAD_LEVEL=100 --env ALGO=hash --out json=results_hash_100vu.json test.js
//   k6 run --env LOAD_LEVEL=200 --env ALGO=hash --out json=results_hash_200vu.json test.js
// (tương tự cho plaintext / aes / rsa / fpe)
//
// Kết quả in ra sẽ có dòng riêng:
//   hash_duration_ms.......: avg=...
// để so sánh Hash với Encrypt/Decrypt của AES/RSA/FPE.
