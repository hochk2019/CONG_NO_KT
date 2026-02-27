 # Chú ý:
 • Giữ trí nhớ xuyên suốt nhiều lượt trao đổi
 • Duy trì trạng thái công việc trong một “sổ tay” thống nhất
 • Tránh cảnh AI bị quên ngữ cảnh khi context bị nén hay reset
 • Không nồi quá nhiều code vào trong một module. Mỗi module không quá 800 dòng code nếu có thể.
 • Mỗi module mới đều cần tạo bản test. Nếu code chính thay đổi thì cũng cập nhật test cho phù hợp và ngược lại.

# TASKS - Cong No Golden

> [!IMPORTANT]
> Các phase có mốc ngày **<= 2026-01-29** là nhật ký lịch sử để truy vết.
> Nguồn vận hành hiện hành ưu tiên: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `docs/OPS_ADMIN_CONSOLE.md`.

## Phase 0 - Spec alignment (done)
- [x] Quy ước ADJUST lưu số âm + constraint DB.
- [x] Import RECEIPT included in scope (staging/preview/commit).
- [x] Thêm `idempotency_key` cho import commit.
- [x] Thêm cache `current_balance` (customer) + `outstanding_amount` (invoice/advance).
- [x] Ràng buộc allocation: invoice_id/advance_id + CHECK constraint.
- [x] Bổ sung `/health/ready`, monitoring note, offsite backup note.
- [x] Bổ sung Import History trong prototype.

## Phase 1 - Architecture & Design
- [x] `TECH_DECISIONS.md`: stack, ORM strategy, auth, logging/error format, trade-offs.
- [x] `MODULE_BOUNDARIES.md` + `CLEAN_ARCHITECTURE_MAP.md`.
- [x] `REPO_STRUCTURE.md` + `CODING_STANDARDS.md` + `REVIEW_CHECKLIST.md` (LOC/function limits).
- [x] `DB_REVIEW.md` + `INDEX_STRATEGY.md` + `MIGRATION_PLAN.md` (cache fields, allocation constraint, unaccent).
- [x] `UX_NAVIGATION_MAP.md` + `WIREFRAMES.md` + `ROLE_MATRIX_UI.md` (Import History, period lock, ownership).
- [x] `API_CONTRACT_NOTES.md`: mở rộng OpenAPI (schemas, required, max length, `version`).

## Phase 2 - Backend Core (API + DB)
- [x] Scaffold solution Clean Architecture (Api/Application/Domain/Infrastructure).
- [x] Health endpoints `/health` + `/health/ready` (DB check).
- [x] `RUN_BACKEND.md` + `ENV_SAMPLE.md` added.
- [x] Auth/RBAC/ownership; seed roles/users.
- [x] Import pipeline: upload → staging → preview → commit (idempotent) → rollback (Invoices/Advances/Receipts). (basic)
- [x] Import upload endpoint (creates STAGING batch + file_hash).
- [x] Import staging parser for INVOICE/ADVANCE/RECEIPT.
- [x] Import preview endpoint (paging + status counts).
- [x] Import commit endpoint (basic insert + period lock check).
- [x] Import rollback endpoint (soft delete + balance revert + lock check).
- [x] Receipt import: template parse + validations (staging).
- [x] Parse `ReportDetail.xlsx`, dedup, file_hash, warn duplicates (staging).
- [x] Update cache: `outstanding_amount` + `current_balance` on commit/approve/void.
- [x] AllocationEngine (BY_INVOICE/BY_PERIOD/FIFO) + preview endpoint.
- [x] Receipts allocation preview endpoint (API).
- [x] Receipts approve flow (allocations + balances).
- [x] Receipts approve transaction: allocations + unallocated_amount + cache updates.
- [x] Period lock enforcement (commit/approve/rollback + override + audit).
- [x] Reports endpoints + Excel export.
- [x] Reports summary endpoint (basic).
- [x] Reports statement + aging endpoints.
- [x] Audit log before/after.
- [x] Unit tests: AllocationEngine, import validators, period lock

## Phase 3 - Frontend (Web)
- [x] App shell + auth guard + permission guard.
- [x] Shared DataTable (server paging/filter/sort).
- [x] Import Wizard: upload/preview/commit + batch history + rollback (admin) + type select (Invoice/Advance/Receipt).
- [x] Receipts UI: 3 modes + allocation preview + applied_period_start.
- [x] Advances UI: approve/void theo ownership.
- [x] Customers UI: search MST/tên (unaccent + trigram) + detail tabs.
- [x] Reports UI + export download.
- [x] Admin UI: users/roles, period locks, audit viewer.

## Phase 4 - Hardening / QA / Deploy
- [x] Performance pass (EXPLAIN ANALYZE, dashboard <2s dataset lớn).
- [x] QA checklist + UAT script (edge cases từ review).

- [x] Add API smoke test script (health/auth/customers/logout).
- [x] Add deployment guide and IIS SPA fallback web.config.
- [x] Deploy theo `DEPLOYMENT_GUIDE_WEB_WINDOWS.md` + smoke test.
- [x] Add backup/restore helper scripts (pg_dump/pg_restore).
- [x] Backup/restore test + offsite copy.

## Phase 5 - Review Fixes (post-review)
- [x] Block import rollback if batch data is allocated or receipts are approved; return clear error.
- [x] Ensure receipt allocation ignores DRAFT/VOID advances and deleted receipts/invoices/advances.
- [x] Exclude soft-deleted advances in list endpoints.
- [x] Handle idempotency_key reuse on import batch creation (return existing or reject mismatch).
- [x] Fix admin roles update to be case-insensitive.
- [x] Align Admin Users UI visibility with Admin-only backend policy.
- [x] Add receipt draft create flow (API + UI) to remove manual receipt ID entry.
- [x] Decide auth strategy (access token in memory + refresh cookie) and align FE/BE/docs.
  - Add /auth/refresh + /auth/logout, refresh token table, FE auto refresh (no localStorage).
- [x] Implement optimistic concurrency (version checks) on approve/void/update actions.
  - Add version to advance/receipt approve/void + UI sends version.
- [x] Standardize API errors to ProblemDetails + error codes.
  - ApiErrors helper + FE parses detail/code.
- [x] Add migration tooling/scripts per MIGRATION_PLAN (DbUp + /scripts/db/migrations).
  - DbUp runner on startup + scripts 001-004 (refresh_tokens).
- [x] Allow migrations to use admin connection string; disable by default and document setup.
- [x] Normalize UI copy encoding and load intended fonts; wire dashboard CTA actions.
  - Load fonts in index.html; dashboard CTA + reports anchor scroll.
- [x] Add accent-insensitive customer search via name_search + trigram index.

## Nice-to-have backlog
- [x] Progress bar + lazy preview cho file import lớn.
- [x] Keyboard shortcuts (Enter/Esc/Arrows).
- [x] Dashboard trend chart 6 tháng.
- [x] Undo/Un-void (reversal) cho receipts/advances.

## Phase 17 - Opus 4.6 remediation (2026-02-11)
- [x] Xác thực nhận định Opus theo code thực tế, phân loại ĐÚNG/SAI/MỘT PHẦN.
- [x] Lập kế hoạch remediation chi tiết: `docs/plans/2026-02-11-opus-remediation.md`.
- [x] P1 security: thêm rate limiting cho `/auth/login` và `/auth/refresh`.
- [x] P1 security: thêm guard validate JWT secret (min length + chặn placeholder ngoài Development).
- [x] P1 security: harden refresh cookie (`SameSite`, `Path`) qua cấu hình.
- [x] P1 devops: thêm CI workflow (`.github/workflows/ci.yml`) cho backend/frontend.
- [x] P1 config: bổ sung `appsettings.Production.json` + cập nhật `ENV_SAMPLE.md`, `RUN_BACKEND.md`.
- [x] P2 data integrity: thêm reconcile job cho `current_balance`.
- [x] P2 reliability: thêm retry/backoff cho Zalo client.
- [x] P2 UX: cải thiện responsive nav (collapse/hamburger rõ ràng).
- [x] P3 refactor part 1: tách helper user-context dùng chung (`EnsureUser`, `ResolveOwnerFilter`) và áp dụng cho Risk/Reminder services.
- [x] P3 refactor part 2: tiếp tục gom helper trùng lặp cho Advance/Receipt/Dashboard/PeriodLock.
- [x] P3 refactor part 3: đánh giá và giảm API round-trips dashboard/risk/reports (composite endpoint khi phù hợp). *(Dashboard 3→2 call; Reports gộp KPI/charts/insights qua `/reports/overview`; Risk thêm `/risk/bootstrap` để gom dữ liệu khởi tạo).*

## Phase 18 - Opus follow-up hardening (2026-02-11)
- [x] Lập kế hoạch triển khai follow-up: `docs/plans/2026-02-11-opus-follow-up-hardening-plan.md`.
- [x] P1 security: externalize JWT secret hoàn toàn cho production + fail-fast guard.
- [x] P1 security: bổ sung password complexity policy + absolute expiry cho refresh token.
- [x] P1 data integrity: thêm explicit transaction cho luồng `ReceiptService.ApproveAsync`.
- [x] P1 frontend performance: route-level code splitting (`React.lazy` + `Suspense`) cho trang nặng.
- [x] P2 refactor: rà soát service phụ trợ còn helper trùng lặp và migrate sang `CurrentUserAccessExtensions`.
- [x] P2 observability: thiết lập baseline metrics/tracing + readiness diagnostics.
- [x] Cập nhật lại `Opus_4.6_review.md` sau mỗi nhóm thay đổi để tránh trạng thái mâu thuẫn.

### Deferred/Out of scope cho Phase 18 (có lý do)
- [x] Zalo circuit breaker nâng cao — đã triển khai threshold + cooldown có cấu hình (2026-02-11).
- [x] IP/device binding cho refresh token — đã triển khai dual-signal binding (device fingerprint + IP prefix) với rule giảm false-positive (2026-02-11).
- [x] DB partition/retention automation — hoàn tất trong Phase 28: migration `022_audit_logs_partition.sql` + auto-ensure partition theo tháng (2026-02-12).
- [x] Containerization — hoàn tất trong Phase 28: Dockerfile backend/frontend + compose + env mẫu + guide (2026-02-12).
- [x] AI risk scoring — ban đầu out of scope của hardening Phase 18; đã triển khai ở Phase 50 (baseline) và Phase 51 (full ML pipeline).

## Phase 27 - Opus deferred follow-up execution (2026-02-11)
- [x] Tạo bead epic + task con cho 3 hạng mục deferred cần thực hiện (`cng-9ww`, `.1`, `.2`, `.3`).
- [x] Auth hardening: thêm context binding cho refresh token (IP prefix + device fingerprint hash) và chặn khi lệch đồng thời cả 2.
- [x] Zalo reliability: thêm circuit breaker (failure threshold + cooldown) bên cạnh retry/backoff.
- [x] Data lifecycle: thêm data retention service + hosted scheduler + admin trigger `/admin/health/run-retention`.
- [x] Cập nhật migration scripts cho refresh token binding + retention indexes (`020`, `021`).
- [x] Bổ sung/điều chỉnh test cho AuthService, ZaloClient, DataRetentionService.
- [x] Đồng bộ trạng thái vào `Opus_4.6_review.md`, `task.md`, beads.

