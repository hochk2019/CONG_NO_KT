# Opus Remaining Execution Plan (2026-02-12)

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Tài liệu này là kế hoạch/thực thi theo thời điểm viết, có thể chứa giả định cũ.
> Nguồn vận hành hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `task.md`.


## Update 2026-02-12 (Phase 51)
- Mục AI risk scoring/dự báo trễ hạn trước đây để out-of-scope trong plan này đã được triển khai ở `task.md` Phase 51.
- Trạng thái mới: đã có pipeline huấn luyện model theo dữ liệu lịch sử + seasonality, scheduler retrain, model registry/runs và endpoint quản trị train/activate/list.

## Goal
Hoàn tất các hạng mục Opus còn lại có tính kỹ thuật/vận hành: DB partition, Docker containerization, và nâng cấp Ops để quản trị runtime Docker; đồng thời cập nhật đầy đủ trạng thái tài liệu + beads.

## Scope thực hiện
- DB: partition `congno.audit_logs` theo tháng + migration an toàn dữ liệu.
- Deployment: Dockerfile backend/frontend + `docker-compose.yml` + `.dockerignore` + hướng dẫn vận hành.
- Ops: hỗ trợ chế độ runtime Docker bên cạnh Windows Service (status/start/stop/restart).
- Docs/Tracking: cập nhật `task.md`, `Opus_4.6_review.md`, `docs/OPS_ADMIN_CONSOLE.md`, `DEPLOYMENT_GUIDE_WEB_WINDOWS.md`, beads.

## Out of scope trong vòng này
- AI risk scoring / dự báo trễ hạn:
  - Lý do: là roadmap tính năng mới (cần dữ liệu lịch sử + mô hình + hiệu chỉnh nghiệp vụ), không phải hardening/vận hành.

## Tasks
- [x] Task 1: Đồng bộ tracking (beads + task.md) trước khi code.
  - Verify: Có epic/task beads mới cho vòng này, `task.md` có Phase mới.
- [x] Task 2: Tạo migration partition cho `audit_logs` + giữ tương thích retention hiện tại.
  - Verify: Migration mới chạy idempotent; giữ được dữ liệu cũ; không vỡ test backend.
- [x] Task 3: Thêm containerization artifacts (backend/frontend/compose/env mẫu).
  - Verify: `docker compose config` hợp lệ; docs mô tả cách chạy local/prod.
- [x] Task 4: Nâng cấp Ops Agent/Console hỗ trợ Docker runtime.
  - Verify: Ops có thể lấy trạng thái và start/stop/restart theo mode Docker; test Ops pass.
- [x] Task 5: Đồng bộ docs và đóng vòng.
  - Verify: `Opus_4.6_review.md` phản ánh trạng thái mới + lý do mục chưa làm; beads/task cập nhật nhất quán.

## Done when
- [x] `DB partition + Docker + Ops Docker mode` đã có code + test/build pass.
- [x] Tài liệu và trạng thái quản trị công việc nhất quán giữa `Opus_4.6_review.md`, `task.md`, beads.

