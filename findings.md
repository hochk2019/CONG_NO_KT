# Findings - 2026-02-23

## Tracker / Task State
- `task.md`: không còn checkbox mở (`- [ ]`).
- Bead còn mở trước khi xử lý: `cng-fwg` (`in_progress`) dù nội dung Phase 60 đã hoàn thành.
- Sau khi verify lại test/build, bead `cng-fwg` đã được đóng; `bd ready --json` trả về `[]`.

## Code Quality
- Trước khi sửa:
  - `eslint` fail tại:
    - `src/frontend/src/hooks/useTheme.ts` (`react-hooks/set-state-in-effect`).
    - `src/frontend/src/pages/RiskAlertsPage.tsx` (`react-hooks/exhaustive-deps`).
- Sau khi sửa:
  - `npm --prefix src/frontend run lint` pass.
  - `npm --prefix src/frontend run test -- --run` pass (`90/90`).
  - `npm --prefix src/frontend run build` pass.
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` pass (`116/116`).
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` pass (`41/41`).

## Residual Notes
- Vẫn còn module lớn hơn 800 dòng:
  - `src/frontend/src/pages/ReportsPage.tsx` (~965 lines),
  - `src/frontend/src/pages/receipts/ReceiptListSection.tsx` (~838 lines),
  - `src/frontend/src/api/openapi.d.ts` là file generated.
- Các mục Zalo OA thật trong `task.md` đang ở trạng thái “tạm đóng” do thiếu tài khoản OA/credential thực tế.

## Scale Planning Update (2026-02-23)
- Đã tạo roadmap mở rộng chính thức trên bead:
  - Epic `cng-oiw` (`in_progress`).
  - Tasks: `cng-oiw.1` (k6 baseline), `.2` (Redis cache), `.3` (Queue/Worker), `.4` (Read replica), `.5` (Autoscaling).
- Đã tạo kế hoạch chi tiết: `docs/plans/2026-02-23-scale-readiness-roadmap.md`.
- Đã đồng bộ `task.md` bằng `Phase 67` để tracking triển khai từng chặng.

## Scale Execution Update (2026-02-23)
- `cng-oiw.3`: đã triển khai maintenance queue + worker:
  - queue service: `IMaintenanceJobQueue` / `MaintenanceJobQueue`
  - worker: `MaintenanceJobWorkerHostedService`
  - async endpoints:
    - `POST /admin/health/reconcile-balances/queue`
    - `POST /admin/health/run-retention/queue`
    - `GET /admin/maintenance/jobs`
    - `GET /admin/maintenance/jobs/{jobId}`
- Observability cho queue đã có metric:
  - `congno_maintenance_queue_depth`
  - `congno_maintenance_queue_delay_ms`
  - `congno_maintenance_job_duration_ms`
  - `congno_maintenance_job_total`
- Tài liệu kỹ thuật đã bổ sung cho `cng-oiw.4` và `cng-oiw.5`:
  - `docs/performance/READ_REPLICA_ROUTING.md`
  - `docs/performance/AUTOSCALING_GUARDRAILS.md`
  - `docs/performance/QUEUE_WORKER_OPERATIONS.md`
- Build + unit test backend pass sau thay đổi:
  - `dotnet build src/backend/Api/CongNoGolden.Api.csproj`
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` (`127/127`)
- Full verification pass:
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` (`41/41`)
  - `npm --prefix src/frontend run lint`
  - `npm --prefix src/frontend run test -- --run` (`90/90`)
  - `npm --prefix src/frontend run build`
  - `npm --prefix src/frontend run build:budget`
- Bead tracker:
  - `cng-oiw.1` -> `cng-oiw.5`: `CLOSED`
  - Epic `cng-oiw`: `CLOSED`
  - `bd ready --json`: `[]`

## Opus V3 Validation Update (2026-02-24)
- `Opus_review_v3.md` đã bổ sung `Codex Validation Addendum` để phân loại lại các claim theo codebase thực tế.
- Nhóm **OUTDATED (đã có sẵn)**:
  - Risk AI explainability (`aiFactors`) và recommendation (`aiRecommendation`).
  - Dashboard executive summary + KPI MoM.
  - Risk Alerts tab layout (`Overview/Config/History`).
  - Notification route `/notifications` + nút `Xem tất cả`.
- Nhóm **PARTIAL**:
  - Reminder escalation intelligence: đã có mở rộng recipients (owner/supervisor) nhưng chưa có state-machine escalation theo phản hồi.
- Nhóm **CONFIRMED GAP**:
  - Global search đa thực thể.
  - Onboarding tour/coachmarks.
  - Import drag-and-drop UX.
  - Print layout + scheduled report delivery.
  - Risk delta alert theo thời gian.
  - Dashboard widget reorder/customization.
- Verification mới trong phiên 2026-02-24:
  - `dotnet build` pass.
  - `dotnet test` Unit `127/127`, Integration `42/42`.
  - `npm lint` pass, `npm test -- --run` `92/92`, `npm build` pass.