## Phase 28 - Opus remaining execution (2026-02-12)
- [x] Lập kế hoạch chi tiết vòng triển khai: `docs/plans/2026-02-12-opus-remaining-execution-plan.md`.
- [x] Tạo bead epic + task con cho phần còn lại (`cng-jib`, `.1`, `.2`, `.3`, `.4`).
- [x] DB hardening: triển khai partition theo tháng cho `congno.audit_logs` + migration an toàn dữ liệu hiện hữu.
- [x] Containerization: thêm Dockerfile backend/frontend + `docker-compose.yml` + `.dockerignore` + hướng dẫn chạy/deploy.
- [x] Ops update: thêm hỗ trợ runtime Docker trong Ops Agent/Console (status/start/stop/restart) song song mode Windows Service.
- [x] Đồng bộ trạng thái vào `Opus_4.6_review.md`, `docs/OPS_ADMIN_CONSOLE.md`, `task.md`, beads.

### Không triển khai trong Phase 28 (có lý do)
- [x] AI risk scoring / dự báo trễ hạn (đã chuyển sang Phase 50 + 51 và hoàn tất).
  - Ghi chú: tách riêng khỏi hardening vòng 28 để giảm rủi ro, sau đó triển khai đầy đủ theo phase riêng.

## Phase 29 - Ops Docker quick setup (2026-02-12)
- [x] Thêm script chuyển runtime Ops Agent sang Docker mode (`scripts/ops/set-runtime-mode.ps1`).
- [x] Thêm script tạo/cập nhật profile Ops Console từ agent config (`scripts/ops/set-console-profile.ps1`).
- [x] Cập nhật `docs/OPS_ADMIN_CONSOLE.md` với quy trình quick setup Docker runtime.

## Phase 30 - Zalo + E2E stabilization (2026-02-12)
- [x] E2E: sửa `dashboard.spec.ts` theo heading mới `Tổng quan công nợ`.
- [x] E2E: thêm `VITE_API_PROXY_TARGET` vào `vite.config.ts` + `playwright.config.ts` để chạy đúng backend Docker (`127.0.0.1:18080`).
- [x] E2E: harden helper đăng nhập `loginAsDefaultUser` (xác nhận shell đăng nhập thật + retry/backoff khi 429).
- [x] E2E: sửa selector `receipts-flow.spec.ts` để tránh strict-mode violation (scope vào card `Thông tin phiếu thu`).
- [x] Zalo Docker: map đủ env `Zalo__OaId`, `Zalo__ApiBaseUrl`, `Zalo__AccessToken`, `Zalo__WebhookToken` trong `docker-compose.yml` + `.env.docker.example` + guide.
- [x] Zalo local smoke: `link/request` -> webhook `LINK <code>` -> `link/status` linked=true (đóng bead `cng-j1t.3`).
- [x] Zalo OA thật: cấu hình AccessToken/WebhookToken/OaId môi trường staging/prod (`cng-j1t.1`) — tạm đóng 2026-02-13 theo quyết định chưa có tài khoản OA.
- [x] Zalo OA thật: khai báo callback webhook từ OA dashboard (`cng-j1t.2`) — tạm đóng 2026-02-13 theo quyết định chưa có tài khoản OA.
- [x] Zalo OA thật: chạy `/reminders/run` và xác nhận log `SENT` qua OA thật (`cng-j1t.4`) — tạm đóng 2026-02-13 theo quyết định chưa có tài khoản OA.
- [x] Verify runtime local (2026-02-13): `docker compose config` map đủ biến `Zalo__*`; container API đang chạy với `Zalo__Enabled=false`, `Zalo__OaId/AccessToken/WebhookToken` rỗng.
- [x] Verify endpoint local (2026-02-13): `/health`, `/health/ready`, `/webhooks/zalo` trả `200`; `/zalo/link/status` đang `linked=true`.
- [x] Verify reminder local (2026-02-13): chạy `/reminders/run` trả `totalCandidates=22`, `sentCount=2`, `failedCount=0`, `skippedCount=42`; filter log `channel=ZALO,status=SENT` hiện `0`.
- [x] E2E smoke (2026-02-13): `npx playwright test e2e/auth-login.spec.ts e2e/dashboard.spec.ts e2e/receipts-flow.spec.ts` => 2 passed, 1 skipped.
- [x] Full E2E regression (2026-02-13): ổn định E2E auth bằng cache `congno_refresh` giữa test + retry/backoff khi 429 trong `e2e/support/auth.ts`, cấu hình `workers: 1`; chạy `npx playwright test` => `11 passed, 1 skipped`.





## Phase 6 - UAT fixes (2026-01-08)
- [x] Fix customer search 500 (move unaccent into EF expression).
- [x] Return 204 for admin roles/status update to avoid FE JSON parse error.
- [x] Receipt approve: validate BY_INVOICE requires targets + hint in UI.
- [x] Receipt current_balance uses full receipt amount (credit handling) + restore on void.
- [x] Add import templates (invoice/advance/receipt) + download links in Import UI.
- [x] Standardize UI copy to Vietnamese UTF-8 (AppShell, Login, Customers, Advances, Receipts, Imports, Reports, Admin, Errors, DataTable).

## Phase 7 - UX & Import improvements (2026-01-09)
- [x] Add Admin Users create form in frontend (create user + roles + status).
- [x] Improve import upload error messaging + update import templates doc.
- [x] Replace invoice template with simplified parser-friendly format.
- [x] Add usage hints for Receipts/Imports and Vietnamese labels for dropdown/status.
- [x] Fix admin user create FK insert order (save user before user_roles).
- [x] Invoice import: prioritize ExportData sheet + fallback parser for simple template.
- [x] Skip duplicate invoices on commit to avoid uq_invoices_dedup error.
- [x] Mark duplicates vs DB in staging and show reasons in preview.

## Phase 8 - Ops Mission Control (2026-02-08)
- [x] Ops Agent: system metrics, app pool control, maintenance mode, compression, backup schedule/run-now, backend config/recovery, log level/jobs, SQL console.
- [x] Ops Shared: console profiles, new DTOs, AgentClient SQL methods.
- [x] Ops Console: Mission Control layout + profiles + advanced mode + deploy/services/frontend/backend/DB panels.

## Phase 8 - Reports pagination & sorting (2026-02-04)
- [x] Fix paged SQL for reports (summary/statement/aging) + integration tests.
- [x] Add pagination controls + per-section page size persistence for Reports tables.
- [x] Add sort dropdowns (Summary/Aging) and Top cần chú ý count selector (persisted).
- [x] Update Reports copy (empty states, export button labels, summary column headers).

## Phase 8 - UX follow-up (2026-01-10)
- [x] Add lookup endpoints (sellers/customers/owners) + customer owner update API.
- [x] Add searchable MST dropdowns in Advances/Receipts/Reports and owner select for filters.
- [x] Add customer owner assignment UI + status filter dropdowns in customer detail tabs.
- [x] Auto refresh import preview when changing filter status.
- [x] Prevent layout overlap by wrapping input rows and grid fields.

## Phase 9 - UX + Workflow fixes (2026-01-11)
- [x] Chuẩn hóa tiếng Việt cho AppShell, Login, Dashboard, Customers, Advances, Receipts, Imports, Reports, DataTable.
- [x] Đổi nhãn "Tạm ứng" thành "Khoản trả hộ KH" và cập nhật nội dung liên quan.
- [x] Thêm danh sách phiếu thu + chọn nhanh để duyệt/hủy.
- [x] Thêm luồng hủy hóa đơn (yêu cầu hóa đơn thay thế khi đã thu tiền) + cập nhật công nợ.
- [x] Bổ sung hướng dẫn nhập liệu (Import/Receipts) và mô tả thuật ngữ trên UI.
- [x] Giữ phiên đăng nhập bằng refresh cookie + cấu hình proxy `/api` cho môi trường dev.

## Phase 10 - UI/UX polish (2026-01-15)
- [x] A11y: lang=vi, alert aria-live, focus-visible styles, table sort aria.
- [x] Route/role-aware AppShell header + CTA anchors for imports/admin.
- [x] Visual refresh: palette, button states, charts/legend colors, table responsiveness.

## Phase 10 - UI/UX Redesign (ordered: hard -> easy)
- [x] P0 Table system + data density
  - [x] Chuan hoa DataTable semantic (table/thead/tbody/th) + aria-sort + focus.
  - [x] Dong bo table tu tao (Reports/Receipts/Imports/Admin Period Locks) ve dung component.
  - [x] Sticky header + row hover + empty state chuan.
  - [x] Them row state (selected/active) neu can.
  - [x] Kiem tra scroll ngang cho bang rong (desktop-first).
- [x] P0 Layout hierarchy + global header
  - [x] Chon 1 cap header (AppShell hoac page header) va bo trung lap.
  - [x] Dinh nghia CTA theo role cho cac man hinh chinh.
  - [x] Them breadcrumb hoac context label neu can.
- [x] P1 Alerts + destructive actions
  - [x] Tach alert theo loai (success/warn/error/info) + mau/role khac nhau.
  - [x] Them nut danger + disabled states ro rang.
  - [x] Them confirm flow cho rollback/void/unlock.
- [x] P1 Forms UX
  - [x] Dung input types + autocomplete day du (email/tel/number/date).
  - [x] Gom truong nang cao (idempotency/override) vao Advanced panel.
  - [x] Validate on blur + thong bao loi tai field.
- [x] P2 Dashboard clarity
  - [x] Bo sung nhan gia tri/legend ro rang cho chart.
  - [x] The overdue highlight + last updated time.
- [x] P2 Reports UX
  - [x] Filter summary chip + preset theo ky.
  - [x] Export queue/progress state.
- [x] P2 Typography + spacing scale
  - [x] Dinh nghia scale (H1/H2/H3/body/caption) + ap dung.
  - [x] Chuan hoa spacing giua card/section.

## Phase 11 - Dashboard & UX Redesign (2026-01-16)
- [x] P0 Dashboard layout & flow
  - [x] Sap xep lai bo cuc theo tuyen thao tac: KPI -> canh bao -> xu huong -> phan tich chi tiet.
  - [x] Them khoi "Can xu ly" (phieu thu cho duyet, lo nhap cho ghi, khoa ky) va "Qua han" o tren.
  - [x] Them split view cho Customers/Receipts (list + detail) va auto-scroll/anchor khi chon dong.
- [x] P0 Information density (desktop-first)
  - [x] Che do compact cho bang/form (giam padding, font-size 0.9rem, row height 36-40px).
  - [x] Can doi kich thuoc input/select/CTA theo muc do quan trong.
- [x] P1 Color system & contrast
  - [x] Giam gradient nen, giu accent gradient chi o hero; nen phang trung tinh.
  - [x] Co dinh palette: trust blue (primary) + teal (success) + amber (warning) + red (danger).
  - [x] Tang do doc table head (bo uppercase, tang font-size).
- [x] P1 Language polish (Vietnamese)
  - [x] Chuan hoa thuat ngu: override -> "vuot khoa ky", idempotency -> "khoa chong trung", audit log -> "nhat ky he thong".
  - [x] Dong bo nhan (Duyet/ Phe duyet/ Huy/ Hoan tac) va thong diep huong dan.
