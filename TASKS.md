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
- [ ] Deploy theo `DEPLOYMENT_GUIDE_WEB_WINDOWS.md` + smoke test.
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
