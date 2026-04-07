# Findings - 2026-02-23

## Branch Sync Merge (2026-04-07)
- `main` trước khi merge chỉ lệch `fix/table-scroll-hint-20260307` ở một commit dữ liệu cục bộ: `9347540`.
- `fix/table-scroll-hint-20260307` chứa toàn bộ chuỗi commit đang gần với runtime Docker, nên cách an toàn là merge ngược vào `main` thay vì reset/cherry-pick.
- Merge commit tạo ra là `8e71b66`, giữ nguyên cả `9347540` lẫn toàn bộ commit riêng của nhánh fix.
- Regression duy nhất sau merge không phải lỗi logic mà là test frontend cũ:
  - `src/frontend/src/pages/imports/__tests__/importBatchRecovery.test.ts`
  - trạng thái `ready` hiện trả `previewButtonLabel = 'Xem trước lần cuối'`, không còn là `'Xem trước'`.
- Verification đã có:
  - backend targeted unit pass `20/20`;
  - frontend targeted vitest pass `13/13`.

## Customer Assignment Import (2026-04-06)
- Nguồn dữ liệu: `người phụ trách.xlsx`, `376` dòng hữu hiệu, không có MST trùng trong file.
- Mapping tài khoản phụ trách trong DB hợp lệ cho toàn bộ phạm vi import:
  - `kt.ngat`, `kt.trang`, `kt.tranghp`, `kt.diem`, `kt.linh`
- Kết quả ghi dữ liệu:
  - Tạo mới `138` khách hàng.
  - Cập nhật `238` khách hàng hiện có.
  - Gán toàn bộ quản lý về `kt.ngat` (`Phạm Thị Hồng Ngát`).
- Hậu kiểm sau import:
  - Không còn khách hàng nào trong file bị thiếu trên DB.
  - Không còn lệch `accountant_owner_id`.
  - Không còn lệch `manager_user_id`.
  - Không còn trường hợp thiếu tên/địa chỉ trong phạm vi yêu cầu nếu DB trước đó để trống.

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

## 2026-03-17 - cng-huj import reduction adjustments
- Workbook `ReportDetail.xlsx` có 4 dòng âm, tất cả cùng note `Hóa đơn điều chỉnh giảm`, tổng giảm AR `9,158,400`, giảm doanh thu trước VAT `8,480,000`, giảm VAT `678,400`.
- File có 9 dòng giá trị `0`; 5 dòng note `Hóa đơn bị thay thế`, 4 dòng note `Hóa đơn điều chỉnh thông tin`. Đây là dòng thông tin, không nên ghi nhận tài chính nhưng preview cần nêu rõ lý do skip.
- Dữ liệu file không đủ để match 1:1 ổn định cho toàn bộ dòng âm:
  - `2210` / `0110386229`: không có positive exact match.
  - `2213` / `0103440965`: có 4 positive exact matches, không deterministic.
  - `2233` / `0310226744-003`: không có exact match.
  - `2514` / `0310226744`: có 2 exact matches và còn case root/branch ambiguity.
- Kết luận nghiệp vụ đã chốt:
  - Dòng âm hợp lệ phải đi theo document type riêng `ADJUSTMENT_REDUCTION`.
  - Match trực tiếp chỉ dùng khi unique đủ chắc chắn; nếu không thì FIFO trên open receivables của khách hàng đã group theo root MST.
  - Nếu số giảm vượt toàn bộ công nợ mở thì phần còn lại phải vào held/general credit, không để thành invoice âm đang mở.
- Điểm chặn kỹ thuật hiện tại:
  - Parser/staging đang reject số âm ở `ImportInvoiceParser.cs` và `ImportStagingHelpers.cs`.
  - Commit builder đang hardcode `InvoiceType = "NORMAL"` và `OutstandingAmount = total` trong `ImportCommitBuilders.cs`.
  - Auto allocation/import reconcile hiện chỉ xử lý invoice outstanding dương trong `ImportCommitService.cs`, `InvoiceCreditReconcileService.cs`, `AllocationEngine.cs`, `CustomerBalanceReconcileService.cs`.
- `InvoiceType` đã tồn tại trong entity và preview đã có cơ chế hiển thị lý do skip, nên hướng thay đổi phù hợp là mở rộng flow hiện có thay vì tạo bảng/chứng từ tài chính mới hoàn toàn.

## 2026-03-17 - cng-huj verification sync
- Parser/unit flow đã xanh:
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter "FullyQualifiedName~ImportInvoiceParserTests" -v minimal` => `4/4`.
- Import reduction integration flow đã xanh:
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --no-restore -p:BuildInParallel=false -m:1 --filter "FullyQualifiedName~ImportCommitInvoiceAutoAllocateTests"` => `7/7`.
- Preview validation messages cho dòng thông tin giá trị `0` đã xanh:
  - `npm --prefix src/frontend run test -- --run src/pages/imports/__tests__/importValidationMessages.test.ts` => `2/2`.