- [x] P2 Backend support (if needed)
  - [x] Bo sung dashboard summary "action-required" (pending approvals/locks/imports) de sap xep khoi can xu ly.
  - [x] Bo sung timestamp "last_updated" va endpoint thong ke cho "qua han" theo nganh/nhom.

## Phase 12 - Risk alerts & reminders (2026-01-16)
- [x] DB migration risk_rules/reminder_settings/reminder_logs/notifications (007).
- [x] Risk service + endpoints (/risk/overview, /risk/customers, /risk/rules).
- [x] Reminder settings/logs/run + scheduler (auto-run).
- [x] In-app notifications list + mark read endpoint.
- [x] Zalo client stub + config (tạm thời).
- [x] Risk Alerts UI + navigation + styles.
- [x] Default reminder scope: RẤT CAO/Cao/Trung bình.
- [x] Xác nhận Zalo OA endpoint/payload + template nội dung (OA id: 2804410978830725257).
- [x] Tích hợp Zalo user_id: webhook liên kết + UI tạo mã liên kết.
- [x] Thêm nút "Liên kết Zalo" trong Admin Users để nhập thủ công.
- [x] Chạy migration mới cho Zalo link tokens + users.zalo_user_id (008_zalo_link.sql).

## Phase 13 - Backup/Restore UI + Scheduler (2026-01-30)
- [x] DB migration backup_settings/backup_jobs/backup_audit/backup_uploads.
- [x] Backend services: backup/restore + scheduler + queue + maintenance mode.
- [x] Backup admin endpoints + auth policies.
- [x] Frontend admin page: settings, manual backup, jobs, restore, audit.
- [x] Tests: backend unit + frontend.
- [x] Cấu hình Zalo OA sau khi duyệt: AccessToken, WebhookToken, Enabled=true, kiểm tra OaId. (tạm đóng 2026-02-13 do chưa có tài khoản OA)
- [x] Khai báo webhook OA trỏ `/webhooks/zalo?token=...` và xác nhận callback. (tạm đóng 2026-02-13 do chưa có tài khoản OA)
- [x] Test liên kết: tạo mã → nhắn "LINK <code>" → kiểm tra user_id đã lưu (đã xác nhận lại ở Phase 30).
- [x] Test gửi nhắc Zalo: chạy `/reminders/run` và kiểm tra log trạng thái SENT. (tạm đóng 2026-02-13 do chưa có tài khoản OA)
- [x] Chạy migration và kiểm tra bảng: risk_rules, reminder_settings, reminder_logs, notifications.

## Phase 13 - Import lifecycle UX (2026-01-17)
- [x] Cho phép xem lịch sử nhập liệu với role Accountant (ImportBatchList).
- [x] Bổ sung nút "Tiếp tục" cho lô STAGING + anchor đúng về "Lịch sử nhập".
- [x] Bổ sung hủy lô STAGING (API + UI) + lưu metadata hủy.
- [x] Thêm cột/trường cancel: cancelled_at/cancelled_by/cancel_reason cho import_batches.
- [x] Chạy migration 009_import_cancel.sql và 010_import_status_cancelled.sql, xác nhận cột + check constraint.
- [x] Tự động dọn import_staging_rows cũ theo TTL.
- [x] Hiển thị rõ lý do không thể hoàn tác (approved/allocated/khóa kỳ) trên UI.

## Phase 14 - Đồng bộ dữ liệu FE/BE/DB (2026-01-19)
### P1 - Ưu tiên cao (triển khai trước)
- [x] Chuẩn hóa spec DB: cập nhật `extracted_specs/db_schema_postgresql.sql` (CANCELLED + cancel fields + trạng thái).
- [x] UI Risk Rules: nhập tỷ lệ quá hạn theo % (0-100) và tự convert sang 0..1 khi lưu.
- [x] Import History: hiển thị lý do hủy + người hủy + thời điểm hủy cho batch CANCELLED.

### P2 - Ổn định hợp đồng dữ liệu
- [x] OpenAPI + generate types cho FE để tránh drift payload/field.
- [x] Integration tests cho: import cancel/rollback/commit, risk rules update, reminder run/log.
- [x] Chuẩn hóa error codes + mapping FE message cho các flow nhập liệu/risk/reminder.

### P3 - Hiệu năng & vận hành
- [x] Bổ sung index cho `import_batches(status, created_at)`, `reminder_logs(owner_user_id, created_at, status, channel)`, `import_staging_rows(batch_id)`.
- [x] Audit log chi tiết cho thao tác nhạy (hủy lô, vượt khóa kỳ, cập nhật rule).
- [x] Trang admin “health/sync” để rà drift dữ liệu giữa FE/BE/DB.

## Phase 15 - Customers transactions refactor (2026-01-19)
### P0 - DB schema
- [x] Thêm `sellers.short_name` + seed short name cho MST hiện có (Hoàng Minh/Hoàng Kim).
- [x] Thêm `advances.advance_no` + `receipts.receipt_no` (số chứng từ) + index tìm kiếm.
- [x] Cập nhật `extracted_specs/db_schema_postgresql.sql` + migration mới.
- [x] Quy ước: nếu chưa có số chứng từ thì UI fallback về `advanceNo/receiptNo` (không dùng id).

### P1 - Backend APIs
- [x] Mở rộng `CustomerRelationRequest` với `docNo`, `receiptNo`, `from`, `to`.
- [x] Customers API: lọc theo số chứng từ/phiếu thu + khoảng thời gian trên 3 tab.
- [x] Customers API: trả thêm `seller_short_name`, `document_no` + `receipt_refs` (PAID/PARTIAL).
- [x] Thêm endpoint xem allocations của phiếu thu (Invoices/Advances đã phân bổ).
- [x] Advances/Receipts create/update: nhận `advance_no`/`receipt_no`.

### P2 - Frontend UX
- [x] Tab Giao dịch KH: thêm tìm số chứng từ + số phiếu thu + khoảng thời gian + quick range.
- [x] Thu nhỏ dropdown trạng thái để đủ chỗ.
- [x] Cột “Thao tác”: Xem + Hủy (popup); bỏ panel “Hủy hóa đơn” phía dưới.
- [x] Thêm cột “Phiếu thu” (hiển thị số + mở modal xem) cho Hóa đơn/Khoản trả hộ KH.
- [x] Hiển thị “Bên bán” = MST + (short name) dòng dưới (lấy từ DB).
- [x] Tab Phiếu thu: nút Xem → popup allocations (modal vừa).
- [x] Bổ sung trường “Số chứng từ” tại trang Advances/Receipts.
- [x] Hoàn tất modal Xem/Hủy (Hóa đơn/Khoản trả hộ) + modal allocations phiếu thu.
- [x] Tinh chỉnh CSS: quick range, chip phiếu thu, action buttons.

### P3 - Optional follow-up
- [x] Cập nhật template import (advance/receipt) để có cột số chứng từ.

## Phase 16 - Customers module refactor + tests (2026-01-19)
- [x] Tách Customers page thành module nhỏ (CustomersPage, CustomerListSection, CustomerEditModal, CustomerTransactionsSection).
- [x] Chuẩn hóa layout 1 cột + điều chỉnh độ rộng cột danh sách khách hàng.
- [x] Tách logic giao dịch khách hàng thành các module con (TransactionFilters, transactionColumns, useReceiptModal).
- [x] Bổ sung hạ tầng test FE (Vitest + RTL + JSDOM) + test cho module mới.

## Phase 17 - Import template header safety (2026-01-20)
- [x] Không fallback về cột 1 khi thiếu header (ImportTemplateParser/ImportInvoiceTemplateParser).
- [x] Cho phép ParseDate/ParseDecimal nhận null và trả về rỗng an toàn.
- [x] Thêm unit tests đảm bảo thiếu header → báo lỗi bắt buộc (receipt/invoice).

## Phase 18 - E2E coverage (2026-01-20)
- [x] E2E: Customers page load + filters visible.
- [x] E2E: Imports page upload template + hiển thị mã lô.

## Phase 19 - Gộp Advances vào Imports (2026-01-20)
### P0 - IA/UX (trải nghiệm)
- [x] Thiết kế tab trong Imports: "Nhập file" + "Nhập thủ công (Khoản trả hộ KH)".
- [x] Giữ trạng thái tab (localStorage) + URL query `?tab=manual`.
- [x] Tối ưu bố cục tab thủ công: form tạo mới ở trên, danh sách + filter ở dưới (compact).
- [x] Chuẩn hóa CTA: mặc định ưu tiên "Tạo & duyệt" (nếu có quyền), "Tạo nháp" là lựa chọn phụ.
- [x] Giữ bộ lọc tab thủ công tương đương Advances cũ, tối ưu bố cục/nhãn nếu cần.
- [x] Thêm bộ lọc nâng cao: số chứng từ, khoảng ngày, số tiền, nguồn dữ liệu (manual/import).
- [x] Validate khoảng ngày/số tiền khi lọc (from <= to, min <= max).
- [x] Giảm tải nhận thức: mô tả ngắn, helper text, trạng thái rõ ràng.

### P1 - Frontend (refactor & routing)
- [x] Tách ImportsPage thành module nhỏ (không quá 800 LOC):
  - [x] `ImportsPage` (wrapper + tabs + route state)
  - [x] `ImportBatchSection` (upload/preview/commit/history)
  - [x] `ManualAdvancesSection` (từ AdvancesPage, simplified)
- [x] Bỏ menu “Khoản trả hộ KH” trong sidebar + breadcrumb.
- [x] Xóa route `/advances` khỏi App routes và thay bằng redirect → `/imports?tab=manual` để tránh link cũ lỗi.
- [x] Cập nhật Dashboard CTA/links trỏ về `/imports?tab=manual`.
- [x] Cập nhật tests (Playwright) cho tab manual + lọc nâng cao + redirect /advances.
- [x] Fix trang Imports trắng do DataTable thiếu getRowKey (ImportHistorySection).

### P2 - Backend/API
- [x] Bổ sung `source_type` (manual/import) trong AdvanceList API (derive từ `source_batch_id`) hoặc thống nhất derive tại FE.
- [x] (Optional) thêm `source_batch_id` vào AdvanceList response để link tới Import History.
- [x] Cập nhật OpenAPI schema + regenerate types FE.

### P3 - Database (nếu cần)
- [x] Không bắt buộc thay đổi schema (dùng `source_batch_id` để suy luận).
- [x] Nếu cần báo cáo sâu: cân nhắc thêm `source_type` stored/enum (đánh giá sau, hiện chưa cần).

### P4 - Simplify manual advances UX
- [x] Rút gọn hành động: ưu tiên "Tạo", "Tạo & duyệt" (auto approve), "Hủy" rõ ràng.
- [x] Hạn chế luồng phụ: override kỳ khóa chỉ hiện khi cần (collapse/confirm).
- [x] Cột “Nguồn” hiển thị rõ: Thủ công/Import (pill) + batch id (nếu có).

