> [!IMPORTANT]
> **HISTORICAL DOCUMENT**
> Tài liệu này là snapshot/lịch sử để tham khảo, **không phải nguồn vận hành chuẩn hiện tại**.
> Nguồn chuẩn hiện tại:
> - Deploy: DEPLOYMENT_GUIDE_DOCKER.md
> - Runbook: RUNBOOK.md
> - Ops runtime: docs/OPS_ADMIN_CONSOLE.md
> [!NOTE]
> `task_plan.md` và `findings.md` được nhắc trong nội dung bên dưới là tham chiếu lịch sử.
> Hai file này đã được dọn ở **Phase 63 (2026-02-14)** và được khôi phục lại ở **Phase 66 (2026-02-23)** cho workflow planning hiện hành.
# Progress Log
<!-- 
  WHAT: Your session log - a chronological record of what you did, when, and what happened.
  WHY: Answers "What have I done?" in the 5-Question Reboot Test. Helps you resume after breaks.
  WHEN: Update after completing each phase or encountering errors. More detailed than task_plan.md.
-->

## Session: 2026-01-29
<!-- 
  WHAT: The date of this work session.
  WHY: Helps track when work happened, useful for resuming after time gaps.
  EXAMPLE: 2026-01-15
-->

### Phase 1: Requirements & Discovery
<!-- 
  WHAT: Detailed log of actions taken during this phase.
  WHY: Provides context for what was done, making it easier to resume or debug.
  WHEN: Update as you work through the phase, or at least when you complete it.
-->
- **Status:** complete
- **Started:** 2026-01-29 07:40
<!-- 
  STATUS: Same as task_plan.md (pending, in_progress, complete)
  TIMESTAMP: When you started this phase (e.g., "2026-01-15 10:00")
-->
- Actions taken:
  <!-- 
    WHAT: List of specific actions you performed.
    EXAMPLE:
      - Created todo.py with basic structure
      - Implemented add functionality
      - Fixed FileNotFoundError
  -->
  - Xác nhận phạm vi: làm cả 3 (groupBy, BE review, UI review)
  - Tạo task_plan.md/findings.md/progress.md
  - Sửa FE Dashboard/Notifications theo guideline trước đó
- Files created/modified:
  <!-- 
    WHAT: Which files you created or changed.
    WHY: Quick reference for what was touched. Helps with debugging and review.
    EXAMPLE:
      - todo.py (created)
      - todos.json (created by app)
      - task_plan.md (updated)
  -->
  - task_plan.md (created)
  - findings.md (updated)
  - progress.md (updated)
  - src/frontend/src/pages/DashboardPage.tsx (modified)
  - src/frontend/src/pages/NotificationsPage.tsx (modified)
  - src/frontend/src/index.css (modified)

### Phase 2: Planning & Structure
<!-- 
  WHAT: Same structure as Phase 1, for the next phase.
  WHY: Keep a separate log entry for each phase to track progress clearly.
-->
- **Status:** in_progress
- Actions taken:
  - Chọn thứ tự: fix groupBy → BE review → UI review
  - Thêm SQL groupBy customer + cập nhật logic chọn SQL
  - Thêm integration test Dashboard overdue groups
- Files created/modified:
  - src/backend/Infrastructure/Services/DashboardService.Sql.cs (modified)
  - src/backend/Infrastructure/Services/DashboardService.cs (modified)
  - src/backend/Tests.Integration/DashboardOverdueGroupTests.cs (created)

## Test Results
<!-- 
  WHAT: Table of tests you ran, what you expected, what actually happened.
  WHY: Documents verification of functionality. Helps catch regressions.
  WHEN: Update as you test features, especially during Phase 4 (Testing & Verification).
  EXAMPLE:
    | Add task | python todo.py add "Buy milk" | Task added | Task added successfully | ✓ |
    | List tasks | python todo.py list | Shows all tasks | Shows all tasks | ✓ |
