import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';
import { Trend } from 'k6/metrics';

// Cách chạy:
//   k6 run --env USERNAME=huytran --env PASSWORD=xxx --env RATE=50 test-create-nhanvien.js

const HOST = 'https://localhost:1404'; 
const RATE = parseInt(__ENV.RATE || '50');
const USERNAME = __ENV.USERNAME;
const PASSWORD = __ENV.PASSWORD;
const RUN_TAG = String(Date.now() % 900000).padStart(6, '0');
const createTrend = new Trend('create_duration_ms');
const dbCheckTrend = new Trend('create_db_check_ms');
const encryptTrend = new Trend('create_encrypt_ms');
const insertTrend = new Trend('create_insert_ms');


export const options = {
  scenarios: {
    create_test: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'], // ghi có thể trùng MaNV/CCCD -> cho phép fail rate cao hơn search
    checks: ['rate>0.90'],
  },
};

// Đọc file JSON dạng field rời rạc (id, fieldName, value) - GIỐNG CẤU TRÚC test-data-200.json hiện có
// Tự động GHÉP các field cùng "vòng" (round) lại thành 1 object nhân viên hoàn chỉnh
const employees = new SharedArray('employees-built', function () {
  const raw = JSON.parse(open('./test-data-200.json')); // ĐỔI TÊN FILE nếu bạn dùng file khác

  const byField = {};
  raw.forEach((r) => {
    if (!byField[r.fieldName]) byField[r.fieldName] = [];
    byField[r.fieldName].push(r.value);
  });

  // Số nhân viên tạo được = số dòng ít nhất trong 4 field cần thiết
  const need = ['MaNhanVien', 'Holot', 'Ten', 'CMND'];
  const missing = need.filter((f) => !byField[f] || byField[f].length === 0);
  if (missing.length > 0) {
    throw new Error(`File JSON thiếu field bắt buộc: ${missing.join(', ')}`);
  }
  const count = Math.min(...need.map((f) => byField[f].length));

  const list = [];
  for (let i = 0; i < count; i++) {
    // MaNhanVien hiện có dạng "NVxxxx" (6 ký tự) - controller yêu cầu ^NV\d{1,10}$
    // -> giữ nguyên tiền tố NV, đảm bảo phần số hợp lệ
    // let maNV = byField['MaNhanVien'][i];
    // if (!/^NV\d{1,10}$/i.test(maNV)) {
    //   maNV = 'NV' + (100000 + i); // fallback tự sinh mã hợp lệ nếu giá trị gốc không đúng định dạng
    // }

    // // CMND trong file gốc là 12 số (đã đúng chuẩn CCCD 12 số theo ValidateNhanVien)
    // const cccd = byField['CMND'][i];
    const maNV = 'NV' + RUN_TAG + String(i % 10000).padStart(4, '0');
    const cccd = '02' + RUN_TAG + String(i % 10000).padStart(4, '0');

    list.push({
      // MaNV: maNV,
      HoTen: `${byField['Holot'][i]} ${byField['Ten'][i]}`,
      // CCCD: cccd,
      SDT: byField['Mobile'] ? byField['Mobile'][i] : '',
      Email: '',
      DiaChi: '',
      PhongBan: '',
      ChucVu: '',
    });
  }
  return list;
});

function commonHeaders(token) {
  return {
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    insecureSkipTLSVerify: true,
  };
}

// setup() chạy 1 lần duy nhất - login lấy token
export function setup() {
  if (!USERNAME || !PASSWORD) {
    throw new Error('Thiếu USERNAME/PASSWORD. Chạy lại với --env USERNAME=... --env PASSWORD=...');
  }
  const res = http.post(
    `${HOST}/api/authorization/login`,
    JSON.stringify({ Username: USERNAME, Password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' }, insecureSkipTLSVerify: true }
  );
  if (res.status !== 200) {
    throw new Error(`Đăng nhập thất bại (status ${res.status}): ${res.body}`);
  }
  const token = JSON.parse(res.body).token;
  console.log(`Đã login OK. Sẽ tạo tối đa ${employees.length} nhân viên (tùy RATE x 30s).`);
  return { token };
}

export default function (data) {
  const idx = (__VU * 997 + __ITER) % employees.length;
  const base = employees[idx]; // chỉ lấy Họ Tên/SDT, không lấy MaNV/CCCD nữa

  // __VU (tối đa 100) + __ITER luôn là tổ hợp DUY NHẤT trong 1 lần chạy K6,
  // không phụ thuộc kích thước mảng employees -> không bao giờ trùng
  const uniqueSuffix = String(__VU).padStart(3, '0') + String(__ITER).padStart(5, '0'); // 8 số, KHÔNG cắt bớt
  const emp = {
    ...base,
    MaNV: 'NV' + RUN_TAG.slice(-2) + uniqueSuffix, // 2 + 8 = 10 số, vẫn hợp lệ (≤10)
    CCCD: '0' + RUN_TAG.slice(0, 3) + uniqueSuffix,      // 1 + 3 + 8 = 12 số 
  };

  const res = http.post(
    `${HOST}/api/nhanvienmanagement/CreateNhanVien`,
    JSON.stringify(emp),
    commonHeaders(data.token)
  );

  let body = null;
  try { body = JSON.parse(res.body); } catch {}

  check(res, {
    'create status 200': (r) => r.status === 200,
    'create thực sự thành công (không trùng)': () => body && body.status === 1,
  });
  createTrend.add(res.timings.duration);
  if (body && body.data) {
    if (typeof body.data.DbCheckMs === 'number') dbCheckTrend.add(body.data.DbCheckMs);
    if (typeof body.data.EncryptMs === 'number') encryptTrend.add(body.data.EncryptMs);
    if (typeof body.data.InsertMs === 'number') insertTrend.add(body.data.InsertMs);
  }

  sleep(0.1);
}

// CÁCH CHẠY:
//   k6 run --env USERNAME=<tk> --env PASSWORD=<mk> --env RATE=50 --out json=../LoadTestResults/results_create_50rps.json test-create-nhanvien.js