## Phase 20 - Receipts refactor (UX + RBAC + Allocation) (2026-01-20)
### P0 - Business rules (chuẩn hóa luồng)
- [x] Khi chọn khách hàng ở Receipts → bắt buộc phân bổ nếu có open-items (hóa đơn/khoản trả hộ chưa/đang thanh toán).
- [x] Nếu không có open-items → cho phép tạo phiếu thu treo (unallocated) và auto-allocate về sau.
- [x] Auto-allocate mặc định theo **ngày chứng từ** (issue_date) cũ hơn trước; nếu cùng ngày ưu tiên **Hóa đơn**.
- [x] Cho phép user chọn “Ưu tiên theo” trong popup (ngày chứng từ/ngày đến hạn); tie-break ưu tiên hóa đơn.
- [x] Auto-allocate **không tự duyệt** → trạng thái chờ duyệt.
- [x] Nhắc duyệt mỗi 10 ngày nếu: (a) auto-allocated chưa duyệt, (b) phiếu thu treo chưa được phân bổ.
- [x] Cho phép người dùng tắt nhắc duyệt theo từng phiếu thu (per-receipt).

### P1 - Backend/API
- [x] Endpoint open-items: `GET /api/receipts/open-items?customerId=...` trả 1 bảng chung (Invoices + Advances) có cột `type`.
- [x] Receipt create/update: nhận `allocations[]` + validate bắt buộc nếu có open-items.
- [x] Receipt auto-allocate service (job/trigger) theo rule mặc định + lưu audit.
- [x] Receipt approval: xử lý phân bổ + balances + allocation_status.
- [x] Cancel receipt: cho phép hủy mọi trạng thái, bắt buộc lý do; rollback phân bổ + balances + audit.
- [x] RBAC: Accountant create/approve/cancel **chỉ** khách hàng mình phụ trách; Supervisor/Admin full; Viewer read-only.
- [x] Notifications: gửi cho user + supervisor khi auto-allocate & khi cần duyệt (10 ngày).
- [x] OpenAPI cập nhật + regenerate types FE.

### P2 - Database
- [x] Bổ sung/chuẩn hóa receipt allocations + allocation_status + unallocated_amount.
- [x] Lưu trạng thái nhắc duyệt (last_reminder_at/disabled_reminder_at) cho receipt.
- [x] Dùng `customers.accountant_owner_id` cho phân công Accountant (không thêm bảng mới).

### P3 - Frontend UX (modular)
- [x] Refactor Receipts page thành module nhỏ (<800 LOC): `ReceiptFormSection`, `ReceiptAllocationModal`, `ReceiptListSection`.
- [x] Popup phân bổ 1 bảng chung (có cột Loại), tổng phân bổ rõ ràng, bắt buộc đủ số tiền.
- [x] CTA rõ: “Lưu nháp” + “Lưu & duyệt”.
- [x] Hiển thị trạng thái treo/đã phân bổ/chờ duyệt, nhắc duyệt 10 ngày, nút tắt nhắc.
- [x] Hủy phiếu thu trong danh sách (cột Thao tác) + popup lý do bắt buộc.
- [x] Điều hướng/UX: trạng thái chọn khách hàng rõ ràng, focus & hint.

### P4 - Tests
- [x] Unit tests cho allocation rules (issue_date, due_date, tie-break invoice trước).
- [x] Integration tests: create/approve/cancel receipts + open-items API + RBAC.
- [x] E2E: chọn KH → popup phân bổ → lưu nháp/duyệt → hủy → nhắc duyệt.

## Phase 21 - Notification Center (2026-01-23)
### P0 - Data + API
- [x] Thêm bảng `notification_preferences` (receive_notifications, popup_enabled, popup_severities, popup_sources).
- [x] Thêm API: `GET /notifications/unread-count`, `POST /notifications/read-all`.
- [x] Thêm API preferences: `GET/PUT /notifications/preferences`.
- [x] Cập nhật ReminderService/ReceiptAutomationService tôn trọng preferences (Admin có thể tắt nhận).
- [x] Cập nhật OpenAPI + regenerate FE types.

### P1 - Frontend UI/UX
- [x] Bell + badge trong AppShell, panel quick view (chưa đọc, xem tất cả, đánh dấu đã đọc).
- [x] Trang Trung tâm thông báo: lọc nguồn/mức độ/trạng thái + tìm kiếm + panel chi tiết.
- [x] Hệ thống toast/modal: INFO toast ngắn, WARN toast lớn, CRITICAL modal (1 lần/phiên).
- [x] Cài đặt popup theo loại (severity/source) và bật/tắt nhận thông báo.
- [x] Ghi chú: thông báo RISK chịu ảnh hưởng cài đặt trang Cảnh báo rủi ro.

### P2 - Tests
- [x] Backend tests: unread-count, read-all, preferences + recipients filter.
- [x] Frontend tests: bell badge, quick panel, settings, toast/modal rendering.

## Phase 22 - Notification reminders & partial allocation (2026-01-24)
### P0 - Reminder rules
- [x] Thêm `upcoming_due_days` vào reminder_settings (migration 016).
- [x] Nhắc sắp đến hạn: WARN, gửi kế toán phụ trách + Supervisor, mặc định 7 ngày, cấu hình được (1-30).
- [x] UI Risk Alerts: nhập/sửa “Nhắc sắp đến hạn (ngày)” và lưu vào settings.
- [x] Thông báo “phiếu thu phân bổ một phần” (AllocationStatus=PARTIAL) gửi WARN cho Supervisor + kế toán phụ trách + người tạo phiếu thu.

### P1 - QA runs
- [x] dotnet test backend (unit + integration) OK.
- [x] Frontend lint OK.
- [x] Frontend unit tests OK.
- [x] Frontend e2e OK (2 skipped theo cấu hình).

## Phase 23 - Reports refactor (2026-01-27)
### P0 - Backend
- [x] Điều chỉnh logic “đúng hạn” theo phát sinh trong kỳ (không lọc due_date).
- [x] Thêm endpoints `/reports/kpis`, `/reports/charts`, `/reports/insights`.
- [x] Thêm endpoints `/reports/preferences` (GET/PUT) theo user.
- [x] Bổ sung sheet “TongQuan” khi export (KPIs + insights).
- [x] Refactor template Excel export: bố cục TongQuan + header bands + table styles + số liệu không bị cắt.
- [x] Chuẩn hóa format tiền/đếm + auto-adjust row height/column width cho TongHop/ChiTiet/Aging.
- [x] Tách template Excel theo từng loại báo cáo (TongQuan/TongHop/ChiTiet/Aging) + param `kind` cho export.

### P1 - Frontend
- [x] Refactor ReportsPage thành module nhỏ (Filters/KPIs/Charts/Insights/Tables).
- [x] Lưu cấu hình KPI order + dueSoonDays theo DB.
- [x] Bố cục insights: Top cần chú ý (trái), Top trả đúng hạn + Quá hạn theo phụ trách (phải).
- [x] Bổ sung CSS cho reports layout mới.
- [x] Cho phép tải sao kê khi chưa chọn MST (cảnh báo + vẫn tải theo kỳ).

### P2 - Tests
- [x] FE tests cho modules Reports.
- [x] BE integration tests cho report preferences.
- [x] Unit test template xuất Excel (overview layout + styles).
- [x] Chạy dotnet test + frontend tests + e2e (theo yêu cầu kiểm thử sau cùng).

## Phase 24 - Post-review fixes (2026-01-28)
- [x] Fix apiFetch double JSON stringify + hỗ trợ AbortSignal.
- [x] Thêm validator ngày báo cáo (from/to, asOfDate) + unit tests.
- [x] Chặn tải báo cáo khi thiếu khoảng thời gian và đồng bộ FE guard + export aging.
- [x] Chuẩn hóa toDateInput theo local date (Reports + Risk Alerts).
- [x] Refactor ReportsPage utils để dưới 800 LOC + test reportUtils.
- [x] Refactor RiskAlertsPage layout thành sections (<800 LOC) + test sections.
- [x] Guard migration xóa KH test chỉ chạy khi app.env=dev.
- [x] MigrationRunner fail fast khi thiếu thư mục scripts.

## Phase 25 - Full system review (2026-01-29)
- [x] FE Dashboard: chuẩn hóa toDateInput theo local date + quick actions dùng Link.
- [x] FE Notifications: tránh refetch khi chọn thông báo + CTA dùng Link + list row dùng button.
- [x] Review template import/export Excel (headers/sheet).
- [x] Review BE services + migrations (ghi nhận issue nếu có).
- [x] Review UI pages chính (Dashboard/Customers/Imports/Receipts/Admin/Notifications).
- [x] Fix ReportService.Aging: loại VOID/future docs + add ReportAgingTests.
- [x] UI guideline fixes: ImportPreviewModal keyboard/ellipsis + AdminUsersPage autocomplete/name.
- [x] Batch ReceiptAutomationService open-item loads per seller/customer + integration test (query count).
- [x] Refactor modal backdrops to accessible scrim buttons + add/adjust FE tests.

## Phase 6 - Ops Admin Console
- [x] Add ops solution (Agent + Console + Shared + Tests).
- [x] Agent endpoints: health/config, services, backup/restore, logs tail, update.
- [x] WPF Console UI for ops tasks (services, backup, config, logs, updates).
- [x] Ops docs (OPS_ADMIN_CONSOLE.md).
- [x] Ops scripts: publish/install agent + cập nhật hướng dẫn.
- [x] Ops hardening: backup/restore connection, update safety, service env, DB probe, log tail streaming.
- [x] Ops prod hardening: add Serilog file logging + update deployed appsettings/agent-config.
- [x] Fix UpdateRunner preserve across volumes (avoid Directory.Move cross-drive failure).
- [x] Ops Console: Endpoints tab (Open URLs + IIS binding).

## Phase 26 - Auto allocate receipt credits to advances (2026-02-03)
- [x] Auto-allocate overpaid receipts to newly approved advances (manual + import).
- [x] Auto-allocate overpaid receipts to newly imported invoices.
- [x] Backfill/reconcile existing credits (optional admin action).

## Phase 27 - Invoice credit reconcile scheduler (2026-02-03)
- [x] Add periodic reconcile job to allocate receipt credits to open invoices.
- [x] Add integration test for reconcile service.

## Phase 28 - Notification improvements (2026-02-04)
- [x] Align ALERT severity defaults across FE/BE + treat ALERT as critical modal.
- [x] Add IMPORT commit notification and SYSTEM alert on backup failure.
- [x] Refresh unread badge after marking notifications read.

## Phase 29 - Notifications UX polish (2026-02-04)
- [x] Click row auto-marks read + keeps list highlight.
- [x] Move detail view to modal via “Xem chi tiết”, keep settings panel clean.

## Phase 30 - Sidebar branding meta (2026-02-04)
- [x] Tăng cỡ chữ Golden Logistics ở sidebar.
- [x] Thêm phiên bản, bản quyền và “Design by Hoc HK”.

## Phase 31 - Admin audit modal (2026-02-04)
- [x] Đưa chi tiết nhật ký vào modal khi nhấn "Xem".
- [x] Loại bỏ khu vực chi tiết inline ở cuối trang.
- [x] Bổ sung test FE cho modal chi tiết nhật ký.
- [x] Tinh chỉnh modal: format JSON đẹp, nút sao chép, mở rộng/thu gọn.