- Bead mục tiêu: `cng-rlx.1` -> `.5` + epic `cng-rlx` đủ điều kiện đóng sau khi sync tracker.

## Global Search Update (2026-02-25)
- `cng-los.2` đã hoàn tất phần test còn lại:
  - Root cause test fail: `TransactionFilters` render info-tip (`i`) trong label tìm kiếm, nên `getByLabelText` theo chuỗi exact không khớp.
  - Fix: đổi assertion sang `getByRole('textbox', { name: /Tìm chứng từ \(PT \/ HD \/ TH\)/i })`.
- Verification:
  - Backend integration: `GlobalSearchServiceIntegrationTests` pass (`2/2`).
  - Frontend targeted RTL: `app-shell.test.tsx` + `customers-modules.test.tsx` pass (`11/11`).

## Phase 69 Completion Update (2026-02-26)
- `cng-los.3` (onboarding + import drag-drop) đã có đủ implementation + test trong codebase:
  - `src/frontend/src/layouts/AppShell.tsx` + `src/frontend/src/layouts/__tests__/app-shell.test.tsx`
  - `src/frontend/src/pages/imports/ImportBatchSection.tsx` + `src/frontend/src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx`
- `cng-los.4` hoàn tất:
  - Dashboard widget preferences API + UI + test:
    - `src/backend/Api/Endpoints/DashboardEndpoints.cs`
    - `src/backend/Infrastructure/Services/DashboardService.Preferences.cs`
    - `src/backend/Tests.Integration/DashboardPreferencesTests.cs`
    - `src/frontend/src/pages/DashboardPage.tsx`
    - `src/frontend/src/pages/dashboard/DashboardWidgetSettings.tsx`
    - `src/frontend/src/pages/__tests__/dashboard-page.test.tsx`
  - Reports print UX:
    - `src/frontend/src/pages/ReportsPage.tsx` (`window.print`)
    - `src/frontend/src/pages/reports/reports.css` (`@media print`)
    - `src/frontend/src/pages/reports/__tests__/reports-modules.test.tsx`
- Full verification hiện tại pass:
  - Backend: build pass, Unit `134/134`, Integration `52/52`.
  - Frontend: lint pass, test `99/99`, build pass.
- Phát hiện thêm trong lúc verify:
  - Rule lint `react-hooks/set-state-in-effect` chặn full-suite ở 2 file cũ (`AppShell.tsx`, `CustomersPage.tsx`).
  - Đã refactor an toàn để bỏ setState đồng bộ trong effect; không ảnh hưởng behavior đã có test.

## cng-d3e.2 Update (2026-02-26)
- Root cause blocker: file mới `ReminderService.ResponseState.cs` import sai namespace cho extension `EnsureUser`.
  - Sai: `CongNoGolden.Application.Common`
  - Đúng: `CongNoGolden.Infrastructure.Services.Common`
- Sau khi sửa import, backend compile/test reminder flow pass:
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~Reminder"` => `6/6`.
- Frontend targeted test pass:
  - `npm --prefix src/frontend run test -- risk-alerts-page-tabs` => `1/1`.
- Bead state:
  - `cng-d3e.2` đã `CLOSED`.
  - Remaining: `cng-d3e.1` (`in_progress`), `cng-d3e.3` (`open`).

## cng-d3e.3 Update (2026-02-26)
- Đã bổ sung coverage cho 2 transition còn thiếu của response-aware escalation:
  - `Run_WhenDisputed_EscalatesWithDisputedReason`
  - `Run_WhenEscalationLocked_KeepsEscalationLevel`
- Verification targeted pass:
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~ReminderEscalationPolicyTests"` => `6/6`.
  - `npm --prefix src/frontend run test -- risk-alerts-page-tabs` => `1/1`.
- `Opus_review_v3.md` đã sync claim residual:
  - reminder escalation chuyển từ `PARTIAL` sang `OUTDATED (đã có 2026-02-26)` với evidence file-level + test.
- Trạng thái bead sau cập nhật:
  - `cng-d3e.1`: `CLOSED`
  - `cng-d3e.2`: `CLOSED`
  - `cng-d3e.3`: `CLOSED`
  - epic `cng-d3e`: `CLOSED`

## cng-9y1 Update (2026-02-26)
- Context re-check:
  - Dropzone drag-drop cho Import đã có sẵn từ trước; gap còn lại nằm ở UX validation rõ ràng cho file invalid.
- Đã hoàn tất cải tiến nhỏ nhưng có tác động UX:
  - Thêm trạng thái CSS `upload-dropzone--error` để phản hồi visual khi file không hợp lệ.
  - Bổ sung test coverage drag/drop + input cho validation:
    - reject non-`.xlsx`.
    - reject `.xlsx` vượt `20MB` qua input.
    - reject `.xlsx` vượt `20MB` qua drag-drop.
  - Khẳng định invalid file không gọi `uploadImport`.
- Verification trong phiên:
  - `npm run test -- --run src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx` => pass (`4/4`).
  - `npm run lint` => pass.
