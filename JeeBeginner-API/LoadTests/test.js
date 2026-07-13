import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

// CẤU HÌNH: chọn mức tải (LOAD_LEVEL) và thuật toán (ALGO) khi chạy
//   k6 run --env LOAD_LEVEL=50 --env ALGO=aes  test.js
//   k6 run --env LOAD_LEVEL=100 --env ALGO=rsa test.js
//   k6 run --env LOAD_LEVEL=200 --env ALGO=fpe test.js
// 

const LOAD_LEVEL = __ENV.LOAD_LEVEL || '50';
const ALGO = (__ENV.ALGO || 'aes').toLowerCase(); // plaintext / aes / rsa / fpe

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

// Custom metrics - tách riêng latency của Encrypt và Decrypt để so sánh
const encryptTrend = new Trend('encrypt_duration_ms');
const decryptTrend = new Trend('decrypt_duration_ms');

// 4 loại field đại diện đủ tình huống: 2 field số (dùng FpeDigits)
// và 2 field chữ/hỗn hợp (dùng FpeAlphaNumeric) - khớp đúng logic
// IsDigitField() trong EncryptionTestController.cs
const FIELD_GENERATORS = {
  SDT: () => '09' + Math.floor(10000000 + Math.random() * 89999999), // 10 số
  CCCD: () => String(Math.floor(100000000000 + Math.random() * 899999999999)), // 12 số
  HoTen: () => {
    const ho = ['Nguyen', 'Tran', 'Le', 'Pham', 'Hoang'];
    const ten = ['Van A', 'Thi B', 'Minh C', 'Quoc D', 'Thu E'];
    return ho[Math.floor(Math.random() * ho.length)] + ' ' + ten[Math.floor(Math.random() * ten.length)];
  },
  DiaChi: () => {
    const so = Math.floor(1 + Math.random() * 999);
    const duong = ['Le Loi', 'Nguyen Trai', 'Tran Hung Dao', 'Hai Ba Trung'];
    return `${so} ${duong[Math.floor(Math.random() * duong.length)]}, Q.1, TP.HCM`;
  },
};

const FIELD_NAMES = Object.keys(FIELD_GENERATORS); // ['SDT', 'CCCD', 'HoTen', 'DiaChi']

function randomField() {
  const fieldName = FIELD_NAMES[Math.floor(Math.random() * FIELD_NAMES.length)];
  const value = FIELD_GENERATORS[fieldName]();
  return { fieldName, value };
}

function commonParams() {
  return {
    headers: { 'Content-Type': 'application/json' },
    insecureSkipTLSVerify: true, // bỏ qua lỗi self-signed cert của localhost https
  };
}

export default function () {
  const { fieldName, value } = randomField();
  const params = commonParams();

  // TRƯỜNG HỢP PLAINTEXT: không có khái niệm encrypt/decrypt, chỉ có 1 endpoint

  if (ALGO === 'plaintext') {
    const url = `${HOST}/api/encryptiontest/plaintext/field`;
    const res = http.post(url, JSON.stringify({ fieldName, value }), params);
    check(res, { 'plaintext status 200': (r) => r.status === 200 });
    sleep(1);
    return;
  }

  // BƯỚC 1: ENCRYPT - mã hóa dữ liệu, đo thời gian riêng
  const encryptUrl = `${HOST}/api/encryptiontest/${ALGO}/field/encrypt`;
  const encryptRes = http.post(encryptUrl, JSON.stringify({ fieldName, value }), params);

  const encryptOk = check(encryptRes, {
    'encrypt status 200': (r) => r.status === 200,
  });
  encryptTrend.add(encryptRes.timings.duration, { algorithm: ALGO, field: fieldName });

  if (!encryptOk) {
    sleep(1);
    return; // không có bản mã hợp lệ thì không thể test decrypt tiếp
  }

  // Lấy bản mã (outputValue) vừa mã hóa được, dùng làm đầu vào cho bước decrypt
  const encryptedValue = JSON.parse(encryptRes.body).data.OutputValue;

  // BƯỚC 2: DECRYPT - giải mã lại đúng giá trị vừa mã hóa, đo thời gian riêng
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

// CÁCH CHẠY:
//   k6 run --env LOAD_LEVEL=50  --env ALGO=aes --out json=results_aes_50vu.json  test.js
//   k6 run --env LOAD_LEVEL=100 --env ALGO=aes --out json=results_aes_100vu.json test.js
//   k6 run --env LOAD_LEVEL=200 --env ALGO=aes --out json=results_aes_200vu.json test.js
// Đổi ALGO=rsa / fpe / plaintext để chạy các thuật toán còn lại.
//
// Kết quả in ra terminal sẽ có riêng 2 dòng:
//   encrypt_duration_ms.......: avg=...
//   decrypt_duration_ms.......: avg=...
// → so sánh được ngay thời gian Encrypt vs Decrypt của từng thuật toán.