## Phase 32 - Admin users modal UX (2026-02-05)
- [x] Chuyển "Sửa vai trò" và "Liên kết Zalo" sang popup modal.
- [x] Bố cục modal rõ ràng (meta người dùng, trạng thái, hành động).
- [x] Bổ sung test FE cho 2 modal.
- [x] Hỗ trợ đóng modal bằng phím ESC.

## Phase 33 - Role labels localization (2026-02-05)
- [x] Hiển thị vai trò dạng `Code (Việt hóa)` trong admin users.
- [x] Đồng bộ label ở danh sách + popup chọn vai trò.
- [x] Cập nhật test FE xác nhận nhãn vai trò.

## Phase 34 - Role labels app-wide (2026-02-05)
- [x] Dùng mapping chung cho nhãn vai trò.
- [x] Việt hóa vai trò trong AppShell (chip người dùng).
- [x] Bổ sung test xác nhận hiển thị vai trò đã Việt hóa.

## Phase 35 - Modal spacing polish (2026-02-05)
- [x] Tăng padding footer modal để nút không sát viền.

## Phase 36 - Header dedupe (2026-02-05)
- [x] Loại bỏ nhãn header trùng lặp (eyebrow) ở các trang chính/section.
- [x] Đồng bộ tiêu đề Dashboard/Preview về một dòng rõ ràng.

## Phase 37 - Dashboard cashflow chart (2026-02-05)
- [x] Đổi biểu đồ “Luồng tiền thu” sang cột (Doanh thu+trả hộ vs Tiền thu được).
- [x] Hỗ trợ tuần/tháng + dropdown số kỳ + ghi nhớ lựa chọn.
- [x] Cập nhật backend trend theo granularity + ngày duyệt phiếu thu.

## Phase 38 - Dashboard cashflow UX redesign (2026-02-05)
- [x] Gỡ khu vực "Tuổi nợ" trên Dashboard để mở rộng biểu đồ dòng tiền.
- [x] Tái cấu trúc card "Luồng tiền thu theo kỳ" (layout + controls + legend).
- [x] Stacked bar cho Doanh thu + Trả hộ, bar riêng cho Tiền thu được.
- [x] Nhãn tuần theo khoảng ngày, tooltip đầy đủ; định dạng Triệu/Tỷ.
- [x] Xóa code/CSS thừa liên quan "Tuổi nợ".
- [x] Cập nhật test UI nếu cần.

## Phase 39 - Risk sections collapsible (2026-02-05)
- [x] Thêm khả năng mở/thu gọn cho các khu vực Risk (Danh sách cảnh báo, Tiêu chí phân nhóm, Thiết lập nhắc kế toán, Nhật ký nhắc, Thông báo nội bộ).
- [x] Lưu trạng thái mở/đóng theo localStorage (mặc định mở).
- [x] Cập nhật UI/UX toggle + test liên quan.

## Phase 40 - PWA minimal (2026-02-06)
- [x] Thêm manifest.json + icons cho install/pin.
- [x] Link manifest + theme-color + favicon trong index.html.
- [x] Đăng ký service worker tối thiểu (no-cache) ở production.

## Phase 41 - Frontend build fixes (2026-02-06)
- [x] Sửa lỗi TS type (inputMode, null guards, ReceiptTargetRef id).
- [x] Chuẩn hóa typing báo cáo (aging percent, export job).
- [x] Cập nhật DataTable usage + test receipts open-items.
- [x] Fix cấu hình vitest trong vite config.
- [x] Build frontend chạy OK.

## Phase 42 - Bundle size optimization (2026-02-06)
- [x] Lazy-load các page theo route để giảm bundle chính.
- [x] Tách framework/vendor chunks bằng manualChunks (rollup) để giảm main bundle.
- [x] Build xác nhận không còn cảnh báo chunk > 500KB.

## Phase 43 - Route prefetch (2026-02-06)
- [x] Tạo page loaders chung + lazy-load thống nhất.
- [x] Prefetch chunk theo hover/focus trên menu điều hướng.
- [x] Thêm test cho prefetcher và build/test OK.

## Phase 44 - Smart prefetch (2026-02-06)
- [x] Prefetch thông minh theo idle (ưu tiên 2 route gần nhất) với kiểm tra save-data/2g.
- [x] Build/test xác nhận OK.

## Phase 45 - Role-aware prefetch (2026-02-06)
- [x] Xác định route ưu tiên theo role + route affinity.
- [x] Prefetch chọn lọc dựa trên role/route/allowed + test.
- [x] Build/test xác nhận OK.

## Phase 46 - Behavior-based prefetch (2026-02-06)
- [x] Ghi nhận lịch sử truy cập route (recent + counts) và ưu tiên prefetch theo thói quen.
- [x] Bổ sung test cho lịch sử/prefetch.
- [x] Build/test xác nhận OK.

## Phase 47 - Deep role-aware prefetch (2026-02-08)
- [x] Bổ sung prefetch tầng sâu theo vai trò (flow + affinity bậc 2).
- [x] Chạy prefetch theo 2 nhịp (primary + deep) với guard mạng.
- [x] Cập nhật test cho chiến lược prefetch mới.

## Phase 48 - Ops Console UI redesign (2026-02-09)
- [x] Tái cấu trúc giao diện WPF theo hướng data-dense (nav trái + cards).
- [x] Thêm "Tác vụ nhanh" tại Overview (deploy/restart/backup).
- [x] Bổ sung trạng thái App Pool ở Overview + inline validation App Pool Name.
- [x] Sắp xếp lại các khu vực Frontend/Backend/Services bằng grid để dễ thao tác.

## Phase 49 - Ops Console DB init + deploy workflow (2026-02-09)
- [x] Thêm endpoint tạo database + chạy migrations trong Ops Agent.
- [x] Bổ sung UI khởi tạo CSDL (kiểm tra DB/tạo DB/migrations).
- [x] Thêm hướng dẫn quy trình triển khai lần đầu trong tab Triển khai.

## Phase 50 - AI risk scoring baseline (2026-02-12)
- [x] Backend: thêm AI risk scorer explainable và mở rộng payload `/risk/customers`, `/risk/bootstrap`.
- [x] Frontend: hiển thị xác suất trễ hạn + tín hiệu AI tại bảng cảnh báo rủi ro.
- [x] Test + docs: thêm unit test scorer, cập nhật Opus review với trạng thái + giới hạn phạm vi.

## Phase 51 - Full ML risk model training + seasonality + MLOps (2026-02-12) [bead: cng-iva]
- [x] DB schema: thêm `risk_ml_models`, `risk_ml_training_runs` + migration an toàn. [bead: cng-mtd.7]
- [x] ML trainer: pipeline snapshot features + label horizon + seasonality (sin/cos) + logistic regression. [bead: cng-mtd.5]
- [x] ML serving: tích hợp model active vào `RiskService` (fallback heuristic), thêm admin endpoints train/list/activate + scheduler retrain. [bead: cng-mtd.4]
- [x] Quality + tracking: bổ sung unit tests, chạy verify, cập nhật `Opus_4.6_review.md`, `task.md`, beads với trạng thái cuối. [bead: cng-mtd.6]

## Phase 52 - Staging migration + risk-ml smoke + Docker rollout (2026-02-12) [bead: cng-pwb]
- [x] Staging DB: tạo backup và chạy migration `019`→`023`, xác nhận schema mới (`refresh_tokens` + `risk_ml_*`). [bead: cng-pwb.1]
- [x] Smoke `/admin/risk-ml/*`: xác thực login admin, train/list/activate model trên dữ liệu staging. [bead: cng-pwb.2]
- [x] Rollout Docker: build + up compose, fix mount migration scripts, xác nhận `health` + `health/ready` + frontend. [bead: cng-pwb.3]
- [x] Đồng bộ tài liệu/bằng chứng thực thi vào `Opus_4.6_review.md`, `DEPLOYMENT_GUIDE_DOCKER.md`, `docs/OPS_ADMIN_CONSOLE.md`, `task.md`, beads. [bead: cng-pwb.4]

## Phase 53 - Backup restore compatibility on Docker (2026-02-12) [bead: cng-8nq]
- [x] Fix restore dump cũ: reset schema trước restore (`DROP SCHEMA ... CASCADE`) để tránh xung đột constraint/object khi DB hiện tại có bảng mới hơn dump. [bead: cng-8nq.1]
- [x] Harden command `pg_restore`: thêm `--clean --if-exists --no-owner --no-privileges --exit-on-error` để restore portable qua môi trường/role. [bead: cng-8nq.1]
- [x] Harden command `pg_dump`: thêm `-O -x` để backup mới không phụ thuộc owner/privileges của role nguồn. [bead: cng-8nq.1]
- [x] Chạy auto migration ngay sau restore để schema được nâng về version runtime hiện tại, tránh lỗi API sau restore dump cũ. [bead: cng-8nq.1]
- [x] Bổ sung unit tests cho restore/dump argument builder + verify e2e `upload -> restore -> login`. [bead: cng-8nq.1]

## Phase 53 - Docker compatibility re-audit after restore (2026-02-12)
- [x] Re-verify runtime containers (db,api,web) after backup restore.
- [x] Re-run smoke test via direct API (:18080) and proxy API (:18081/api).
- [x] Re-check web route fallback (/, /reports) on Nginx container.
- [x] Re-check Docker log/backup paths in API container.
- [x] Sync status to Opus_4.6_review.md + beads (cng-iyn).

## Phase 54 - Opus medium/low backlog execution (excluding Email) (2026-02-13)
- [x] Task 1: Hoàn tất custom hooks foundation (`usePersistedState`, `usePagination`, `useQuery`) + refactor page liên quan + test.
- [x] Task 2: Monitoring/APM uplift (OpenTelemetry + Prometheus exporter/config + `/metrics`).
- [x] Task 3: Dashboard expected vs actual + variance + cashflow forecast (BE+FE+test).
- [x] Task 4: Report export PDF cho Summary (`format=pdf`) + UI nút tải PDF + test backend.
- [x] Task 5: Dark mode (light/dark/system), toggle AppShell, persisted theme + test.
- [x] Task 6: ERP integration baseline (status + sync summary manual), config `ErpIntegration`, admin route/page + test module mới.
- [x] Task 7: PWA upgrade (service worker caching strategy, `offline.html`, manifest shortcuts, SW lifecycle update check).
- [x] Task 8: Full verification + cập nhật tracker/docs.

### Verification evidence (2026-02-13)
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`94/94`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`36/36`).
- [x] `npm run --prefix src/frontend test -- --run` => pass (`83/83`).
- [x] `npm run --prefix src/frontend build` => pass.
- [x] `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass.

## Phase 55 - Tech-debt follow-up: bundle/CSS/backend split (2026-02-13) [bead: cng-qc0]
- [x] Bundle optimization: giới hạn font subset về latin/vietnamese trong `main.tsx` và thêm budget gate `npm run --prefix src/frontend build:budget`.
- [x] CSS architecture: tách layout shell khỏi `index.css` sang `src/frontend/src/styles/layout-shell.css` và import tại `AppShell`.
- [x] Backend service split: refactor `BackupService` thành partial (`BackupService.cs` + `BackupService.InternalOps.cs`) để đưa module chính về <= 800 dòng.
- [x] Đồng bộ trạng thái vào `Opus_4.6_review.md` (mục refactor + critical issue bundle).

### Verification evidence (2026-02-13, phase 55)
- [x] `npm run --prefix src/frontend build:budget` => pass (bundle budget check passed).
- [x] `npm run --prefix src/frontend test -- --run` => pass (`83/83`).
- [x] `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`94/94`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`36/36`).