-->
| Test | Input | Expected | Actual | Status |
|------|-------|----------|--------|--------|
| dotnet test (integration) | `dotnet test Tests.Integration\\CongNoGolden.Tests.Integration.csproj --no-build` | Pass | 14 passed | ✓ |

## Error Log
<!-- 
  WHAT: Detailed log of every error encountered, with timestamps and resolution attempts.
  WHY: More detailed than task_plan.md's error table. Helps you learn from mistakes.
  WHEN: Add immediately when an error occurs, even if you fix it quickly.
  EXAMPLE:
    | 2026-01-15 10:35 | FileNotFoundError | 1 | Added file existence check |
    | 2026-01-15 10:37 | JSONDecodeError | 2 | Added empty file handling |
-->
<!-- Keep ALL errors - they help avoid repetition -->
| Timestamp | Error | Attempt | Resolution |
|-----------|-------|---------|------------|
| 2026-01-29 07:58 | CustomerEntities.cs not found | 1 | Tìm đúng file entity (MasterEntities.cs) |
| 2026-01-29 08:10 | dotnet test timeout | 1 | Chạy lại với timeout dài hơn / chỉ Tests.Integration |
| 2026-01-29 08:12 | dotnet test failed - DLL locked by running API | 2 | Thử `dotnet test --no-build` hoặc dừng API |
| 2026-02-09 09:20 | dotnet build Ops.Console timeout | 1 | Chạy lại với timeout 120s |
| 2026-02-09 10:02 | dotnet build Ops.Console failed (Path/Directory not found) | 1 | Dùng System.IO.Path/Directory để tránh xung đột WPF Path |
| 2026-02-09 10:42 | Ops Console create DB lỗi JSON parse | 1 | Bổ sung AgentClient xử lý non-JSON/404 và trả lỗi rõ ràng |

## 5-Question Reboot Check
<!-- 
  WHAT: Five questions that verify your context is solid. If you can answer these, you're on track.
  WHY: This is the "reboot test" - if you can answer all 5, you can resume work effectively.
  WHEN: Update periodically, especially when resuming after a break or context reset.
  
  THE 5 QUESTIONS:
  1. Where am I? → Current phase in task_plan.md
  2. Where am I going? → Remaining phases
  3. What's the goal? → Goal statement in task_plan.md
  4. What have I learned? → See findings.md
  5. What have I done? → See progress.md (this file)
-->
<!-- If you can answer these, context is solid -->
| Question | Answer |
|----------|--------|
| Where am I? | Phase 2 (đang fix groupBy, chuẩn bị BE/UX review) |
| Where am I going? | Phase 3–5 |
| What's the goal? | Hoàn tất review toàn hệ thống + fix issue ưu tiên |
| What have I learned? | Historical notes nằm trong file này; `findings.md` đã được khôi phục lại ở Phase 66 để làm sổ tay phiên hiện hành. |
| What have I done? | See above |

---
<!-- 
  REMINDER: 
  - Update after completing each phase or encountering errors
  - Be detailed - this is your "what happened" log
  - Include timestamps for errors to track when issues occurred
-->
*Update after completing each phase or encountering errors*
2026-01-29: Người dùng xác nhận dùng giả định mặc định về hiệu năng; yêu cầu báo cáo/duyệt trước mọi refactor lớn.
2026-01-29: dotnet test Tests.Integration failed (DLL locked by CongNoGolden.Api). npm test -- --run (frontend) passed.
2026-01-29: Cập nhật ReportService.Aging thêm lọc VOID/future; thêm ReportAgingTests; chỉnh ImportPreviewModal (keyboard/ellipsis) và AdminUsersPage (autocomplete/name/ellipsis).
2026-01-29: dotnet test Tests.Integration passed (16 tests).

Update: inspected ReceiptAutomationService + modal-backdrop usage; ready to write failing tests per TDD.

Update: searched tests for ReceiptAutomationService; preparing new TDD test cases.

