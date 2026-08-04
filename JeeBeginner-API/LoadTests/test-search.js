import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';
import { Trend } from 'k6/metrics';

// SCRIPT 2 - TEST TÌM KIẾM/ĐỌC (Get_DSNhanVien)
// Khác với Script 1 (test.js - test Ghi/Insert qua 5 thuật toán),
// script này bắn tải vào API tìm kiếm nhân viên thật,
// dùng CCCD xoay vòng (round-robin) từ list data lớn để tránh cache.
//
// Cách chạy:
//   k6 run --env USERNAME=huytran --env PASSWORD=xxx --env RATE=100 test-search.js
//   k6 run --env USERNAME=huytran --env PASSWORD=xxx --env RATE=200 test-search.js

const HOST = 'https://localhost:1404';
const RATE = parseInt(__ENV.RATE || '100'); // 100-200 request/giây 
const searchTrend = new Trend('search_duration_ms');
const hashTrend = new Trend('search_hash_ms');
const dbTrend = new Trend('search_db_ms');
// const USERNAME = __ENV.USERNAME;
// const PASSWORD = __ENV.PASSWORD;


export const options = {
  scenarios: {
    search_test: {
      executor: 'constant-arrival-rate', // giữ đúng tốc độ N request/giây, khác với constant-vus (số VU cố định)
      rate: RATE,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 50,   // số VU khởi tạo sẵn để đáp ứng tốc độ RATE
      maxVUs: 300,           // K6 tự tăng thêm VU nếu cần, tối đa 300
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    http_req_duration: ['p(95)<300'], // tìm kiếm DB thật (200k+ dòng) nên ngưỡng rộng hơn test mã hóa thuần
  },
};

//Đọc CCCD THẬT từ DB Test - file CSV, mỗi dòng 1 số CMND
const testData = new SharedArray('cccd-real', function () {
  const raw =  open('./Danh_sach_CMND.csv');
  return raw
    .split('\n')
    .map((line) => line.trim().replace(/^\uFEFF/, ''))
    .filter((line) => line.length > 0);
});

function pickCccd() {
  // Round-robin (xoay vòng tuần tự)
  const idx = (__VU * 997 + __ITER) % testData.length;
  return testData[idx];
}

function commonParams() {
  return {
    headers: { 'Content-Type': 'application/json' },
    insecureSkipTLSVerify: true,
  };
}

// setup() chạy ĐÚNG 1 LẦN trước khi test bắt đầu - dùng để đăng nhập lấy JWT token
// export function setup() {
//   if (!USERNAME || !PASSWORD) {
//     throw new Error('Thiếu USERNAME/PASSWORD. Chạy lại với --env USERNAME=... --env PASSWORD=...');
//   }

//   const res = http.post(
//     `${HOST}/api/authorization/login`,
//     JSON.stringify({ username: USERNAME, password: PASSWORD }),
//     { headers: { 'Content-Type': 'application/json' }, insecureSkipTLSVerify: true }
//   );

//   if (res.status !== 200) {
//     throw new Error(`Đăng nhập thất bại (status ${res.status}): ${res.body}`);
//   }

//   const token = JSON.parse(res.body).token;
//   return { token };
// }


  const WARMUP_CALLS = 30;

  export function setup() {
    const params = commonParams();
    for (let i = 0; i < WARMUP_CALLS; i++) {
      const idx = i % testData.length;
      const cccd = testData[idx];
      const url = `${HOST}/api/nhanvienmanagement/search/test` +
        `?filter.keys=keyword&filter.vals=${encodeURIComponent(cccd)}`;
      http.get(url, params);
    }
    sleep(1); // đảm bảo warm-up và đo thật không lẫn vào cùng 1 nhịp
  }

// default() nhận lại kết quả từ setup() qua tham số "data"
export default function () {
  const cccd = pickCccd();

  // Xây query string đúng định dạng thật (không phải JSON body):
  // page, record, sortOrder, sortField, filter.keys, filter.vals
  const url = `${HOST}/api/nhanvienmanagement/search/test` +
    `?filter.keys=keyword&filter.vals=${encodeURIComponent(cccd)}`;

  const res = http.get(url, commonParams());
  let body = null;
  try { body = JSON.parse(res.body); } catch {}

  check(res, {
    'search status 200': (r) => r.status === 200,
  });
  
  searchTrend.add(res.timings.duration);
  if (body && body.data) {
    if (typeof body.data.HashMs === 'number') hashTrend.add(body.data.HashMs);
    if (typeof body.data.DbMs === 'number') dbTrend.add(body.data.DbMs);
  }
  sleep(0.1); // nghỉ ngắn hơn Script 1, vì mục tiêu là bắn NHIỀU request/giây liên tục
}

// CÁCH CHẠY:
//   k6 run --env USERNAME=<tài khoản> --env PASSWORD=<mật khẩu> --env RATE=100 --out json=../LoadTestResults/results_search_100rps.json test-search.js
//   k6 run --env USERNAME=<tài khoản> --env PASSWORD=<mật khẩu> --env RATE=200 --out json=../LoadTestResults/results_search_200rps.json test-search.js