## Phase 56 - Reports CSS split from global stylesheet (2026-02-13) [bead: cng-1lf]
- [x] Tạo `src/frontend/src/pages/reports/reports.css` và di chuyển toàn bộ selector `reports-*` từ `src/frontend/src/index.css`.
- [x] Import CSS route-scoped tại `src/frontend/src/pages/ReportsPage.tsx`.
- [x] Giữ `kpi-stack` ở global do đang dùng chung cho Dashboard và Reports.
- [x] Re-verify frontend build/test/budget sau refactor.

### Verification evidence (2026-02-13, phase 56)
- [x] `npm run --prefix src/frontend build` => pass.
- [x] `npm run --prefix src/frontend test -- --run` => pass (`83/83`).
- [x] `npm run --prefix src/frontend build:budget` => pass (bundle budget check passed).

## Phase 57 - Dashboard CSS split from global stylesheet (2026-02-13) [bead: cng-qcb]
- [x] Tạo `src/frontend/src/pages/dashboard/dashboard.css` cho các selector Dashboard/Cashflow/Forecast.
- [x] Import CSS mới tại `src/frontend/src/pages/DashboardPage.tsx` và `src/frontend/src/pages/DashboardPreviewPage.tsx`.
- [x] Rút các selector dashboard-specific khỏi `src/frontend/src/index.css`, giữ lại selector dùng chung (`kpi-stack`, `line-chart`, `filter-chip`).
- [x] Xác nhận `index.css` giảm thêm xuống 2203 dòng.

### Verification evidence (2026-02-13, phase 57)
- [x] `npm run --prefix src/frontend build` => pass.
- [x] `npm run --prefix src/frontend test -- --run` => pass (`83/83`).
- [x] `npm run --prefix src/frontend build:budget` => pass (bundle budget check passed).

## Phase 58 - ReminderService partial split and tech-debt closure (2026-02-13) [bead: cng-395]
- [x] Refactor `ReminderService` thành partial theo trách nhiệm:
  - `ReminderService.cs`: constructor + settings API + log listing + normalization helpers.
  - `ReminderService.Execution.cs`: runtime flow `RunAsync` + candidate loading + delivery/logging.
- [x] Giữ nguyên behavior nhắc nợ quá hạn và nhắc đến hạn; không đổi contract API.
- [x] Cập nhật `Opus_4.6_review.md` để đóng 3 mục còn mở: CSS architecture, backend service files, frontend bundle size.

### Verification evidence (2026-02-13, phase 58)
- [x] `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`94/94`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`36/36`).
- [x] `npm run --prefix src/frontend build:budget` => pass (bundle budget check passed).

## Phase 59 - Opus review v2 validation + execution plan (2026-02-13) [bead: cng-4uj]
- [x] Đối chiếu `Opus_review_v2.md` với codebase thực tế (snapshot ngày 13/02/2026), phân loại nhận định theo nhóm: ĐÚNG / MỘT PHẦN / KHÔNG CÒN ĐÚNG.
- [x] Tạo epic + bead con để triển khai backlog còn hiệu lực:
  - `cng-4uj.1` P0 - Reconcile + retention memory-safe batching.
  - `cng-4uj.2` P0 - Transactional audit integrity for receipt approve.
  - `cng-4uj.3` P0 - Risk classifier configurable match mode.
  - `cng-4uj.4` P0 - Security hardening round 3.
  - `cng-4uj.5` P1 - Receipt workflow enhancements (draft edit, bulk actions feasibility, reminder dry-run, commit progress visibility).
  - `cng-4uj.6` P1 - Frontend refactor for dashboard/queries (`useMutation`, split page lớn, allocation donut/drill-down).
  - `cng-4uj.7` P1 - Observability enrichment (correlation id, custom business metrics, readiness checks).
  - `cng-4uj.8` P2 - Monitoring stack baseline (Grafana/Loki/Alertmanager hoặc tương đương).
  - `cng-4uj.9` P1 - Backend contract hardening (status constants + API versioning strategy).
  - `cng-4uj.10` P2 - Allocation Pro-rata mode.
- [x] Cập nhật `Opus_review_v2.md` với phụ lục xác thực của Codex, chỉ rõ các mục đúng/sai/một phần và lộ trình triển khai.

### Kế hoạch triển khai chi tiết theo ưu tiên
- [x] P0 - Correctness + reliability + security baseline
  - [x] [bead: cng-4uj.1] Refactor `CustomerBalanceReconcileService` và `DataRetentionService` sang batch/chunk processing, tránh full materialization và `RemoveRange` trên tập lớn.
  - [x] [bead: cng-4uj.2] Đưa audit nghiệp vụ receipt approve vào cùng transaction boundary; thêm integration test đảm bảo rollback khi audit fail.
  - [x] [bead: cng-4uj.3] Bổ sung `MatchMode` (Any/All) cho `RiskRule`, đồng bộ logic domain + SQL evaluation path, thêm test ngăn false-positive.
  - [x] [bead: cng-4uj.4] Triển khai lockout policy, middleware security headers, CORS đọc từ config theo môi trường.
- [x] P1 - Workflow + architecture hardening
  - [x] [bead: cng-4uj.5] Bổ sung luồng sửa phiếu thu ở trạng thái DRAFT; đánh giá/triển khai bulk approve có kiểm soát; thêm reminder dry-run; bổ sung trạng thái tiến trình commit import.
  - [x] [bead: cng-4uj.6] Tạo `useMutation`, tách `DashboardPage`/`ReportsPage`/`RiskAlertsPage` thành sub-components, thay chart phân bổ từ bar sang donut + drill-down.
  - [x] [bead: cng-4uj.7] Thêm correlation id middleware, metric nghiệp vụ OTel (approval, import, reminder, reconcile), mở rộng readiness checks.
  - [x] [bead: cng-4uj.9] Chuẩn hóa constants/enum trạng thái và thiết kế lộ trình API versioning (`/api/v1`) tương thích ngược.
- [x] P2 - Scale/readiness enhancements
  - [x] [bead: cng-4uj.8] Thiết lập baseline dashboard/alert tối thiểu cho Prometheus metrics + logs.
  - [x] [bead: cng-4uj.10] Thêm mode phân bổ Pro-rata, đảm bảo không phá behavior hiện tại của FIFO/ByInvoice/ByPeriod/Manual.

### Verification gate (áp dụng cho từng bead trước khi đóng)
- [x] Backend: `dotnet build src/backend/Api/CongNoGolden.Api.csproj`.
- [x] Backend tests: `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` và `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj`.
- [x] Frontend tests/build: `npm run --prefix src/frontend test -- --run`, `npm run --prefix src/frontend build`, `npm run --prefix src/frontend build:budget`.
- [x] Đồng bộ tracker: cập nhật `task.md`, `Opus_review_v2.md`, trạng thái bead (`bd update`/`bd close`) sau mỗi cụm task hoàn tất.

### Verification evidence (2026-02-13, phase 59 - cng-4uj.1)
- [x] `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter "FullyQualifiedName~CustomerBalanceReconcileServiceTests|FullyQualifiedName~DataRetentionServiceTests"` => pass (`5/5`, RED->GREEN cycle).
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`98/98`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`36/36`).

### Verification evidence (2026-02-13, phase 59 - cng-4uj.2/.3/.4)
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~ReceiptLifecycleRbacTests"` => pass (`4/4`) cho transactional audit integrity.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter "FullyQualifiedName~RiskClassifierTests"` => pass (`2/2`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~RiskRulesTests"` => pass (`2/2`) với case `ALL` giảm false-positive.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter "FullyQualifiedName~AuthServiceTests|FullyQualifiedName~SecurityHeadersMiddlewareTests"` => pass (`9/9`) cho lockout + security headers.
- [x] Full backend verify:
  - `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass.
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`104/104`).
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`38/38`).
- [x] Frontend verify:
  - `npm run --prefix src/frontend test -- --run` => pass (`86/86`).
  - `npm run --prefix src/frontend build` => pass.
  - `npm run --prefix src/frontend build:budget` => pass.

### Verification evidence (2026-02-13, phase 59 - cng-4uj.5/.6/.7/.8/.9/.10)
- [x] Backend verify:
  - `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass.
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`115/115`).
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`41/41`).
- [x] Frontend verify:
  - `npm run --prefix src/frontend test -- --run` => pass (`88/88`).
  - `npm run --prefix src/frontend build` => pass.
  - `npm run --prefix src/frontend build:budget` => pass.
- [x] Monitoring baseline verify:
  - `docker compose --profile monitoring config` => pass.
  - `docker compose --profile monitoring up -d alertmanager prometheus loki grafana` => image/provision thành công cho đa số services; `loki` fail do host port `3100` đang bị chiếm, đã bổ sung hướng dẫn fallback `LOKI_PORT=13100` trong `docs/OPS_MONITORING_BASELINE.md`.
  - Hoàn tất triển khai local bằng fallback ports:
    - `LOKI_PORT=13100`, `GRAFANA_PORT=13001` + `docker compose --profile monitoring up -d loki prometheus grafana` => services chạy ổn định.
    - Health check: `http://localhost:13100/ready` => `200`, `http://localhost:9090/-/ready` => `200`, `http://localhost:13001/api/health` => `200`.

## Phase 60 - ERP integration config UI + API (2026-02-13) [bead: cng-fwg]
- [x] Thêm bảng cấu hình ERP `erp_integration_settings` và wire vào `ConGNoDbContext`.
- [x] Mở rộng `IErpIntegrationService` với luồng đọc/cập nhật cấu hình kết nối (provider/baseUrl/companyCode/apiKey/timeout/enabled).
- [x] Bổ sung API:
  - [x] `GET /admin/erp-integration/config`
  - [x] `PUT /admin/erp-integration/config`
- [x] Thêm khu vực `Cấu hình kết nối` trên UI `AdminErpIntegrationPage` để xem/sửa cấu hình ngay trên giao diện.
- [x] Bổ sung test:
  - [x] Unit test backend cho persist/keep API key behavior.
  - [x] Frontend test cho flow lưu cấu hình.

### Verification evidence (2026-02-13, phase 60)
- [x] `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter ErpIntegrationServiceTests` => pass (`4/4`).
- [x] `npm --prefix src/frontend run test -- --run src/pages/__tests__/admin-erp-integration-page.test.tsx` => pass (`2/2`).
- [x] `npm --prefix src/frontend run build` => pass.

## Phase 61 - Docker-first documentation synchronization (2026-02-14) [bead: cng-fou]
- [x] Đồng bộ tài liệu vận hành để phản ánh Docker Compose là phương thức deploy mặc định.
- [x] Chuyển toàn bộ mô tả IIS/Windows Service còn dùng ở tài liệu vận hành sang trạng thái legacy/fallback.
- [x] Cập nhật quyết định kỹ thuật (`TECH_DECISIONS`) theo runtime Docker-first.
- [x] Cập nhật runbook + backup/restore guide theo quy trình Docker.
- [x] Chuẩn hóa wording trong docs liên quan (`README`, `RUN_BACKEND`, `RUN_FRONTEND`, `OPS_ADMIN_CONSOLE`, deployment guides).

