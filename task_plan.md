# Task Plan - 2026-02-23

## Goal
Tự động xử lý các vấn đề chất lượng còn tồn tại sau lần rà soát gần nhất, cập nhật trạng thái công việc và đồng bộ tài liệu vận hành.

## Tasks
- [x] Áp dụng skill workflow (`planning-with-files`, `plan-writing`, `lint-and-validate`, `verification-before-completion`).
- [x] Xác thực trạng thái tracker (`task.md` + beads) để tìm việc còn mở.
- [x] Sửa lỗi chất lượng mã đang fail (`eslint` frontend).
- [x] Chạy lại verification đầy đủ cho backend/frontend.
- [x] Đóng bead mở còn lại nếu đã có đủ bằng chứng pass.
- [x] Cập nhật tài liệu liên quan (`task.md`, `QA_REPORT.md`, `progress.md`, `task_plan.md`, `findings.md`).

## Done When
- [x] Không còn bead `ready/in_progress` cho phạm vi đang xử lý.
- [x] Frontend lint pass.
- [x] Backend + frontend test/build pass.
- [x] Tài liệu trạng thái phản ánh đúng kết quả mới nhất.

---

## Goal (Update 2026-02-23 - Scale Planning)
Lập kế hoạch mở rộng hệ thống theo lộ trình có thể triển khai dần, có tracker bead + tài liệu cho người không chuyên theo dõi.

## Tasks (Update 2026-02-23 - Scale Planning)
- [x] Tạo epic và task con cho scale readiness trong bead.
- [x] Viết execution plan trong `docs/plans` với ngôn ngữ dễ hiểu.
- [x] Đồng bộ `task.md` để phản ánh roadmap mới và các chặng triển khai kế tiếp.

## Done When (Update 2026-02-23 - Scale Planning)
- [x] Có epic `cng-oiw` và task `cng-oiw.1` -> `cng-oiw.5`.
- [x] Có tài liệu kế hoạch: `docs/plans/2026-02-23-scale-readiness-roadmap.md`.
- [x] Có Phase 67 trong `task.md` để theo dõi tiến độ triển khai.

---

## Goal (Update 2026-02-23 - Scale Execution)
Triển khai toàn bộ roadmap scale readiness (`cng-oiw.1` -> `cng-oiw.5`) với code + verification + tài liệu vận hành.

## Tasks (Update 2026-02-23 - Scale Execution)
- [x] Hoàn tất baseline load test assets + SLO docs (`cng-oiw.1`).
- [x] Triển khai Redis read-model cache + invalidation (`cng-oiw.2`).
- [x] Triển khai queue/worker cho maintenance jobs + endpoint theo dõi (`cng-oiw.3`).
- [x] Hoàn tất read-replica routing cho read-heavy services (`cng-oiw.4`).
- [x] Hoàn tất tài liệu autoscaling + guardrails + rollback/game-day (`cng-oiw.5`).
- [x] Chạy verification backend/frontend trước khi cập nhật tracker.

## Done When (Update 2026-02-23 - Scale Execution)
- [x] Các bead con `cng-oiw.1` -> `cng-oiw.5` đều đóng.
- [x] Epic `cng-oiw` đóng.
- [x] Test/build/lint pass theo evidence mới nhất.
- [x] `task.md`, `findings.md`, `progress.md`, runbook và docs hiệu năng được cập nhật.

---

## Goal (Update 2026-02-24 - Opus V3 Validation Retry)
Hoàn tất vòng xác thực lại `Opus_review_v3.md`, đồng bộ tracker và đóng bead `cng-rlx*` với evidence test mới.

## Tasks (Update 2026-02-24 - Opus V3 Validation Retry)
- [x] Xác thực lại claim V3 theo code hiện tại và ghi matrix `OUTDATED/PARTIAL/CONFIRMED GAP`.
- [x] Chốt phạm vi thực thi: chỉ xử lý tài liệu/tracker cho các hạng mục đã có sẵn trong code.
- [x] Chạy lại verification backend/frontend trong cùng phiên để làm evidence đóng bead.
- [x] Cập nhật `task.md`, `task_plan.md`, `findings.md`, `progress.md`, `opus-review-v3-remediation.md`.
- [x] Đóng bead `cng-rlx.1` -> `cng-rlx.5` và epic `cng-rlx`.

