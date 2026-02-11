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
| What have I learned? | See findings.md |
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
