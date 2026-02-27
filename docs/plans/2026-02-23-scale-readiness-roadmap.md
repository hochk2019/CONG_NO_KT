# Scale Readiness Roadmap (2026-02-23)

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Đây là tài liệu kế hoạch triển khai theo từng giai đoạn.
> Tracker vận hành chính: `task.md` + beads (`bd`).
> Tài liệu run thực tế hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`.

## 1) Mục tiêu (dễ hiểu)

Mục tiêu của kế hoạch này là giúp hệ thống:
- Chịu tải cao hơn mà vẫn ổn định.
- Ít bị chậm khi nhiều người dùng truy cập cùng lúc.
- Dễ mở rộng thêm tài nguyên khi lượng dữ liệu và người dùng tăng.
- Có số liệu rõ ràng để ra quyết định, thay vì tối ưu theo cảm tính.

## 2) Phạm vi triển khai

Kế hoạch gồm 5 chặng chính, có thứ tự ưu tiên:
1. Đo tải thực tế + đặt chuẩn chất lượng (k6 + SLO).
2. Cache Redis cho API đọc nhiều (dashboard/reports/risk).
3. Tách tác vụ nặng sang queue/worker.
4. Đọc dữ liệu từ read-replica cho luồng báo cáo.
5. Autoscaling + guardrail vận hành (scale an toàn, rollback nhanh).

## 3) Bead mapping (tracker chính thức)

- Epic: `cng-oiw` - Scale readiness roadmap.
- `cng-oiw.1` - Baseline tải + SLO bằng k6.
- `cng-oiw.2` - Redis cache cho read-heavy endpoints.
- `cng-oiw.3` - Queue worker cho tác vụ nặng.
- `cng-oiw.4` - Read-replica + tách read/write path.
- `cng-oiw.5` - Autoscaling + guardrail vận hành.

## 4) Kế hoạch triển khai theo chặng

### Chặng A - Baseline tải + SLO (`cng-oiw.1`)

**Mục đích:** Biết “hệ thống đang chịu được bao nhiêu” trước khi sửa kiến trúc.

**Làm gì:**
- Viết kịch bản k6 cho các luồng chính: đăng nhập, dashboard, reports, receipts.
- Chạy test theo 3 mức tải (thấp/trung bình/cao).
- Chốt SLO: p95 latency, error rate, throughput mục tiêu.

**Xong khi:**
- Có báo cáo baseline lưu vào docs.
- Có ngưỡng SLO chính thức để dùng cho các chặng sau.

### Chặng B - Redis cache (`cng-oiw.2`)

**Mục đích:** Giảm tải DB cho các API đọc nhiều.

**Làm gì:**
- Thêm Redis vào runtime.
- Chọn endpoint áp dụng cache trước: dashboard/reports/risk bootstrap.
- Thiết kế TTL + quy tắc invalidation khi dữ liệu thay đổi.
- Theo dõi hit-rate và độ trễ sau khi bật cache.

**Xong khi:**
- Có ma trận “endpoint nào cache, TTL bao nhiêu, invalidation ra sao”.
- p95 giảm rõ rệt ở các endpoint đọc nhiều.

### Chặng C - Queue/Worker (`cng-oiw.3`)

**Mục đích:** Không để API request trực tiếp gánh việc nặng.

**Làm gì:**
- Liệt kê tác vụ nặng/chạy lâu để chuyển sang queue.
- Triển khai worker riêng cho background jobs.
- Thiết lập retry, dead-letter, idempotency.
- Bổ sung metrics queue depth + job latency.

**Xong khi:**
- API response ổn định hơn dưới tải.
- Tác vụ nền có observability rõ (queue depth, fail/retry).

### Chặng D - Read replica (`cng-oiw.4`)

**Mục đích:** Giảm tải DB primary cho truy vấn đọc lớn.

**Làm gì:**
- Tách read/write policy theo endpoint.
- Route các truy vấn report/dashboard sang replica khi phù hợp.
- Thiết lập guard cho dữ liệu cần nhất quán mạnh (vẫn đọc primary).
- Viết failover runbook khi replica lỗi.

**Xong khi:**
- Primary giảm tải đọc.
- Có hướng dẫn rõ endpoint nào đọc replica/primary.

### Chặng E - Autoscaling + guardrail (`cng-oiw.5`)

**Mục đích:** Hệ thống tự scale có kiểm soát, tránh scale “vô tội vạ”.

**Làm gì:**
- Thiết lập ngưỡng scale up/down cho API và worker.
- Gắn alert theo SLO và saturation.
- Viết runbook rollback/cắt tải khi có sự cố.
- Chạy game-day scenario.

**Xong khi:**
- Có policy autoscaling chính thức.
- Có checklist sự cố + rollback đã test.

## 5) Kiểm soát rủi ro

- Không bật đồng loạt tất cả thay đổi cùng lúc.
- Mỗi chặng đều có “before/after metrics” để chứng minh hiệu quả.
- Có cờ bật/tắt (feature flag/config) cho thành phần mới.
- Luôn có phương án rollback rõ ràng trước khi rollout.

## 6) Tiêu chí nghiệm thu tổng thể

Kế hoạch được xem là hoàn tất khi:
- 5 bead con (`cng-oiw.1` đến `cng-oiw.5`) đều đóng.
- Có báo cáo before/after cho độ trễ, lỗi, thông lượng.
- Có runbook vận hành và rollback cho Redis/queue/replica/autoscaling.
- `task.md` và tài liệu liên quan được cập nhật đầy đủ.