## Done When (Update 2026-02-24 - Opus V3 Validation Retry)
- [x] `Opus_review_v3.md` có validation addendum với bằng chứng file-level.
- [x] Verification suite pass (`dotnet build/test`, `npm lint/test/build`).
- [x] Tracker và beads đồng bộ trạng thái hoàn tất.

---

## Goal (Update 2026-02-26 - cng-d3e.2)
Hoàn tất bead `cng-d3e.2`: refactor reminder escalation theo response state và chốt bằng test targeted.

## Tasks (Update 2026-02-26 - cng-d3e.2)
- [x] Xử lý compile blocker `EnsureUser` trong `ReminderService.ResponseState.cs`.
- [x] Chạy lại backend reminder integration tests.
- [x] Chạy frontend test cho Risk Alerts tabs sau cập nhật payload settings.
- [x] Đóng bead `cng-d3e.2` và đồng bộ tracker files.

## Done When (Update 2026-02-26 - cng-d3e.2)
- [x] `dotnet test ... --filter "FullyQualifiedName~Reminder"` pass.
- [x] `npm --prefix src/frontend run test -- risk-alerts-page-tabs` pass.
- [x] `bd show cng-d3e.2` trạng thái `CLOSED`.

---

## Goal (Update 2026-02-26 - cng-d3e.3)
Hoàn tất residual closure của Opus V3 cho reminder escalation: bổ sung test transition còn thiếu và đồng bộ lại review/tracker để đóng epic `cng-d3e`.

## Tasks (Update 2026-02-26 - cng-d3e.3)
- [x] Bổ sung integration tests cho transition `DISPUTED` và `ESCALATION_LOCKED`.
- [x] Chạy lại targeted backend tests cho `ReminderEscalationPolicyTests`.
- [x] Chạy lại frontend targeted test `risk-alerts-page-tabs`.
- [x] Cập nhật `Opus_review_v3.md` (claim reminder escalation từ `PARTIAL` -> đã có).
- [x] Đồng bộ `task.md`, `task_plan.md`, `findings.md`, `progress.md`.
- [x] Đóng beads `cng-d3e.1`, `cng-d3e.3` và epic `cng-d3e`.

## Done When (Update 2026-02-26 - cng-d3e.3)
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~ReminderEscalationPolicyTests"` pass (`6/6`).
- [x] `npm --prefix src/frontend run test -- risk-alerts-page-tabs` pass (`1/1`).
- [x] `Opus_review_v3.md` phản ánh đúng trạng thái reminder escalation.
- [x] `bd show cng-d3e` hiển thị epic và tasks con ở trạng thái `CLOSED`.

---

## Goal (Update 2026-02-26 - cng-9y1)
Hoàn tất UX validation cho Import dropzone theo bead `cng-9y1`: báo lỗi sớm cho file sai định dạng/kích thước, bổ sung test và đồng bộ tracker.

## Tasks (Update 2026-02-26 - cng-9y1)
- [x] Thêm trạng thái visual lỗi cho dropzone (`upload-dropzone--error`).
- [x] Bổ sung test cho các case invalid:
  - [x] reject non-`.xlsx` khi drag-drop.
  - [x] reject file `.xlsx` vượt `20MB` qua input.
  - [x] reject file `.xlsx` vượt `20MB` qua drag-drop.
- [x] Chạy verification phù hợp cho frontend thay đổi.
- [x] Cập nhật `task.md`, `progress.md`, `findings.md` và đóng bead `cng-9y1`.

## Done When (Update 2026-02-26 - cng-9y1)
- [x] `npm run test -- --run src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx` pass (`4/4`).
- [x] `npm run lint` pass.
- [x] `bd show cng-9y1` ở trạng thái `CLOSED`.
