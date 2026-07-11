import http from 'k6/http';
import { check, sleep } from 'k6';

// ============================================================
// CẤU HÌNH KỊCH BẢN TẢI
// Đổi biến LOAD_LEVEL khi chạy để test từng mức: 50 / 100 / 200
// Cách chạy (xem chi tiết ở cuối file):
//   k6 run --env LOAD_LEVEL=50  load-test.js
//   k6 run --env LOAD_LEVEL=100 load-test.js
//   k6 run --env LOAD_LEVEL=200 load-test.js
// ============================================================

const LOAD_LEVEL = __ENV.LOAD_LEVEL || '50'; // mặc định 50 nếu không truyền vào

export const options = {
  scenarios: {
    load_test: {
      executor: 'constant-vus', // giữ đúng N VUs (virtual users) gọi liên tục trong suốt thời gian test
      vus: parseInt(LOAD_LEVEL), // số request đồng thời: 50 / 100 / 200
      duration: '30s',           // chạy trong 30 giây - có thể chỉnh lại tùy nhu cầu
    },
  },
};

// ============================================================
// TODO: THAY URL NÀY BẰNG ENDPOINT THẬT CỦA CV2 KHI CÓ
// Hiện tại tạm dùng httpbin.org để test luồng chạy K6 trước
// ============================================================
const BASE_URL = 'https://httpbin.org/post'; // ← đổi thành https://localhost:1404/api/encryption/aes ... khi có endpoint thật

// TODO: THAY BODY NÀY THEO ĐÚNG FORMAT JSON MÀ CV2 YÊU CẦU
// (field tên gì, ví dụ "data" hay "plainText" - hỏi lại CV2)
function buildPayload() {
  return JSON.stringify({
    data: 'sample-data-for-load-test-' + Math.random().toString(36).substring(7),
    // algorithm: 'AES', // bỏ comment nếu API cần chỉ định thuật toán trong body
  });
}

export default function () {
  const payload = buildPayload();
  const params = {
    headers: {
      'Content-Type': 'application/json',
      // 'Authorization': 'Bearer <token>', // bỏ comment nếu endpoint cần JWT
    },
  };

  const res = http.post(BASE_URL, payload, params);

  console.log(`Status: ${res.status}`); // thêm dòng này để xem status thật

  check(res, {
    'status is 200': (r) => r.status === 200,
  });

  // K6 tự động ghi lại các chỉ số quan trọng cho MỌI request, không cần code thêm:
  // - http_req_duration: latency (thời gian từ lúc gửi đến lúc nhận response, tính bằng ms)
  // - http_reqs: tổng số request đã gửi (dùng tính throughput = http_reqs / duration)
  // Log thô của các chỉ số này sẽ được xuất ra file khi chạy với --out (xem hướng dẫn cuối file)

  sleep(1); // nghỉ 1 giây giữa các lần gọi của mỗi VU, tránh spam quá nhanh không thực tế
}

// ============================================================
// CÁCH CHẠY VÀ XUẤT FILE LOG THÔ:
//
// Chạy từng mức tải riêng biệt, xuất ra file JSON riêng để dễ so sánh:
//
//   k6 run --env LOAD_LEVEL=50  --out json=results_50vu.json  load-test.js
//   k6 run --env LOAD_LEVEL=100 --out json=results_100vu.json load-test.js
//   k6 run --env LOAD_LEVEL=200 --out json=results_200vu.json load-test.js
//
// File JSON xuất ra sẽ chứa RAW LOG của từng request (không chỉ summary),
// gồm timestamp, latency (http_req_duration), status... - đúng yêu cầu
// "ghi nhận toàn bộ file log thô" của CV4.
//
// Muốn xem summary nhanh ngay trên terminal (không cần mở file) thì chạy
// không cần --out, K6 tự in bảng thống kê p95, avg, max... sau khi chạy xong.
// ============================================================