### Verification evidence (2026-02-14, phase 61)
- [x] `rg -n "\\bIIS\\b|Windows Service|windows-service" -S . -g "*.md" -g "!extracted_specs/**" -g "!docs/beads-starter/**" -g "!docs/plans/**" -g "!progress.md" -g "!task.md" -g "!TASKS.md" -g "!Opus_*.md" -g "!findings.md"` => tài liệu vận hành hiện hành chỉ còn nhắc IIS ở ngữ cảnh legacy/fallback.

## Phase 62 - Historical label for archive documents (2026-02-14) [bead: cng-oes]
- [x] Gắn banner `HISTORICAL DOCUMENT` cho toàn bộ tài liệu `extracted_specs/*`.
- [x] Gắn banner `HISTORICAL DOCUMENT` cho `progress.md`.
- [x] Gắn banner `HISTORICAL DOCUMENT` cho `Opus_4.6_review.md`, `Opus_review_v2.md`.
- [x] Đảm bảo banner trỏ về nguồn chuẩn hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `docs/OPS_ADMIN_CONSOLE.md`.

### Verification evidence (2026-02-14, phase 62)
- [x] `rg -n "HISTORICAL DOCUMENT" progress.md Opus_4.6_review.md Opus_review_v2.md extracted_specs` => tất cả file mục tiêu đã có nhãn historical.

## Phase 63 - Cleanup tài liệu legacy <= 2026-01-29 (2026-02-14) [bead: cng-0vy]
- [x] Xóa tài liệu scratch/duplicate cũ không còn phù hợp với trạng thái hệ thống hiện tại:
  - [x] `TASKS.md` (trùng vai trò với `task.md`, nội dung cũ dễ gây nhầm).
  - [x] `findings.md` (nhật ký khám phá theo phiên cũ, không còn là nguồn chuẩn).
  - [x] `PERFORMANCE_NOTES.md` (kết quả perf sample rất sớm, không phản ánh runtime hiện tại).
  - [x] `task_plan.md` (kế hoạch phiên cũ đã hoàn thành, không còn hiệu lực vận hành).
- [x] Giữ `task.md` làm tracker chính.
- [x] Chuẩn hóa `QA_REPORT.md` để phân biệt historical baseline (2026-01-08) với verification mới (2026-02-13 Docker runtime).
- [x] Bổ sung cảnh báo historical trong `task.md` và `progress.md` để tránh đọc nhầm các mốc <= 2026-01-29 là trạng thái vận hành hiện tại.

### Verification evidence (2026-02-14, phase 63)
- [x] `git rm -f TASKS.md findings.md PERFORMANCE_NOTES.md task_plan.md` => removed.
- [x] `rg -n "findings\\.md|PERFORMANCE_NOTES\\.md|TASKS\\.md" -S task.md README.md RUNBOOK.md docs` => không còn tham chiếu vận hành hiện hành đến các file đã xóa.
- [x] `rg -n "HISTORICAL DOCUMENT|Phase 63" progress.md task.md` => các cảnh báo historical mới đã có mặt.

## Phase 64 - Rà soát tài liệu > 2026-01-29 (2026-02-14) [bead: cng-ui5]
- [x] Rà toàn bộ nhóm tài liệu sau 2026-01-29 để đối chiếu với trạng thái hệ thống hiện tại.
- [x] Gắn nhãn `HISTORICAL EXECUTION PLAN` cho toàn bộ `docs/plans/*.md` để tránh nhầm với runbook vận hành hiện tại.
- [x] Đồng bộ drift nội dung giữa `API_CONTRACT_NOTES.md` và `docs/API_CONTRACT_NOTES.md`.
- [x] Cập nhật `docs/OPENAPI_TYPES.md` theo thực tế Docker/env (URL swagger theo `API_PORT`).
- [x] Cập nhật `DEPLOYMENT_GUIDE_DOCKER.md` để endpoint dùng biến cổng `WEB_PORT`/`API_PORT` rõ ràng.
- [x] Chuẩn hóa `DEPLOYMENT_GUIDE_DOCKER.md` dùng `.env.example` làm template chính (giữ `.env.docker.example` ở trạng thái legacy compatibility).

### Verification evidence (2026-02-14, phase 64)
- [x] `rg -n "HISTORICAL EXECUTION PLAN" -S docs/plans -g "*.md"` => tất cả execution plan đã có nhãn historical.
- [x] `Get-FileHash API_CONTRACT_NOTES.md` == `Get-FileHash docs/API_CONTRACT_NOTES.md` => không còn drift.
- [x] `rg -n "API_PORT|WEB_PORT|swagger/v1/swagger.json" DEPLOYMENT_GUIDE_DOCKER.md docs/OPENAPI_TYPES.md` => nội dung đã đồng bộ theo cấu hình cổng runtime.

## Phase 65 - Dashboard allocation donut layout refinement (2026-02-14) [bead: cng-23z]
- [x] Chuyển layout khu vực `Trạng thái phân bổ` từ ngang sang dọc: donut ở trên, danh sách trạng thái ở dưới.
- [x] Tăng kích thước donut + tâm donut để tận dụng không gian card và cân đối thị giác sau khi đổi bố cục.
- [x] Tinh chỉnh legend item (spacing/grid) để dễ đọc khi đặt dưới biểu đồ.
- [x] Giữ drilldown behavior hiện tại, chỉ thay đổi presentation/layout.

### Verification evidence (2026-02-14, phase 65)
- [x] `npm run --prefix src/frontend test -- --run src/pages/__tests__/dashboard-page.test.tsx` => pass (`1/1`).
- [x] `npm run --prefix src/frontend build` => pass.

## Phase 66 - Quality gate + tracker synchronization (2026-02-23)
- [x] Áp dụng workflow skills phù hợp cho lượt xử lý đa bước:
  - [x] `planning-with-files` (khôi phục sổ tay phiên: `task_plan.md`, `findings.md`, `progress.md`).
  - [x] `plan-writing` (lập task list + done criteria rõ ràng).
  - [x] `lint-and-validate` (chạy lại lint/test/build sau chỉnh sửa).
  - [x] `verification-before-completion` (chỉ chốt sau khi có evidence command mới).
- [x] Sửa lỗi lint frontend:
  - [x] `src/frontend/src/hooks/useTheme.ts`: bỏ `setState` đồng bộ trong `useEffect` để tránh `react-hooks/set-state-in-effect`.
  - [x] `src/frontend/src/pages/RiskAlertsPage.tsx`: refactor bootstrap guard bằng `bootstrapInFlightRef` để xử lý warning dependency của `useEffect`.
- [x] Đồng bộ tracker công việc:
  - [x] Đóng bead `cng-fwg` (ERP integration) sau khi xác thực lại test/build.
  - [x] Cập nhật tài liệu trạng thái (`task.md`, `QA_REPORT.md`, `progress.md`, `task_plan.md`, `findings.md`).

### Verification evidence (2026-02-23, phase 66)
- [x] `npm --prefix src/frontend run lint` => pass.
- [x] `npm --prefix src/frontend run test -- --run` => pass (`90/90`).
- [x] `npm --prefix src/frontend run build` => pass.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --nologo --verbosity minimal` => pass (`116/116`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --nologo --verbosity minimal` => pass (`41/41`).
- [x] `bd ready --json` => `[]`.

## Phase 67 - Scale readiness roadmap planning (2026-02-23) [bead: cng-oiw]
- [x] Tạo epic + task con trên bead cho toàn bộ lộ trình mở rộng:
  - [x] `cng-oiw` (epic): Scale readiness roadmap.
  - [x] `cng-oiw.1`: Baseline tải + SLO bằng k6.
  - [x] `cng-oiw.2`: Redis cache cho read-heavy endpoints.
  - [x] `cng-oiw.3`: Queue worker cho job nặng.
  - [x] `cng-oiw.4`: Read-replica + tách read/write path.
  - [x] `cng-oiw.5`: Autoscaling + guardrail vận hành.
- [x] Tạo tài liệu kế hoạch chi tiết, dễ theo dõi cho người không chuyên:
  - [x] `docs/plans/2026-02-23-scale-readiness-roadmap.md`.
- [x] Triển khai chặng A (k6 baseline + SLO) theo bead `cng-oiw.1`.
- [x] Triển khai chặng B (Redis cache) theo bead `cng-oiw.2`.
- [x] Triển khai chặng C (Queue/Worker) theo bead `cng-oiw.3`.
- [x] Triển khai chặng D (Read replica) theo bead `cng-oiw.4`.
- [x] Triển khai chặng E (Autoscaling + guardrail) theo bead `cng-oiw.5`.

### Verification evidence (2026-02-23, phase 67 planning)
- [x] `bd create --type epic ...` => tạo epic `cng-oiw`.
- [x] `bd create --type task --parent cng-oiw ...` => tạo các task `cng-oiw.1` -> `cng-oiw.5`.
- [x] `bd update cng-oiw --status in_progress` => epic chuyển sang `in_progress`.