Update: inspected ReportsValidationModal markup to plan modal-backdrop refactor.

Update: searched for interceptor patterns; will decide test strategy for N+1 fix.

Update: enumerated entity files; next read MasterEntities.cs for customer fields.

Update: captured entity fields needed for ReceiptAutomationService test data.

Update: captured invoice/advance seeding examples for new integration test.

Update: reviewed ReceiptService open-item loader; ready to implement batch loader in automation service.

Update: located receipt modal tests for adding scrim assertions.

Update: confirmed modal-backdrop CSS in index.css for upcoming refactor.

Update: inspected ReceiptCancelModal and ReceiptViewAllocationsModal for scrim refactor.

Update: inspected ReceiptAllocationModal and ReceiptAdvancedModal for refactor.

Update: inspected import preview + notification critical modal for scrim conversion.

Update: inspected CustomerEditModal + CustomerTransactionModals for scrim refactor.

Update: confirmed modal-scrim usage in all modal components.
Update: Frontend vitest ran reports-validation-modal.test.tsx + receipts-modules.test.tsx (pass).
Update: dotnet test ReceiptAutomationServiceTests passed after stopping CongNoGolden.Api (SELECT count 6).
Update: ran full backend tests `dotnet test` (Unit 32/32 pass, Integration 17/17 pass).
Update: ran full frontend tests `npm test -- --run` (9 files, 33 tests pass).
Update: prepared to run Playwright E2E via npm run test:e2e.
Update: skill path lookup done; next run Playwright E2E via npm run test:e2e.
Update: ran Playwright E2E `npm run test:e2e` → 9 passed, 2 skipped.
2026-01-30: Thiết kế + triển khai hệ thống backup/restore (backend + frontend) theo spec.
2026-01-30: Thêm migration 018_backup_system.sql + entities + services + scheduler + endpoints + maintenance middleware.
2026-01-30: Thêm AdminBackupPage + API client + route/nav + test UI.
2026-01-30: dotnet test Tests.Unit passed (38 tests).
2026-01-30: npm run lint + npm run test -- --run passed (10 files, 34 tests).
2026-01-30: dotnet build Api/CongNoGolden.Api.csproj failed do file lock (CongNoGolden.Api đang chạy).
2026-01-30: npm run test:e2e passed (10 passed, 1 skipped).
2026-01-30: Fixed 018_backup_system.sql (set search_path) and applied migration; verified backup tables exist.
2026-01-30: dotnet test passed (Unit 38/38, Integration 17/17).
2026-01-30: Fixed BackupAudit jsonb mapping + added unit test; dotnet test passed (Unit 39/39, Integration 17/17).
2026-01-30: Disabled antiforgery on /admin/backup/upload + added endpoint metadata unit test; dotnet test passed (Unit 40/40, Integration 17/17).
2026-01-30: Added immediate restore notice in AdminBackupPage with regression test (vitest passed).
2026-01-30: Parse application/problem+json in api client; added vitest for error parsing.
2026-01-30: Added restore error details (stdout/stderr) + formatter test; dotnet test passed (Unit 41/41, Integration 17/17).
2026-01-30: Added restore hint for DB owner requirement; dotnet test passed (Unit 42/42, Integration 17/17).
2026-01-30: Frontend vitest passed (11 files, 36 tests). E2E Playwright failed 1/11 due to duplicate "Đóng" button selector in customers-actions.
2026-01-30: Updated customers-actions E2E to disambiguate "Đóng" button; Playwright E2E passed (11/11).
2026-01-30: dotnet build Api/CongNoGolden.Api.csproj passed (0 warnings, 0 errors).
2026-01-30: dotnet test passed (Unit 38/38, Integration 17/17).
2026-01-30: npm run test:e2e passed (10 passed, 1 skipped).
2026-01-30: Test restore qua API (upload + restore) thành công khi chạy backend với ConnectionStrings__Migrations=postgres; root cause lỗi "must be owner" là backend không dùng DB owner connection string.
2026-01-30: Phát hiện UI vẫn lỗi do backend đang chạy là CongNoGolden.Api.exe không có env Migrations; restart backend từ thư mục exe với env Default+Migrations=postgres; health OK; restore qua API trả status ok.
2026-01-30: Điều tra lỗi F5/reload bị redirect login; cập nhật AuthProvider bootstrap để retry refresh 1 lần khi lỗi không phải 401, thêm test auth-context.
2026-01-30: Chặn refresh token bị dùng song song (StrictMode) bằng refreshSession dedupe; thêm test auth-refresh.
2026-01-30: Tạo backup v1.0: pg_dump congno_golden -> backups/v1.0/congno_golden_v1.0.dump; đóng gói zip CONG_NO_KT_v1.0_3.zip (loại trừ file đang mở và thư mục build).
2026-01-31: Added Ops admin console + agent (src/ops) with config, service/IIS control, backup/restore, logs tail, update runner; WPF console UI; docs OPS_ADMIN_CONSOLE.md; dotnet build + tests passed (Ops.Tests 8/8).
2026-02-01: Fixed Ops.Console crash on Connect by recreating HttpClient in AgentClient; moved AgentClient to Ops.Shared; added AgentConnectionTests; dotnet build ops + dotnet test ops (10/10) pass.
2026-02-01: Ops.Console dashboard health/status outputs switched to selectable TextBox for copy; AgentClient now recreates HttpClient on Connect; tests Ops.Tests 10/10 pass; ops console build pass (after closing running Ops.Console).
2026-02-01: Added diagnostics + backend service install endpoint (nssm) + setup tab; ServiceControl now handles missing service; IIS module check; dashboard outputs copyable; ops tests 12/12 pass.
2026-02-01: Added diagnostics endpoint + Setup tab + backend service install fallback using sc.exe when nssm missing; improved IIS module/site checks; ops build/test pass (12/12).
2026-02-01: Backend API now uses UseWindowsService + added WindowsServices package so sc.exe service can start; dotnet build Api passed.
2026-02-03: Analyzed backup scheduler/job lock/restore notice issues; drafted fix plan in docs/plans/2026-02-03-backup-scheduler-restore-fixes.md.
2026-02-03: Task 1 done: added pending scheduled guard (IBackupService + BackupService + scheduler) and unit test; added EFCore.InMemory package; dotnet test filter passed.
2026-02-03: Task 2 done: mark lock-contended backup jobs as skipped + integration test; dotnet test filter passed.
2026-02-03: Task 3 done: clear restore notice on failure + UI test; vitest test file passed.
2026-02-03: Bổ sung nhãn tiếng Việt cho trạng thái skipped (Bỏ qua); vitest admin-backup-page.test.tsx passed.
2026-02-03: Chạy full frontend vitest (npm run test -- --run) -> 13 files, 40 tests pass.
2026-02-03: Chạy Playwright E2E (npm run test:e2e) -> 10 passed, 1 skipped.
2026-02-03: Lập kế hoạch xử lý lệch trạng thái phân bổ phiếu thu/khách hàng, lưu tại docs/plans/2026-02-03-receipt-allocation-sync.md.
2026-02-26: Hoàn thiện Collection Task Queue wiring (DI + endpoint mapping) và sửa dedupe/count logic khi generate từ risk list.
2026-02-26: Bổ sung unit tests CollectionTaskQueueTests (3 case: dedupe, no-duplicate-open-task, status transition).
2026-02-26: Bổ sung responsive card layout cho Reports tables (data-label + CSS `.table--mobile-cards`) và cập nhật test reports-modules.
2026-02-26: Verification targeted pass: `dotnet test --filter CollectionTaskQueueTests` (3/3), `npm run test -- reports-modules.test.tsx` (9/9).
2026-02-03: Đã truy vấn DB cho MST 0315666756; xác nhận receipt PARTIAL có unallocated 592,320; advance 600,000 chưa được phân bổ (receipt_allocations = 0).
2026-02-03: Added integration tests for auto-allocating receipt credits to advances (approve + import commit); implemented allocation logic in AdvanceService and ImportCommitService.
2026-02-03: dotnet test filters passed for AdvanceAutoAllocateTests + ImportCommitAdvanceAutoAllocateTests.
2026-02-03: Ran backfill script to allocate unallocated receipts to outstanding advances (1 advance/1 receipt updated, 1 allocation); verified MST 0315666756 now receipts fully allocated, advance outstanding 7,680.
2026-02-03: Added import auto-allocation for new invoices (receipt credits -> invoice), with integration test ImportCommitInvoiceAutoAllocateTests; dotnet test filter passed.
2026-02-03: Ran invoice backfill (receipt credits -> invoices). Result: invoices_scanned=94, invoices_updated=0, receipts_updated=0, allocations_created=0.
2026-02-03: Added invoice credit reconcile scheduler (hosted service + options) and InvoiceCreditReconcileService; added integration test InvoiceCreditReconcileServiceTests; dotnet test filter passed.
2026-02-04: Đổi thông báo tuổi nợ rỗng trên Reports thành 'Khách hàng không còn khoản nợ nào trên hệ thống'; thêm vitest cho charts/tables; vitest passed.
2026-02-04: Fixed report paged SQL CTE scope + typed parameters; ReportPagedTests pass (dotnet test filtered).\n2026-02-04: Added report pagination/sort/top count persistence in Reports UI; updated empty-state + export labels + summary headers; vitest reports-modules.test.tsx pass.\n
2026-02-04: Ran full backend tests (dotnet test) -> Unit 43/43, Integration 25/25 pass.\n2026-02-04: Ran full frontend tests (npm test -- --run) -> 13 files, 42 tests pass.\n
2026-02-04: Playwright E2E failed first due to port 5173 already in use; killed leftover listener.
2026-02-04: Playwright E2E rerun timed out from CLI (EPIPE) while backend was running; reran with extended timeout.
2026-02-04: Playwright E2E passed (11/11) with backend running on 8080; stopped backend afterward.
2026-02-04: Ran integration tests for notifications/reminders (NotificationPreferencesTests + ReminderRunTests) -> 3/3 pass.
2026-02-08: Ops Console bổ sung cấu hình IIS App Pool Name trong UI; đổi IisConfigHelper → IisConfigUpdater + tests; build Ops.Console + dotnet test Ops.Tests (pass, warnings CA1416/nullable).
2026-02-08: Thêm validator App Pool Name (regex + length) + cảnh báo inline khi nhập; thêm tests AppPoolNameValidatorTests; dotnet test filter pass (warnings CA1416/nullable).
2026-02-08: Test Ops Agent API bằng X-Api-Key từ agent-config.json: /health, /status, /diagnostics OK; nhiều endpoint trả 404 (metrics/app-pool/version/backup-schedule) → khả năng agent đang chạy bản cũ chưa có route mới.
2026-02-08: Test API tác động: backend start/stop OK; frontend stop/start OK; backups list OK; backup create OK (file congno_20260208_154248.dump). Nhiều endpoint (app-pool/maintenance/compression/cache/log-level/jobs/service config) trả 404 → agent cần cập nhật bản mới.
2026-02-08: Không thể stop dịch vụ CongNoOpsAgent do thiếu quyền (Access denied). Tạo agent tạm thời port 6091 bằng dotnet run để test route mới: metrics/app-pool/version/backup-schedule/maintenance/log-level/jobs OK; compression GET trả 500 (JsonElement.GetBoolean với null ở IisControl.GetCompressionSettingsAsync line 161). Đã tắt agent tạm (port 6091).
2026-02-08: Fix lỗi compression: thêm CompressionSettingsParser xử lý null/invalid json, IisControl dùng parser; thêm CompressionSettingsParserTests (pass). Publish bản mới ra C:\apps\congno\ops\agent.new để thay thế service.
2026-02-08: Test full nhóm Ops Agent (service thật) với API key: tất cả OK, không lỗi; kết quả lưu C:\apps\congno\ops\tmp\ops-test-results.json. Đã publish release v1: C:\apps\congno\ops\release\v1 (agent+console) và zip C:\apps\congno\ops\release\Ops_release_v1.zip.
2026-02-09: Tái cấu trúc UI Ops.Console theo hướng data-dense: thêm "Tác vụ nhanh", card trạng thái App Pool ở Overview, inline validation App Pool Name, bố cục Frontend/Backend/Services dạng grid, cập nhật styles.
2026-02-09: dotnet build Ops.Console (Release) thành công (0 warnings/errors).
2026-02-09: Thêm Ops Agent DB init: endpoint tạo database + chạy migrations (DbUp), bổ sung UI khởi tạo CSDL + hướng dẫn quy trình deploy lần đầu.
2026-02-09: dotnet test Ops.Tests (Release) pass; cảnh báo CA1416/nullable giữ nguyên như trước.
2026-02-09: dotnet build Ops.Console (Release) pass sau khi fix Path/Directory conflict.
2026-02-09: Cập nhật AgentClient để xử lý phản hồi lỗi (HTTP/404/JSON rỗng) rõ ràng cho CommandResponse.
2026-02-09: dotnet build Ops.Console + dotnet test Ops.Tests (Release) pass (warnings CA1416/nullable).
2026-02-09: Chuẩn hóa output tạo DB (DO), hỗ trợ thư mục migration/migrations, UpdateRunner tự copy migrations vào backend; cập nhật tests + dotnet test Ops.Tests pass; publish agent -> C:\apps\congno\ops\agent.new.
2026-02-09: Thêm mục cài đặt prerequisite (Dotnet Hosting/Desktop, Node LTS, IIS URL Rewrite) trong tab Triển khai; thêm agent endpoints /prereq + cài đặt tự động; bổ sung tests; dotnet test Ops.Tests + dotnet build Ops.Console pass.
2026-02-09: Tạo Ops.Launcher (WPF) UI theo ui-ux-pro-max; thêm kiểm tra service, mở console/logs/config, logging launcher; publish launcher -> C:\apps\congno\ops\launcher và mở ứng dụng.
2026-02-09: Thêm icon/logo cho Ops Console và Ops Launcher (Assets/*.ico), set ApplicationIcon + Window Icon; publish + copy console/launcher; mở ứng dụng để kiểm tra.
2026-02-09: Cập nhật logo web: copy Logo.ico -> public/favicon.ico + regenerate pwa-192/512 từ Logo.ico; cập nhật index.html dùng favicon.ico; cập nhật dist assets.
2026-02-10: Build release bundle (ops+backend+frontend+migrations) to E:\GPT\CONG_NO_KT\release\CongNoGolden_DeployBundle_20260210 and zip to CongNoGolden_DeployBundle_20260210.zip; added DEPLOY_GUIDE.md; Ops Console auto-detect payload if source empty.
2026-02-10: Tạo script scripts/INSTALL_OPS.ps1 (copy ops+payload + cài service agent) và cập nhật DEPLOY_GUIDE.md; rezip bundle.
2026-02-10: Bổ sung scripts INSTALL_OPS (auto DB+Migrations), ROLLBACK_OPS, UNINSTALL_OPS vào bundle; cập nhật DEPLOY_GUIDE.md; rezip bundle.
2026-02-23: Áp dụng workflow skills (`planning-with-files`, `plan-writing`, `lint-and-validate`, `verification-before-completion`); tạo lại `task_plan.md` + `findings.md` cho phiên hiện tại.
2026-02-23: Sửa lint frontend: bỏ setState đồng bộ trong effect (`useTheme`), refactor guard bootstrap bằng `bootstrapInFlightRef` ở `RiskAlertsPage` để hết cảnh báo exhaustive-deps.
2026-02-23: Verification pass: `npm --prefix src/frontend run lint`; `npm --prefix src/frontend run test -- --run` (90/90); `npm --prefix src/frontend run build`; `dotnet test` Unit (116/116) + Integration (41/41).
2026-02-23: Đóng bead `cng-fwg` sau khi xác thực lại evidence; `bd ready --json` trả `[]`.
2026-02-23: Lập roadmap scale readiness theo yêu cầu user không chuyên kỹ thuật; tạo epic `cng-oiw` + 5 task con (`cng-oiw.1` -> `cng-oiw.5`) trong bead.
2026-02-23: Viết kế hoạch chi tiết `docs/plans/2026-02-23-scale-readiness-roadmap.md` và đồng bộ tracker vào `task.md` (Phase 67).
2026-02-23: Hoàn tất code scale readiness còn thiếu: thêm maintenance async queue + hosted worker + metrics queue/latency/outcome; thêm endpoint enqueue/status cho reconcile và retention.
2026-02-23: Bổ sung test mới `MaintenanceJobQueueTests`, `AdminMaintenanceEndpointsTests`; backend unit test pass `127/127`.
2026-02-23: Cập nhật tài liệu kỹ thuật liên quan thay đổi: `API_CONTRACT_NOTES.md`, `docs/API_CONTRACT_NOTES.md`, `RUNBOOK.md`, `DEPLOYMENT_GUIDE_DOCKER.md`, `docs/OPS_MONITORING_BASELINE.md`, cùng bộ docs hiệu năng (`QUEUE_WORKER_OPERATIONS`, `READ_REPLICA_ROUTING`, `AUTOSCALING_GUARDRAILS`).
2026-02-23: Chạy verification full sau khi hoàn tất triển khai: backend integration `41/41`, frontend lint/test/build/build:budget đều pass.
2026-02-23: Đóng toàn bộ beads scale readiness `cng-oiw.1` -> `cng-oiw.5` và epic `cng-oiw`; `bd ready --json` trả `[]`.
2026-02-24: Retry xử lý Opus V3; bổ sung `Codex Validation Addendum` vào `Opus_review_v3.md` để phân loại claim `OUTDATED/PARTIAL/CONFIRMED GAP` với evidence file-level.
2026-02-24: Cập nhật kế hoạch/remediation tracker cho `cng-rlx` (`opus-review-v3-remediation.md`) và đồng bộ sổ tay (`task.md`, `task_plan.md`, `findings.md`, `progress.md`).
2026-02-24: Chạy lại verification full trong cùng phiên: `dotnet build`, `dotnet test` Unit `127/127`, Integration `42/42`; `npm lint`, `npm test -- --run` `92/92`, `npm build` pass.
2026-02-24: Đủ điều kiện đóng bead `cng-rlx.1` -> `cng-rlx.5` và epic `cng-rlx`.


2026-02-25: Tiếp tục cng-los.1 (risk delta); hoàn tất test integration RiskDeltaAlertsTests (2/2) + unit RiskBootstrapEndpointTests (1/1), fix compile bằng thêm using CurrentUserAccessExtensions trong RiskService.Delta.
2026-02-25: Thêm integration tests `ReportScheduleServiceTests` (RunNow + RunDueSchedules) để xác nhận run logs, artifact metadata, notification `REPORT` và chỉ chạy lịch đến hạn.
2026-02-25: Thêm integration tests `ReminderEscalationPolicyTests` để xác nhận escalation theo mức 1->2->3 (owner/supervisor/admin) và cooldown skip (`COOLDOWN_ACTIVE`) không phát sinh notification mới.
2026-02-25: Thêm unit tests `ReportScheduleServiceTests` cho parse cron (5/6 token), timezone validation, và `CalculateNextRunUtc`.
2026-02-25: Verification pass: `dotnet test Tests.Unit --filter ReportScheduleServiceTests` (4/4) và `dotnet test Tests.Integration --filter "ReportScheduleServiceTests|ReminderEscalationPolicyTests"` (4/4).
2026-02-25: Tiếp tục `cng-los.2` (Global Search) và sửa flaky assertion của deep-link test: trường tìm chứng từ có info-tip nên nhãn thực tế chứa ký tự phụ, đổi query sang `getByRole('textbox', { name: /Tìm chứng từ \(PT \/ HD \/ TH\)/i })` để ổn định.
2026-02-25: Verification pass cho `cng-los.2`: `dotnet test ... --filter GlobalSearchServiceIntegrationTests` (2/2) và `npm --prefix src/frontend run test -- --run src/layouts/__tests__/app-shell.test.tsx src/pages/customers/__tests__/customers-modules.test.tsx` (11/11).
2026-02-26: Tiếp tục `cng-los.4` theo phần dở dang: hoàn tất Dashboard widget preferences (API+UI), thêm print action/layout cho Reports, bổ sung test backend/frontend tương ứng.
2026-02-26: Frontend lint fail do `react-hooks/set-state-in-effect` tại `AppShell.tsx` và `CustomersPage.tsx`; đã refactor để bỏ setState đồng bộ trong effect và giữ behavior cũ.
2026-02-26: Verification pass full Phase 69 completion: `dotnet build` pass; backend Unit `134/134`; backend Integration `52/52`; frontend `lint` pass; frontend `vitest --run` `99/99`; frontend `build` pass.
2026-02-26: Đồng bộ tracker Phase 69 trong `task.md` (đánh dấu hoàn tất `cng-los.3/.4/.5`, cập nhật evidence hoàn tất).
2026-02-26: Đóng beads `cng-los.3`, `cng-los.4`, `cng-los.5` và epic `cng-los`; `bd ready --json` trả `[]`.
2026-02-26: Tiếp tục bead `cng-d3e.2`; fix compile error `EnsureUser` trong `ReminderService.ResponseState.cs` bằng `using CongNoGolden.Infrastructure.Services.Common`.
2026-02-26: Chạy test targeted cho reminder flow: `dotnet test ... --filter "FullyQualifiedName~Reminder"` pass (`6/6`).
2026-02-26: Chạy frontend regression test cho Risk Alerts tabs: `npm --prefix src/frontend run test -- risk-alerts-page-tabs` pass (`1/1`).
2026-02-26: Đóng bead `cng-d3e.2` sau khi verification pass; trạng thái `CLOSED`.
2026-02-26: Tiếp tục `cng-d3e.3`; bổ sung 2 integration tests mới cho transition `DISPUTED` và `ESCALATION_LOCKED` trong `ReminderEscalationPolicyTests`.
2026-02-26: Chạy `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~ReminderEscalationPolicyTests"` pass (`6/6`).
2026-02-26: Chạy lại frontend targeted test `npm --prefix src/frontend run test -- risk-alerts-page-tabs` pass (`1/1`).
2026-02-26: Cập nhật `Opus_review_v3.md` để đóng claim residual reminder escalation (`PARTIAL` -> `OUTDATED (đã có 2026-02-26)`), đồng bộ `task.md`, `task_plan.md`, `findings.md`, `progress.md`.
2026-02-26: Đóng beads `cng-d3e.1`, `cng-d3e.3` và epic `cng-d3e`.
2026-02-26: Tiếp tục bead `cng-9y1`; bổ sung style `upload-dropzone--error` trong `src/frontend/src/index.css` để phản hồi trực quan khi file import không hợp lệ.
2026-02-26: Mở rộng `importBatchSection.dragdrop.test.tsx` với 3 case invalid file (sai định dạng `.txt`, quá `20MB` qua input, quá `20MB` qua drag-drop), xác nhận không gọi `uploadImport`.
2026-02-26: Verification frontend pass: `npm run test -- --run src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx` (`4/4`) và `npm run lint` pass.
