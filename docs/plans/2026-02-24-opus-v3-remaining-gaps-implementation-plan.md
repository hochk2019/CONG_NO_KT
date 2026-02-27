# Opus V3 Remaining Gaps - Implementation Plan (2026-02-24)

> [!IMPORTANT]
> Kế hoạch này chỉ triển khai các hạng mục **thực sự còn thiếu** sau khi đối chiếu `Opus_review_v3.md` với codebase hiện tại.
> Các nhận định đã lỗi thời (đã có sẵn) sẽ **không làm lại**.
>
> Tracker chuẩn: `task.md` + beads `cng-los*`.

## 1) Kết luận đối chiếu Opus V3

### 1.1 Outdated (đã có, không cần làm lại)
- Risk AI explainability + recommendation.
- Dashboard executive summary + KPI MoM.
- Risk tabs (Overview/Config/History).
- Notification center + route “Xem tất cả”.

### 1.2 Partial (cần nâng cấp thêm)
- Reminder automation: đã có gửi cho owner + supervisor, nhưng chưa có escalation theo lịch sử phản hồi/lần nhắc.
- Reports: đã có export PDF/XLSX, nhưng chưa có print-layout web rõ ràng và chưa có scheduled delivery.

### 1.3 Confirmed gaps (cần triển khai)
- Global search đa thực thể.
- Onboarding tour/coachmarks.
- Import drag-and-drop UX.
- Scheduled report delivery.
- Risk score delta alerts theo thời gian.
- Dashboard widget customization/reorder.

---

## 2) Mục tiêu triển khai

1. Bổ sung đúng các gap còn thiếu, không chạm lại phần đã ổn định.
2. Mỗi hạng mục có API contract + test backend/frontend tương ứng.
3. Giữ module gọn (<800 LOC nếu có thể), tách file khi logic lớn.
4. Mọi thay đổi code đều đi kèm test và cập nhật tracker.

---

## 3) Mapping với beads

- Epic: `cng-los`
- `cng-los.1`: Backend (scheduled reports + risk delta alerts + reminder escalation)
- `cng-los.2`: Global search (backend + frontend command surface)
- `cng-los.3`: Frontend UX (onboarding + import drag/drop)
- `cng-los.4`: Frontend UX (dashboard widget customization + report print)
- `cng-los.5`: Verification + tracker sync

---

## 4) Kế hoạch chi tiết theo workstream

## Workstream A - Backend foundation (`cng-los.1`, ưu tiên P1)

### A1. Thiết kế schema + migration cho 3 khối mới
- [ ] Tạo migration `027_report_delivery_schedule.sql` cho lịch gửi báo cáo.
  - Bảng đề xuất: `congno.report_delivery_schedules` (user_id, report_kind, report_format, cron, timezone, recipients, filter_payload, enabled, last_run_at, next_run_at, created_at, updated_at).
  - Bảng đề xuất: `congno.report_delivery_runs` (schedule_id, status, started_at, finished_at, error_detail, artifact_meta).
- [ ] Tạo migration `028_risk_score_snapshots.sql` cho snapshot và delta alerts.
  - Bảng đề xuất: `congno.risk_score_snapshots` (customer_tax_code, as_of_date, score, signal, model_version, created_at).
  - Bảng đề xuất: `congno.risk_delta_alerts` (customer_tax_code, prev_score, curr_score, delta, threshold, status, detected_at).
- [ ] Tạo migration `029_reminder_escalation_policy.sql`.
  - Cột/bảng đề xuất để theo dõi escalation level, max attempts, cooldown, escalation target.

**Files dự kiến**
- `scripts/db/migrations/027_report_delivery_schedule.sql`
- `scripts/db/migrations/028_risk_score_snapshots.sql`
- `scripts/db/migrations/029_reminder_escalation_policy.sql`

**Verify**
- Chạy backend startup migration pass.
- Kiểm tra index và FK bằng integration test migration smoke.

### A2. Scheduled report delivery service + worker
- [ ] Thêm Application contracts cho schedule CRUD + run result.
- [ ] Thêm Infrastructure service để:
  - tạo/cập nhật/lấy danh sách schedule;
  - tính next-run theo timezone/cron;
  - tạo artifact từ `IReportExportService`;
  - gửi thông báo nội bộ (in-app) khi job thành công/thất bại.
- [ ] Thêm hosted service quét lịch đến hạn và enqueue/thực thi.
- [ ] Thêm endpoints quản trị lịch gửi báo cáo:
  - `GET /reports/schedules`
  - `POST /reports/schedules`
  - `PUT /reports/schedules/{id}`
  - `POST /reports/schedules/{id}/run-now`
  - `DELETE /reports/schedules/{id}`

