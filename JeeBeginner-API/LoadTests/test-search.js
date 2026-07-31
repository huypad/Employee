import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';

// SCRIPT 2 - TEST TÌM KIẾM/ĐỌC (Get_DSNhanVien)
// Khác với Script 1 (test.js - test Ghi/Insert qua 5 thuật toán),
// script này bắn tải vào API tìm kiếm nhân viên thật,
// dùng CCCD xoay vòng (round-robin) từ list data lớn để tránh cache.
//
// Cách chạy:
//   k6 run --env USERNAME=huytran --env PASSWORD=xxx --env RATE=100 test-search.js
//   k6 run --env USERNAME=huytran --env PASSWORD=xxx --env RATE=200 test-search.js

const HOST = 'https://localhost:1404';
const RATE = parseInt(__ENV.RATE || '100'); // 100-200 request/giây theo yêu cầu anh Huy
const USERNAME = __ENV.USERNAME;
const PASSWORD = __ENV.PASSWORD;

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

// Đọc bộ dữ liệu cố định, lấy các dòng CMND (12 số) để dùng làm từ khóa tìm kiếm
const testData = new SharedArray('test-data-search', function () {
  const all = JSON.parse(open('./test-data-200.json'));
  return all.filter((x) => x.fieldName === 'CMND'); // chỉ lấy các dòng CMND để tìm kiếm
});

function pickCccd() {
  // Round-robin (xoay vòng tuần tự), KHÔNG random - tránh bắn lặp 1-2 giá trị khiến DB cache
  const idx = (__VU * 1000 + __ITER) % testData.length;
  return testData[idx].value;
}

// setup() chạy ĐÚNG 1 LẦN trước khi test bắt đầu - dùng để đăng nhập lấy JWT token
export function setup() {
  if (!USERNAME || !PASSWORD) {
    throw new Error('Thiếu USERNAME/PASSWORD. Chạy lại với --env USERNAME=... --env PASSWORD=...');
  }

  const res = http.post(
    `${HOST}/api/authorization/login`,
    JSON.stringify({ username: USERNAME, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' }, insecureSkipTLSVerify: true }
  );

  if (res.status !== 200) {
    throw new Error(`Đăng nhập thất bại (status ${res.status}): ${res.body}`);
  }

  const token = JSON.parse(res.body).token;
  return { token };
}

// default() nhận lại kết quả từ setup() qua tham số "data"
export default function (data) {
  const cccd = pickCccd();

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${data.token}`,
    },
    insecureSkipTLSVerify: true,
  };

  // Xây query string đúng định dạng thật (không phải JSON body):
  // page, record, sortOrder, sortField, filter.keys, filter.vals
  const url = `${HOST}/api/nhanvienmanagement/Get_DSNhanVien` +
    `?page=1&record=10&sortOrder=&sortField=` +
    `&filter.keys=keyword&filter.vals=${encodeURIComponent(cccd)}`;

  const res = http.get(url, params);

  check(res, {
    'search status 200': (r) => r.status === 200,
  });

  sleep(0.1); // nghỉ ngắn hơn Script 1, vì mục tiêu là bắn NHIỀU request/giây liên tục
}

// CÁCH CHẠY:
//   k6 run --env USERNAME=<tài khoản> --env PASSWORD=<mật khẩu> --env RATE=100 --out json=../LoadTestResults/results_search_100rps.json test-search.js
//   k6 run --env USERNAME=<tài khoản> --env PASSWORD=<mật khẩu> --env RATE=200 --out json=../LoadTestResults/results_search_200rps.json test-search.js