- Heuristic match cuối cùng đã chốt trong code:
  - nếu có đúng một open invoice có `OutstandingAmount` bằng chính giá trị reduction thì ưu tiên invoice đó trước;
  - nếu có `0` hoặc `>1` exact match thì giữ nguyên FIFO theo `IssueDate/CreatedAt`.
- Lý do heuristic trên được giữ ở mức tối thiểu:
  - dữ liệu file import không có khóa tham chiếu nguồn ổn định cho mọi dòng âm;
  - notebook thực tế cho thấy có case không có exact match và có case nhiều exact match, nên chỉ exact-match duy nhất mới đủ an toàn để override FIFO.

## 2026-03-18 - cng-z39 runtime + e2e verification
- Runtime Docker được rebuild từ snapshot `HEAD` sạch, nên phần verify production-like bám đúng fix đã commit trong `ConGNoDbContext` thay vì ăn theo các thay đổi backend/frontend còn dở trong worktree hiện tại.
- E2E preview cần locator scope theo dialog:
  - MST khách hàng mới cũng xuất hiện trong tên file upload tạm, nên `getByText(customerTaxCode)` gây strict-mode violation;
  - modal preview có cả scrim `Đóng hộp thoại` và button action `Đóng`, nên selector đóng modal phải khóa vào exact button bên trong dialog.
- Selector ổn định cuối cùng:
  - preview assert dựa trên `pre.code-block` chứa JSON `"customer_tax_code":"<mst>"`;
  - close action dùng `previewDialog.getByRole('button', { name: 'Đóng', exact: true })`.
- Sau khi rebuild runtime, `/health` và `/health/ready` đều trả `ok`, và Playwright scenario import `ADVANCE` với customer mới xác nhận dữ liệu commit xong xuất hiện lại ở workspace `Advances` với nguồn `Import · <batch>`.

## 2026-03-18 - cng-p9o import recovery UX
- Root cause UX:
  - Batch `STAGING` có dòng lỗi nhưng history chỉ hiển thị CTA kiểu `Tiếp tục`, không nói rõ batch đang bị chặn hay chỉ cần rà soát.
  - Workspace import vẫn để `Ghi dữ liệu` khả dụng quá lâu, làm kế toán tưởng hệ thống có thể commit được dù preview vẫn còn lỗi.
  - Guidance chỉ xuất hiện sau khi mở preview, nên batch lỗi tạo cảm giác bị kẹt thay vì có recovery path.
- Quick fix đã triển khai:
  - Thêm `importBatchRecovery.ts` để chuẩn hóa trạng thái `loading/blocked/review/ready`.
  - `ImportBatchSection` có banner ngữ cảnh + pill trạng thái + đổi nhãn CTA preview theo từng trạng thái.
  - `handleCommit` chặn commit khi còn lỗi, mở preview và trả thông điệp rõ ràng thay vì tiếp tục gửi commit.
  - `ImportPreviewModal` có alert hướng dẫn riêng cho `error` và `warning`.
  - `ImportHistorySection` đổi CTA `Tiếp tục` thành `Kiểm tra lô` và copy summary `Chưa ghi dữ liệu`.
- Verification:
  - Targeted vitest cho helper/workspace/preview/history pass `19/19`.
  - `npm run build` pass.
  - `npm run lint` không có lỗi mới; còn `1` warning unrelated tại `ReceiptListSection.tsx`.
- Residual gap sau quick fix:
  - History API vẫn chưa trả validation counts/sub-status, nên trạng thái blocked/review/ready hiện mới được diễn giải tốt nhất trong workspace đang mở.
  - UI chưa có grouping triage kiểu `Cần xử lý ngay` / `Sẵn sàng ghi`.
  - Recovery action vẫn mới dừng ở mức “xem lỗi/rà soát/hủy”, chưa có flow tải lại file đã sửa ngay từ cùng ngữ cảnh.

## 2026-03-18 - cng-0ye redesign plan
- Đã tạo bead follow-up `cng-0ye` để giữ scope redesign dài hạn tách biệt khỏi quick fix `cng-p9o`.
- Hướng redesign đề xuất:
  - API/status model:
    - trả `okCount/warnCount/errorCount` hoặc staging sub-status riêng cho history;
    - cho phép UI biết lô đang `blocked`, `review`, hay `ready` mà không cần fetch full preview trước.
  - History triage:
    - gom nhóm lô theo mức ưu tiên xử lý;
    - hiển thị nhãn tiếng Việt rõ ràng như `Có lỗi cần sửa`, `Cần rà soát`, `Sẵn sàng ghi`.
  - Workspace ergonomics:
    - chỉ đưa CTA phù hợp với trạng thái hiện tại lên primary;
    - giảm phụ thuộc vào preview modal để hiểu “cần làm gì tiếp theo”.
  - Rollout/testing:
    - giữ backward-compatible với quick fix hiện tại;
    - thêm regression/e2e cho từng staging sub-status mới.