**Files dự kiến**
- `src/backend/Application/Reports/` (DTO + interface mới)
- `src/backend/Infrastructure/Services/ReportScheduleService.cs` (mới)
- `src/backend/Api/Services/ReportScheduleHostedService.cs` (mới)
- `src/backend/Api/Endpoints/ReportEndpoints.cs` (mở rộng)
- `src/backend/Infrastructure/DependencyInjection.cs`

**Test bắt buộc**
- Unit: cron parsing/next-run/timezone normalize.
- Integration: create schedule -> run-now -> ghi run log + notification.

### A3. Risk score delta snapshot + alert pipeline
- [ ] Tạo service chụp snapshot score theo ngày (hoặc theo chu kỳ cấu hình).
- [ ] So sánh snapshot mới với snapshot trước theo customer.
- [ ] Tạo alert khi delta vượt ngưỡng cấu hình (absolute hoặc relative).
- [ ] Publish notification vào notification center (source: `RISK_DELTA`).
- [ ] Thêm endpoint truy vấn lịch sử delta:
  - `GET /risk/delta-alerts`
  - `GET /risk/{customerTaxCode}/score-history`

**Files dự kiến**
- `src/backend/Application/Risk/` (DTO/contract mới)
- `src/backend/Infrastructure/Services/RiskDeltaAlertService.cs` (mới)
- `src/backend/Api/Services/RiskDeltaHostedService.cs` (mới hoặc gộp worker)
- `src/backend/Api/Endpoints/RiskEndpoints.cs` (mở rộng)

**Test bắt buộc**
- Unit: detect delta vượt ngưỡng.
- Integration: seed snapshot N-1/N -> run detector -> có notification đúng.

### A4. Reminder escalation intelligence (nâng cấp phần partial)
- [ ] Bổ sung escalation policy: số lần nhắc, cooldown, escalation step.
- [ ] Điều chỉnh `ReminderService.Execution`:
  - tăng mức recipients theo số lần không phản hồi;
  - tránh spam (respect cooldown);
  - log rõ escalation_level + reason.
- [ ] Bổ sung endpoint preview escalation outcome (dry-run có level).

**Files dự kiến**
- `src/backend/Infrastructure/Services/ReminderService.Execution.cs`
- `src/backend/Application/Reminders/` (request/result mở rộng)
- `src/backend/Api/Endpoints/ReminderEndpoints.cs`

**Test bắt buộc**
- Unit: escalation policy transitions.
- Integration: cùng 1 customer qua nhiều lần chạy -> level tăng đúng kỳ vọng.

---

## Workstream B - Global search (`cng-los.2`, ưu tiên P1)

### B1. Unified search API
- [ ] Thiết kế endpoint `GET /search/global?q=...&top=...`.
- [ ] Trả kết quả hợp nhất theo nhóm entity:
  - Customer
  - Invoice
  - Receipt
  - (optional) Advance
- [ ] Chuẩn hóa score/ranking và label hiển thị.
- [ ] Tối ưu SQL bằng trigram/unaccent, giới hạn top per entity.

**Files dự kiến**
- `src/backend/Application/Search/` (mới)
- `src/backend/Infrastructure/Services/GlobalSearchService.cs` (mới)
- `src/backend/Api/Endpoints/SearchEndpoints.cs` (mới)
- `src/backend/Api/Program.cs` (map endpoint)

**Test bắt buộc**
- Integration: query keyword trả đúng nhóm entity.
- Unit: ranking/normalization logic.

### B2. Command surface ở AppShell
- [ ] Thêm quick search/command palette ở header AppShell.
- [ ] Hotkey mở palette (`Ctrl/Cmd + K`).
- [ ] Enter để điều hướng tới entity detail tương ứng.
- [ ] Debounce + loading + empty/error states.

**Files dự kiến**
- `src/frontend/src/layouts/AppShell.tsx`
- `src/frontend/src/components/search/GlobalSearchPalette.tsx` (mới)
- `src/frontend/src/api/search.ts` (mới)
- `src/frontend/src/styles/layout-shell.css` (nếu cần)

**Test bắt buộc**
- RTL: mở palette bằng hotkey, render result, navigate bằng Enter.

---

## Workstream C - Onboarding + Import DnD (`cng-los.3`, ưu tiên P2)