### Verification evidence (2026-02-23, phase 67 execution)
- [x] `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`127/127`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`41/41`).
- [x] `npm --prefix src/frontend run lint` => pass.
- [x] `npm --prefix src/frontend run test -- --run` => pass (`90/90`).
- [x] `npm --prefix src/frontend run build` => pass.
- [x] `npm --prefix src/frontend run build:budget` => pass.
- [x] Thêm async maintenance queue/worker + metrics:
  - `POST /admin/health/reconcile-balances/queue`
  - `POST /admin/health/run-retention/queue`
  - `GET /admin/maintenance/jobs`
  - `GET /admin/maintenance/jobs/{jobId}`
- [x] Hoàn tất docs scale readiness:
  - `docs/performance/QUEUE_WORKER_OPERATIONS.md`
  - `docs/performance/READ_REPLICA_ROUTING.md`
  - `docs/performance/AUTOSCALING_GUARDRAILS.md`
- [x] `bd close cng-oiw.1 ... cng-oiw.5` + `bd close cng-oiw` => closed.
- [x] `bd ready --json` => `[]`.

## Phase 68 - Opus review v3 validation + tracker synchronization (2026-02-24) [bead: cng-rlx]
- [x] Đối chiếu `Opus_review_v3.md` với codebase hiện tại và thêm bảng phân loại claim theo 3 nhóm: `OUTDATED`, `PARTIAL`, `CONFIRMED GAP`.
- [x] Xác nhận các nhận định đã lỗi thời (không cần làm lại):
  - [x] Risk AI explainability + action recommendation.
  - [x] Dashboard executive summary + KPI MoM.
  - [x] Risk page tab layout (Overview/Config/History).
  - [x] Notification Center route/view-all.
- [x] Chốt nhóm gap còn hiệu lực trong V3 review:
  - [x] Global search đa thực thể.
  - [x] Onboarding tour/coachmarks.
  - [x] Import drag-and-drop UX.
  - [x] Report print layout + scheduled report delivery.
  - [x] Risk score delta alert theo thời gian.
  - [x] Dashboard widget customization/reorder.
- [x] Cập nhật tài liệu điều phối: `opus-review-v3-remediation.md`, `task.md`, `task_plan.md`, `findings.md`, `progress.md`.
- [x] Đóng toàn bộ bead liên quan `cng-rlx.1` -> `cng-rlx.5` và epic `cng-rlx`.

### Verification evidence (2026-02-24, phase 68)
- [x] `dotnet build src/backend/Api/CongNoGolden.Api.csproj --nologo` => pass.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --nologo --verbosity minimal` => pass (`127/127`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --nologo --verbosity minimal` => pass (`42/42`).
- [x] `npm --prefix src/frontend run lint` => pass.
- [x] `npm --prefix src/frontend run test -- --run` => pass (`92/92`).
- [x] `npm --prefix src/frontend run build` => pass.

## Phase 69 - Opus V3 remaining gaps execution (2026-02-24) [bead: cng-los]
- [x] Re-validate `Opus_review_v3.md` against current code; split claims into `OUTDATED` / `PARTIAL` / `CONFIRMED GAP`.
- [x] Create remediation plan for remaining gaps:
  - [x] `docs/plans/2026-02-24-opus-v3-remaining-gaps-implementation-plan.md`.
- [x] Create/align beads for execution:
  - [x] `cng-los` (epic, closed)
  - [x] `cng-los.1` Backend: scheduled reports + risk delta alerts + reminder escalation (closed)
  - [x] `cng-los.2` Global search (closed)
  - [x] `cng-los.3` Onboarding + import drag-drop (closed)
  - [x] `cng-los.4` Dashboard widget customization + report print (closed)
  - [x] `cng-los.5` Verification + tracker sync (closed)

### Planned execution checklist (do dần theo bead)
- [x] `cng-los.1` - Backend foundation
  - [x] Add migrations `027/028/029` for report schedules, risk snapshots/delta alerts, reminder escalation policy.
  - [x] Implement scheduled report delivery service + hosted worker + run logs.
  - [x] Implement risk delta snapshot/alert pipeline + notification integration.
  - [x] Upgrade reminder execution to escalation-aware policy with cooldown.
  - [x] Add backend unit/integration tests for all above.
- [x] `cng-los.2` - Global search
  - [x] Add unified backend search endpoint (customer/invoice/receipt).
  - [x] Add frontend command palette (`Ctrl/Cmd + K`) + quick navigation.
  - [x] Add backend integration + frontend RTL tests.
- [x] `cng-los.3` - Onboarding + import UX
  - [x] Add first-run onboarding tour with skip/replay state.
  - [x] Add drag-and-drop upload flow in Import Batch section.
  - [x] Add RTL tests for onboarding and dropzone behaviors.
- [x] `cng-los.4` - Dashboard + reports UX
  - [x] Add dashboard widget visibility/order preferences (API + UI).
  - [x] Add report print-friendly layout and print action.
  - [x] Add backend/frontend tests for preference + print trigger/smoke.
- [x] `cng-los.5` - Verification + sync
  - [x] Run build/lint/test suite backend + frontend.
  - [x] Update `task.md`, `progress.md`, `findings.md`, and close beads.

### Verification evidence (phase 69 planning)
- [x] `bd ready --json` => shows `cng-los` epic + tasks `.1` -> `.5` with expected statuses.
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter RiskDeltaAlertsTests` => pass (`2/2`).
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter RiskBootstrapEndpointTests` => pass (`1/1`).
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter ReportScheduleServiceTests` => pass (`4/4`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "ReportScheduleServiceTests|ReminderEscalationPolicyTests"` => pass (`4/4`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter GlobalSearchServiceIntegrationTests` => pass (`2/2`).
- [x] `npm --prefix src/frontend run test -- --run src/layouts/__tests__/app-shell.test.tsx src/pages/customers/__tests__/customers-modules.test.tsx` => pass (`11/11`).

### Verification evidence (2026-02-26, phase 69 completion)
- [x] `dotnet build src/backend/Api/CongNoGolden.Api.csproj --nologo` => pass.
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --nologo --verbosity minimal` => pass (`134/134`).
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --nologo --verbosity minimal` => pass (`52/52`).
- [x] `npm --prefix src/frontend run lint` => pass.
- [x] `npm --prefix src/frontend run test -- --run` => pass (`99/99`).
- [x] `npm --prefix src/frontend run build` => pass.
- [x] `bd close cng-los.3; bd close cng-los.4; bd close cng-los.5; bd close cng-los` => pass.
- [x] `bd ready --json` => `[]` (không còn bead mở cho phase 69).
- [x] Frontend lint hardening bổ sung để pass full-suite:
  - [x] `src/frontend/src/layouts/AppShell.tsx`: bỏ setState trong mount effect, chuyển sang lazy initializer.
  - [x] `src/frontend/src/pages/customers/CustomersPage.tsx`: bỏ setState đồng bộ theo query trong effect.

## Phase 70 - Response-aware reminder escalation residual (2026-02-26) [bead: cng-d3e]
- [x] `cng-d3e.2`: triển khai luồng escalation theo response state trong ReminderService:
  - [x] thêm entity + DbSet + migration `030_reminder_response_states.sql`.
  - [x] thêm API/contract quản lý response state (`GET/PUT /reminders/response-state`).
  - [x] refactor execution flow dùng response-state (`ACKNOWLEDGED`, `DISPUTED`, `RESOLVED`, `ESCALATION_LOCKED`).
  - [x] bổ sung integration tests cho acknowledged/resolved.
- [x] Sửa compile error `EnsureUser` bằng namespace đúng `CongNoGolden.Infrastructure.Services.Common`.
- [x] Verification targeted:
  - [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~Reminder"` => pass (`6/6`).
  - [x] `npm --prefix src/frontend run test -- risk-alerts-page-tabs` => pass (`1/1`).
- [x] Đồng bộ bead: `bd close cng-d3e.2`.
- [x] `cng-d3e.3`: bổ sung test transition còn thiếu + sync docs/tracker:
  - [x] thêm integration tests `Run_WhenDisputed_EscalatesWithDisputedReason` và `Run_WhenEscalationLocked_KeepsEscalationLevel` trong `ReminderEscalationPolicyTests`.
  - [x] cập nhật `Opus_review_v3.md` cho claim reminder escalation (`PARTIAL` -> `OUTDATED (đã có 2026-02-26)`).
  - [x] chạy lại verification targeted backend/frontend.
- [x] Đồng bộ bead: `bd close cng-d3e.1`, `bd close cng-d3e.3`, `bd close cng-d3e`.
- Trạng thái bead liên quan sau phiên:
  - `cng-d3e.1`: `closed`
  - `cng-d3e.2`: `closed`
  - `cng-d3e.3`: `closed`
  - `cng-d3e` (epic): `closed`

## Phase 71 - Import validation UX hardening (2026-02-26) [bead: cng-9y1]
- [x] Bổ sung trạng thái visual lỗi cho dropzone import:
  - [x] thêm class `.upload-dropzone--error` trong `src/frontend/src/index.css`.
- [x] Mở rộng test validation import invalid file:
  - [x] reject non-`.xlsx` khi drag-drop.
  - [x] reject `.xlsx` vượt `20MB` qua file input.
  - [x] reject `.xlsx` vượt `20MB` qua drag-drop.
  - [x] xác nhận invalid cases không gọi API `uploadImport`.
- [x] Chạy quality gate frontend cho thay đổi:
  - [x] `npm run test -- --run src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx`.
  - [x] `npm run lint`.
- [x] Đồng bộ `task.md`, `task_plan.md`, `findings.md`, `progress.md`.
- [x] Đóng bead `cng-9y1`.

### Verification evidence (2026-02-26, phase 71)
- [x] `npm run test -- --run src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx` => pass (`4/4`).
- [x] `npm run lint` => pass.
- [x] `bd close cng-9y1` => pass.

## Phase 72 - Collection queue + reports mobile cards (2026-02-26)
- [x] Hoàn thiện wiring cho Collection Task Queue:
  - [x] register `ICollectionTaskQueue` trong `Infrastructure/DependencyInjection.cs`.
  - [x] map `app.MapCollectionEndpoints()` trong `Api/Program.cs`.
- [x] Sửa logic enqueue từ risk để tránh đếm trùng task khi cùng khách hàng xuất hiện nhiều lần trong một lần generate.
- [x] Bổ sung unit tests cho queue mới:
  - [x] dedupe theo customer tax code.
  - [x] không tạo task mới khi đã có task OPEN.
  - [x] transition status DONE/OPEN và `CompletedAt`.
- [x] Bổ sung mobile-optimized table cards cho Reports:
  - [x] thêm `data-label` cho cell ở summary/statement/aging tables.
  - [x] thêm CSS responsive card layout (`.table--mobile-cards`) cho màn hình nhỏ.
  - [x] cập nhật test `reports-modules` kiểm tra `data-label`.

### Verification evidence (2026-02-26, phase 72)
- [x] `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter CollectionTaskQueueTests` => pass (`3/3`).
- [x] `npm --prefix src/frontend run test -- reports-modules.test.tsx` => pass (`9/9`).

## Phase 73 - Role cockpit UX refinement (2026-02-27) [bead: cng-cb2]
- [x] Review nhanh luong thao tac dashboard theo 3 goc nhin: director / manager / user.
- [x] Refactor UI dashboard:
  - [x] Them widget `roleCockpit` vao bo widget preference.
  - [x] Tao module moi `RoleCockpitSection.tsx` de tach logic khoi `DashboardPage.tsx`.
  - [x] Hien thi decision cards + workflow step + diem nong theo vai tro.
  - [x] Cap nhat CSS responsive cho cockpit section.
- [x] Cap nhat test dashboard:
  - [x] Dong bo `defaultWidgetOrder` co `roleCockpit`.
  - [x] Bo sung test render cockpit cho role director.

### Verification evidence (2026-02-27, phase 73)
- [x] `npm run test -- src/pages/__tests__/dashboard-page.test.tsx` => pass (`4/4`).
- [x] `npm run lint` => pass.
- [x] `npm run build` => pass.

## Phase 74 - Header navigation simplification + dashboard settings modal (2026-02-27) [bead: cng-dcx]
- [x] Loại bỏ quick navigation chips ở header AppShell và dọn code/CSS/test liên quan.
- [x] Đổi chuyển đổi theme `Sáng/Tối/Hệ thống` từ segmented buttons sang dropdown.
- [x] Chuyển `Tùy chỉnh Dashboard` từ section inline sang nút mở popup/modal.
- [x] Giảm nhiễu trạng thái auto-save để không còn nháy `Đang lưu cấu hình...` liên tục.
- [x] Chạy test/lint/build frontend và đồng bộ lại bead + tracker.

### Verification evidence (2026-02-27, phase 74)
- [x] `npm --prefix src/frontend run test -- --run src/layouts/__tests__/app-shell.test.tsx src/pages/__tests__/dashboard-page.test.tsx src/pages/reports/__tests__/reports-modules.test.tsx` => pass (`20/20`).
- [x] `npm --prefix src/frontend run lint` => pass.
- [x] `npm --prefix src/frontend run build` => pass.

