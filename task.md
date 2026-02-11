 # Chú ý:
 • Giữ trí nhớ xuyên suốt nhiều lượt trao đổi
 • Duy trì trạng thái công việc trong một “sổ tay” thống nhất
 • Tránh cảnh AI bị quên ngữ cảnh khi context bị nén hay reset
 • Không nồi quá nhiều code vào trong một module. Mỗi module không quá 800 dòng code nếu có thể.
 • Mỗi module mới đều cần tạo bản test. Nếu code chính thay đổi thì cũng cập nhật test cho phù hợp và ngược lại.

# TASKS - Cong No Golden

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
- [ ] Keyboard shortcuts (Enter/Esc/Arrows).
- [x] Dashboard trend chart 6 tháng.
- [ ] Undo/Un-void (reversal) cho receipts/advances.





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
- [ ] Cấu hình Zalo OA sau khi duyệt: AccessToken, WebhookToken, Enabled=true, kiểm tra OaId.
- [ ] Khai báo webhook OA trỏ `/webhooks/zalo?token=...` và xác nhận callback.
- [ ] Test liên kết: tạo mã → nhắn "LINK <code>" → kiểm tra user_id đã lưu.
- [ ] Test gửi nhắc Zalo: chạy `/reminders/run` và kiểm tra log trạng thái SENT.
- [x] Chạy migration và kiểm tra bảng: risk_rules, reminder_settings, reminder_logs, notifications.

## Phase 13 - Import lifecycle UX (2026-01-17)
- [x] Cho phép xem lịch sử nhập liệu với role Accountant (ImportBatchList).
- [x] Bổ sung nút "Tiếp tục" cho lô STAGING + anchor đúng về "Lịch sử nhập".
- [x] Bổ sung hủy lô STAGING (API + UI) + lưu metadata hủy.
- [x] Thêm cột/trường cancel: cancelled_at/cancelled_by/cancel_reason cho import_batches.
- [x] Chạy migration 009_import_cancel.sql và 010_import_status_cancelled.sql, xác nhận cột + check constraint.
- [ ] Tự động dọn import_staging_rows cũ theo TTL.
- [ ] Hiển thị rõ lý do không thể hoàn tác (approved/allocated/khóa kỳ) trên UI.

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
- [ ] Không bắt buộc thay đổi schema (dùng `source_batch_id` để suy luận).
- [ ] Nếu cần báo cáo sâu: cân nhắc thêm `source_type` stored/enum (đánh giá sau).

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
- [ ] OpenAPI cập nhật + regenerate types FE.

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
- [ ] Integration tests: create/approve/cancel receipts + open-items API + RBAC.
- [ ] E2E: chọn KH → popup phân bổ → lưu nháp/duyệt → hủy → nhắc duyệt.

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