### C1. First-run onboarding tour
- [ ] Tạo onboarding state per user (localStorage key + version).
- [ ] Định nghĩa các step cho dashboard/import/reports/risk.
- [ ] Cho phép skip/replay tour từ UI.
- [ ] Không chặn luồng chính (progressive enhancement).

**Files dự kiến**
- `src/frontend/src/components/onboarding/OnboardingTour.tsx` (mới)
- `src/frontend/src/hooks/useOnboarding.ts` (mới)
- `src/frontend/src/layouts/AppShell.tsx` (entry point)

**Test bắt buộc**
- RTL: first-run hiển thị; skip lưu state; replay hoạt động.

### C2. Import drag-and-drop UX
- [ ] Bổ sung dropzone ở `ImportBatchSection`.
- [ ] Hỗ trợ click-to-upload + kéo thả cùng một luồng validate.
- [ ] Hiển thị trạng thái kéo (drag-over), file accepted/rejected.
- [ ] Bảo toàn luồng upload/preview/commit hiện có.

**Files dự kiến**
- `src/frontend/src/pages/imports/ImportBatchSection.tsx`
- `src/frontend/src/pages/imports/imports.css` hoặc style hiện hữu
- `src/frontend/src/pages/imports/__tests__/` (test mới)

**Test bắt buộc**
- RTL: drop file hợp lệ -> set file.
- RTL: drop file không hợp lệ -> hiện lỗi.

---

## Workstream D - Dashboard widget + Report print (`cng-los.4`, ưu tiên P2)

### D1. Dashboard widget customization/reorder
- [ ] Thiết kế schema preference cho dashboard widgets (visible + order).
- [ ] Thêm API đọc/ghi preference.
- [ ] Tách Dashboard sections thành các “widget block” có key ổn định.
- [ ] UI reorder + hide/show widgets (ưu tiên button up/down trước, DnD sau).

**Files dự kiến**
- Backend:
  - `src/backend/Application/Dashboard/` (preference DTO + method)
  - `src/backend/Infrastructure/Services/DashboardService*.cs`
  - `src/backend/Api/Endpoints/DashboardEndpoints.cs`
- Frontend:
  - `src/frontend/src/pages/DashboardPage.tsx`
  - `src/frontend/src/api/dashboard.ts`
  - `src/frontend/src/pages/dashboard/` (tách widget components nếu cần)

**Test bắt buộc**
- Backend integration: lưu và đọc lại preference.
- Frontend RTL: thay đổi thứ tự/ẩn hiện vẫn render đúng.

### D2. Report print layout
- [ ] Bổ sung action “In báo cáo”.
- [ ] Bổ sung stylesheet print (`@media print`) cho report sections quan trọng.
- [ ] Ẩn control không cần in; tối ưu typography và page-break.
- [ ] Giữ tương thích với export PDF hiện có (không thay thế).

**Files dự kiến**
- `src/frontend/src/pages/ReportsPage.tsx`
- `src/frontend/src/pages/reports/reports.css`

**Test bắt buộc**
- UI smoke: trigger print action gọi `window.print`.
- Snapshot/style assertion cho class print state (nếu có).

---

## Workstream E - Verification + tracker sync (`cng-los.5`, ưu tiên P1)

### E1. Verification commands
- [ ] `dotnet build src/backend/Api/CongNoGolden.Api.csproj`
- [ ] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj`
- [ ] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj`
- [ ] `npm --prefix src/frontend run lint`
- [ ] `npm --prefix src/frontend run test -- --run`
- [ ] `npm --prefix src/frontend run build`

### E2. Tracker/docs sync
- [ ] Cập nhật `task.md` theo phase.
- [ ] Cập nhật bead trạng thái `cng-los.*`.
- [ ] Cập nhật `findings.md` và `progress.md`.
- [ ] Đánh dấu các claim Opus đã xử lý với evidence file-level.

---

## 5) Thứ tự triển khai khuyến nghị

1. Workstream A (backend core) và B (global search API) song song phần độc lập.
2. C và D triển khai sau khi API contracts ổn định.
3. E chạy ở cuối mỗi cụm tính năng, không dồn đến phút cuối.

---

## 6) Tiêu chí hoàn tất toàn bộ epic `cng-los`

- Tất cả claim trong nhóm `CONFIRMED GAP`/`PARTIAL` được xử lý hoặc có quyết định defer rõ lý do.
- Có test backend + frontend cho từng feature mới.
- `task.md` + beads + docs nhất quán, không còn trạng thái “đánh dấu xong nhưng chưa implement”.
- Build/test/lint pass theo checklist Workstream E.