## 2026-03-18 - cng-z25 template parser regression
- Root cause:
  - `cng-huj` chỉ sửa [ImportInvoiceParser.cs](E:/GPT/CONG_NO_KT/src/backend/Infrastructure/Services/ImportInvoiceParser.cs) cho file kiểu `ReportDetail`.
  - File INVOICE dạng template vẫn đi qua [ImportInvoiceTemplateParser.cs](E:/GPT/CONG_NO_KT/src/backend/Infrastructure/Services/ImportInvoiceTemplateParser.cs) từ [ImportStagingService.cs](E:/GPT/CONG_NO_KT/src/backend/Infrastructure/Services/ImportStagingService.cs).
  - Parser template còn hardcode rule `revenue < 0 || vat < 0 || total < 0 => NEGATIVE_AMOUNT`, nên dòng `Hóa đơn điều chỉnh giảm` bị `SKIP`.
- Fix đã triển khai:
  - thêm nhận diện `IsReductionAdjustmentNote(note)` cho parser template;
  - bỏ `NEGATIVE_AMOUNT` với dòng adjustment hợp lệ;
  - giữ nguyên MST gốc ở `customer_tax_code`;
  - thêm `customer_tax_code_matching` theo root MST;
  - set `invoice_type = ADJUSTMENT_REDUCTION`.
- Regression test:
  - thêm case đỏ/xanh tại [ImportInvoiceTemplateParserTests.cs](E:/GPT/CONG_NO_KT/src/backend/Tests.Unit/ImportInvoiceTemplateParserTests.cs) để khóa behavior template import.
- Verification:
  - test đỏ tái hiện lỗi: expected `INSERT`, actual `SKIP`;
  - targeted parser tests sau fix pass `7/7`.

## 2026-03-18 - cng-h1d frontend prefetch alignment
- Root cause:
  - auth state/frontend gating đã bắt đầu đi theo `permissions`, nhưng [pageLoaders.ts](E:/GPT/CONG_NO_KT/src/frontend/src/pages/pageLoaders.ts) vẫn chọn affinity/prefetch budget từ `roles` thuần.
  - [AppShell.tsx](E:/GPT/CONG_NO_KT/src/frontend/src/layouts/AppShell.tsx) cũng chưa truyền `state.permissions` vào prefetch planner, nên user không có role nhưng có permission accountant vẫn bị fallback sang thứ tự generic.
- Fix đã triển khai:
  - thêm mapping `rolePermissionSignals` + helper `hasAnyPermission(...)` để suy ra effective role từ `roles + permissions`;
  - cập nhật `computePrefetchBudget`, `computePrefetchPlan`, `selectPrefetchTargets` nhận `permissions`;
  - cập nhật [page-loaders.test.ts](E:/GPT/CONG_NO_KT/src/frontend/src/pages/__tests__/page-loaders.test.ts) và [AppShell.tsx](E:/GPT/CONG_NO_KT/src/frontend/src/layouts/AppShell.tsx) để khóa regression cho permission-only users.
- Verification:
  - `npm --prefix src/frontend run test -- --run src/pages/__tests__/page-loaders.test.ts` => `10/10`;
  - `npm --prefix src/frontend run test -- --run src/layouts/__tests__/app-shell.test.tsx` => `9/9`.
- Residual gap:
  - đây mới là alignment cho prefetch/navigation heuristics;
  - Phase 110 vẫn còn các slice lớn hơn về backend permission matrix, customer edit/import commit permissions, và admin permission management.

## 2026-03-19 - cng-h1d permission matrix closure
- Current state confirmed:
  - customer manage gating và import commit gating ở frontend đã chuyển sang permission-first, không còn fallback role cứng cho các flow accountant cần dùng;
  - admin UI đã có [RolePermissionsManager](E:/GPT/CONG_NO_KT/src/frontend/src/pages/admin/RolePermissionsManager.tsx) và được gắn vào [AdminUsersPage.tsx](E:/GPT/CONG_NO_KT/src/frontend/src/pages/AdminUsersPage.tsx);
  - backend đã đăng ký các route permission management (`/admin/permissions`, `/admin/roles/{roleId:int}/permissions`) và có route test tương ứng.
- Fix hoàn tất trong lượt này:
  - loại import React trùng trong [CustomersPage.tsx](E:/GPT/CONG_NO_KT/src/frontend/src/pages/customers/CustomersPage.tsx) để tránh lỗi build/lint;
  - cập nhật regression test [role-permissions-manager.test.tsx](E:/GPT/CONG_NO_KT/src/frontend/src/pages/admin/__tests__/role-permissions-manager.test.tsx) để chọn đúng `select` option theo `role.id`, khớp contract thực của component.
- Verification:
  - `npm --prefix src/frontend run test -- --run src/context/__tests__/auth-guards.test.tsx src/pages/customers/__tests__/customers-page.permissions.test.tsx src/pages/imports/__tests__/imports-page.fixed-type.test.tsx src/pages/admin/__tests__/role-permissions-manager.test.tsx src/pages/admin/__tests__/admin-users-page.test.tsx` => `16/16`;
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter "FullyQualifiedName~AdminEndpointsRouteTests" -v minimal` => `1/1`.
- Status:
  - checklist Phase 110 đã đủ bằng chứng để đóng bead `cng-h1d`.
