> [!IMPORTANT]
> **HISTORICAL DOCUMENT**
> Tài liệu này là snapshot/lịch sử để tham khảo, **không phải nguồn vận hành chuẩn hiện tại**.
> Nguồn chuẩn hiện tại:
> - Deploy: DEPLOYMENT_GUIDE_DOCKER.md
> - Runbook: RUNBOOK.md
> - Ops runtime: docs/OPS_ADMIN_CONSOLE.md
# ACCEPTANCE_CRITERIA_BY_PHASE — Công nợ Golden (PostgreSQL + Windows + LAN)

Tài liệu này là **checklist nghiệm thu theo phase** để đảm bảo agent/dev làm đúng nghiệp vụ và đúng kiến trúc.  
**Mục tiêu:** bạn có thể “tick từng ô” để kiểm soát tiến độ & chất lượng, tránh lệch spec.

> Input chuẩn để đối chiếu: `app_cong_no.md`, `db_schema_postgresql.sql`, `Mau_DoiSoat_CongNo_Golden.xlsx`, `ReportDetail.xlsx`, `openapi_golden_congno.yaml`.

---

## 0) Global — Điều kiện bắt buộc (Non‑negotiables)

### 0.1. Nghiệp vụ “core” phải có
- [ ] Import có **STAGING → PREVIEW (OK/WARN/ERROR) → COMMIT** (không được commit thẳng).
- [ ] Có cảnh báo **file đã import** (dựa vào `file_hash` SHA-256 và/hoặc period).
- [ ] **Approve workflow** cho **Trả hộ** và **Thu tiền**:
  - DRAFT → APPROVED
  - Duyệt theo **accountant_owner_id** (kế toán phụ trách)
- [ ] Thu tiền có **2 mốc thời gian**:
  - `receipt_date` (ngày tiền về thực tế)
  - `applied_period_start` (ngày đầu tháng đối soát, UI dạng YYYY‑MM; cho phép ghi về kỳ trước)
- [ ] **AllocationEngine** phân bổ thu theo 3 chế độ:
  - **BY_INVOICE** (chọn hóa đơn rõ ràng)
  - **BY_PERIOD** (thu gộp theo tháng; ưu tiên nợ tháng đó rồi dư sang FIFO)
  - **FIFO** (không chỉ định; trừ nợ cũ nhất)
- [ ] **Multi-seller**: mọi chứng từ đều gắn `seller_tax_code` (2 MST bên bán).
- [ ] **Period lock**: kỳ khóa thì không cho ghi/commit vào kỳ đó (trừ ADMIN/SUPERVISOR có override + lý do + audit).
- [ ] **Audit log**: lưu before/after (JSONB) cho các thao tác quan trọng (commit import, approve, void, unlock/lock, sửa master data).

### 0.2. Maintainability “core” phải có
- [ ] **Tách module theo domain** (Auth, Master, Import, Invoices, Advances, Receipts+AllocationEngine, Reports+Excel, PeriodLocks, Audit/Admin, Integrations).
- [ ] Không nhồi code:
  - [ ] Mỗi file code ≤ **300 LOC**
  - [ ] Mỗi function ≤ **50 LOC**
  - [ ] Controller **không chứa business logic**, chỉ gọi usecase/service.
- [ ] Có unit test cho **AllocationEngine** và validators import (tối thiểu các case bên dưới).

### 0.3. DB/Infra bắt buộc
- [ ] DB: **PostgreSQL** (Windows Service) chạy trên Server1.
- [ ] Dùng UUID + JSONB đúng chỗ (staging, audit).
- [ ] Có backup plan (pg_dump + Task Scheduler) + retention.
- [ ] Backup có bản sao offsite (NAS/cloud/USB) theo lịch.


---

## 1) Definition of Done (DoD) chung cho mọi phase
- [ ] Có tài liệu/artefacts theo yêu cầu phase.
- [ ] Có demo chạy được (local/LAN tùy phase).
- [ ] Có checklist test pass (unit/smoke/UAT).
- [ ] Không vi phạm “non-negotiables”.
- [ ] Không vi phạm “file quá dài” (đính kèm thống kê hoặc script kiểm tra).

---

# PHASE 1 — Architecture & Design

## P1.A — Technical decisions
**Artefacts yêu cầu:** `TECH_DECISIONS.md`
- [ ] Chốt stack backend/frontend và lý do (PostgreSQL + Windows).
- [ ] Chốt ORM strategy (EF/Dapper/hybrid) và lý do theo loại workload:
  - Import batch
  - Report tổng hợp
  - Allocation transactional
- [ ] Chốt auth strategy phù hợp LAN (JWT/cookie), refresh/token expiry.
- [ ] Chốt logging, correlation id, error format chuẩn (problem details / error code).

**Evidence cần nộp:**
- [ ] File `TECH_DECISIONS.md` đầy đủ, có mục “trade-offs”.

---

## P1.B — Module boundaries & dependency rules
**Artefacts yêu cầu:** `MODULE_BOUNDARIES.md`, `CLEAN_ARCHITECTURE_MAP.md`
- [ ] Vẽ module boundaries đúng spec.
- [ ] AllocationEngine nằm ở **Domain** (pure) và **không truy cập DB**.
- [ ] Rules dependency rõ ràng (Api → Application → Domain; Infrastructure implement interfaces).
- [ ] Không circular dependency.

**Evidence:**
- [ ] Sơ đồ dependency (markdown/diagram).
- [ ] Danh sách “forbidden imports” (quy tắc).

---

## P1.C — Repo structure + standards chống file dài
**Artefacts yêu cầu:** `REPO_STRUCTURE.md`, `CODING_STANDARDS.md`, `REVIEW_CHECKLIST.md`
- [ ] Repo chia theo module/feature rõ ràng.
- [ ] Có quy tắc “file ≤ 300 LOC, function ≤ 50 LOC”.
- [ ] Có cơ chế enforce:
  - [ ] script/check (ví dụ: lint/CI) hoặc
  - [ ] checklist review bắt buộc
- [ ] Quy ước DTO/Mapper/Validator/Repository tách riêng.

**Evidence:**
- [ ] `REVIEW_CHECKLIST.md` có mục “Fail nếu file > 300 LOC”.

---

## P1.D — DB review & index strategy
**Artefacts yêu cầu:** `DB_REVIEW.md`, `INDEX_STRATEGY.md`, `MIGRATION_PLAN.md`
- [ ] Mapping bảng ↔ nghiệp vụ rõ ràng.
- [ ] Unique/dedup keys chỉ ra cụ thể:
  - invoices dedup partial unique index
- [ ] Liệt kê tối thiểu **10 index load-bearing** và query tương ứng:
  - customer list search
  - invoice list by customer/date
  - advances list by customer/date
  - receipts filter applied_period_start
  - audit logs by entity/date
  - staging rows by status
- [ ] Migration plan + seed roles + seed sellers.

**Evidence:**
- [ ] Có đoạn “EXPLAIN plan assumptions” hoặc ghi rõ cách đo.

---

## P1.E — UX navigation + wireframes field-level + role matrix
**Artefacts yêu cầu:** `UX_NAVIGATION_MAP.md`, `WIREFRAMES.md`, `ROLE_MATRIX_UI.md`
- [ ] Có mô tả đầy đủ màn:
  - Dashboard
  - Customers list + Customer detail tabs
  - Import wizard 3 bước
  - Advances + approve
  - Receipts + allocation preview + approve
  - Reports + export excel
  - Admin: users/roles, templates, period locks, audit viewer
- [ ] Mỗi màn có:
  - [ ] fields + validation
  - [ ] buttons/actions
  - [ ] permission (role + ownership)
  - [ ] empty/error/loading states
- [ ] UX cho kế toán: ít bước, filter nhanh, table rõ ràng.

---

# PHASE 2 — Backend Core (API + DB + Business)

## P2.A — Backend skeleton & infra
- [ ] Project structure đúng Clean Architecture (Api/Application/Domain/Infrastructure).
- [ ] Health endpoint `/health` OK.
- [ ] Swagger chạy được, auth integrated.
- [ ] Middleware:
  - [ ] correlation id
  - [ ] exception handler chuẩn
  - [ ] validation pipeline
  - [ ] structured logging

**Evidence:**
- [ ] `RUN_BACKEND.md` + `ENV_SAMPLE.md`
- [ ] Screenshot hoặc mô tả swagger endpoints.

---
- [ ] Health endpoint `/health/ready` kiểm tra DB connection.

## P2.B — Auth / RBAC / Ownership authorization
- [ ] Roles: ADMIN/SUPERVISOR/ACCOUNTANT/VIEWER (seed script).
- [ ] Login endpoint trả token/session.
- [ ] Policy checks:
  - [ ] role-based
  - [ ] ownership-based theo `customers.accountant_owner_id`
- [ ] Viewer bị chặn khỏi mọi endpoint ghi dữ liệu (commit/approve/void/lock).
- [ ] Audit log ghi các thao tác admin quan trọng.

**Testcases (pass/fail):**
- [ ] Accountant A **không** approve receipt/advance của khách thuộc Accountant B.
- [ ] Viewer **không** commit import.
- [ ] Admin có thể override lock (bắt buộc lý do) và có audit.

---

## P2.C — Import Pipeline (Invoices + Advances) — staging/preview/commit/rollback
### P2.C.1 Upload → staging
- [ ] Upload tạo `import_batches` status STAGING.
- [ ] Parser đọc `ReportDetail.xlsx`:
  - [ ] detect `seller_tax_code` từ header `[03]Mã số thuế:`; fail → yêu cầu user chọn seller
- [ ] Insert `import_staging_rows` raw_data JSONB + validation_status/messages.

- [ ] Receipt import template includes seller_tax_code, customer_tax_code, receipt_date,
  applied_period_start, amount, method, description.

### P2.C.2 Preview
- [ ] Endpoint preview trả:
  - [ ] counts OK/WARN/ERROR
  - [ ] danh sách row lỗi/warn (phân trang)
  - [ ] duplicates (trong file + trong DB)
- [ ] Quy tắc validation:
  - [ ] receipt missing seller/customer/date/amount => ERROR
  - [ ] applied_period_start missing => ERROR
  - [ ] thiếu MST/name/invoice_no/issue_date → ERROR
  - [ ] revenue/vat âm => ERROR (trừ invoice_type=ADJUST: cho phép âm, total <= 0)
  - [ ] total mismatch → WARN (nếu áp dụng)
  - [ ] duplicate DB → WARN (default SKIP)

### P2.C.3 Commit (transactional)
- [ ] Chỉ commit khi không còn ERROR (WARN yêu cầu confirm).
- [ ] Upsert customers theo tax_code (tạo mới nếu chưa có).
- [ ] Insert invoices/advances theo dedup key; không nhân bản dữ liệu.
- [ ] Insert receipts as DRAFT (no allocations on import).
- [ ] Lưu `file_hash` (SHA-256) để cảnh báo import lại.
- [ ] Update batch status COMMITTED + summary_data.
- [ ] Audit log batch commit.
- [ ] Commit idempotent: nếu `idempotency_key` đã COMMITTED thì trả kết quả cũ.
- [ ] Init `outstanding_amount` cho invoices/advances = total/amount khi commit.

### P2.C.4 Rollback (ADMIN)
- [ ] Rollback chỉ cho admin (hoặc supervisor nếu cho phép).
- [ ] Chỉ rollback batch COMMITTED và **không vi phạm period lock policy**.
- [ ] Rollback gỡ dữ liệu tạo bởi batch (soft delete/void theo policy).
- [ ] Audit log rollback.

**Testcases (pass/fail):**
- [ ] Import sample `ReportDetail.xlsx` → staging preview có dữ liệu.
- [ ] Commit tạo invoices + customers mới.
- [ ] Import lại cùng file → cảnh báo file_hash.
- [ ] Rollback batch → invoices từ batch không còn tính vào công nợ.

---

- [ ] Import receipt file => staging preview OK.
- [ ] Commit receipts => receipts created as DRAFT.

## P2.D — Advances (Trả hộ) + approve
- [ ] Create advance DRAFT.
- [ ] Approve advance:
  - [ ] ownership enforced (kế toán phụ trách hoặc supervisor/admin)
  - [ ] ghi approved_by/approved_at
- [ ] DRAFT không tính công nợ; APPROVED mới tính.
- [ ] Void rule rõ ràng:
  - [ ] nếu đã allocate thì phải reversal/hoặc bắt buộc unallocate trước (chọn 1 policy, document)
- [ ] Audit log cho create/approve/void.

---
- [ ] Approve cập nhật `outstanding_amount` + `customers.current_balance` (cached).

## P2.E — Receipts + AllocationEngine (CORE)
### P2.E.1 Receipts workflow
- [ ] Create receipt DRAFT.
- [ ] Approve receipt:
  - [ ] ownership enforced
  - [ ] transaction insert allocations + compute unallocated_amount
  - [ ] update invoice/advance statuses (OPEN/PARTIAL/PAID) (nếu implement)
- [ ] Update `outstanding_amount` + `customers.current_balance` khi approve receipt.

### P2.E.2 AllocationEngine rules (must match spec)
- [ ] BY_INVOICE: chỉ allocate trong targets được chọn; không “đi lan” sang target khác.
- [ ] BY_PERIOD: ưu tiên nợ phát sinh trong `applied_period_start` trước; dư → FIFO.
- [ ] FIFO: trừ nợ cũ nhất còn outstanding.
- [ ] Overpay: unallocated_amount > 0.

### P2.E.3 Allocation preview
- [ ] Endpoint preview trả list allocations để UI hiển thị/chỉnh (option).

### P2.E.4 Unit tests bắt buộc (AllocationEngine)
- [ ] BY_INVOICE allocation đúng thứ tự và không vượt outstanding.
- [ ] BY_PERIOD: month-first rồi FIFO.
- [ ] FIFO: oldest-first.
- [ ] Overpay: tạo unallocated_amount.
- [ ] Mix invoice + advance.

**Acceptance tests (pass/fail):**
- [ ] Tạo receipt BY_PERIOD cho tháng có nợ → trừ đúng tháng trước.
- [ ] Nếu dư → trừ tiếp nợ cũ nhất.
- [ ] Nếu chọn hóa đơn rõ ràng → trừ đúng hóa đơn đó.

---

## P2.F — Period lock enforcement
- [ ] Endpoints lock/unlock (admin/supervisor).
- [ ] Enforcement:
  - [ ] receipt approve chặn applied_period_start thuộc kỳ lock
  - [ ] import commit chặn ghi vào kỳ lock (policy được document)
  - [ ] advance approve chặn kỳ lock (policy được document)
- [ ] Admin override phải nhập lý do + audit.

**Testcase:**
- [ ] Lock 2025-12 → receipt applied_period_start=2025-12-01 bị chặn (non-admin).

---

## P2.G — Reports + Export Excel template
- [ ] Summary endpoints:
  - by customer / owner / seller / period
  - công nợ tính đúng theo allocations + advances approved
- [ ] Statement 1 khách: opening/movements/closing.
- [ ] Aging report asOfDate với buckets đúng.
- [ ] Export Excel:
  - [ ] load template `Mau_DoiSoat_CongNo_Golden.xlsx`
  - [ ] fill TongHop/ChiTiet/Aging
  - [ ] file download mở được, dữ liệu khớp report API

**Testcase:**
- [ ] Xuất excel tháng bất kỳ → file mở OK, cột có dữ liệu, không lỗi format.

---

# PHASE 3 — Frontend (UI/UX)

## P3.A — Frontend foundation
- [ ] Layout + routing + auth guard.
- [ ] PermissionGuard (role/ownership) để ẩn/disable nút đúng người.
- [ ] Shared DataTable (server pagination/filter/sort).
- [ ] Error handling + toast.

**Testcase:**
- [ ] Viewer đăng nhập → không thấy nút commit/approve/rollback.

---

## P3.B — Import Wizard UI
- [ ] Step 1: upload file + chọn period + chọn seller nếu cần.
- [ ] Step 2: preview OK/WARN/ERROR, duplicates, search/filter staging.
- [ ] Step 3: commit + summary + batch history.
- [ ] Rollback chỉ admin.

**Testcase:**
- [ ] Upload sample file → preview hiển thị đúng số dòng, commit OK.

---
- [ ] Batch history list (file_name, created_at, status, summary) + detail/rollback (admin).

- [ ] Step 1: select import type (INVOICE/ADVANCE/RECEIPT).
## P3.C — Customers UI + Customer Detail
- [ ] List khách: search MST/tên (debounce), filter owner/status.
- [ ] Customer detail:
  - Overview cards: debt/overdue/credit
  - Tabs: invoices/advances/receipts/statement/audit
- [ ] Approve nút chỉ hiện đúng ownership.

---

## P3.D — Receipts UI + Allocation Preview (màn core)
- [ ] Toggle mode: BY_INVOICE / BY_PERIOD / FIFO.
- [ ] BY_INVOICE: pick invoices/advances outstanding.
- [ ] BY_PERIOD: month picker (applied_period_start).
- [ ] Preview allocations table: outstanding + allocated (editable nếu cho phép).
- [ ] Approve flow.
- [ ] Show unallocated credit.

**Testcase:**
- [ ] BY_PERIOD trừ đúng tháng; dư → FIFO; overpay → credit.

---

## P3.E — Reports UI + Export
- [ ] Summary report filters: seller/customer/owner/period
- [ ] Aging report
- [ ] Export excel download thành công

---

# PHASE 4 — Hardening, QA, Deploy (Windows)

## P4.A — Performance pass
- [ ] Paging ở mọi list endpoint.
- [ ] Index usage kiểm chứng (EXPLAIN ANALYZE).
- [ ] Dashboard response time mục tiêu < 2s (dataset lớn) — ghi rõ dataset giả lập.
- [ ] Option: cache/materialized view nếu cần (document).

**Evidence:**
- [ ] `PERFORMANCE_NOTES.md` + kết quả đo.

---

## P4.B — QA checklist & UAT scripts
- [ ] `QA_CHECKLIST.md` bao phủ:
  - Import (missing/trùng/import lại/rollback)
  - Advances approve ownership
  - Receipts allocation 3 mode + overpay
  - Period lock
  - Reports & export excel
- [ ] UAT script cho kế toán (user journeys end-to-end).

---

## P4.C — Deploy Windows (LAN)
- [ ] `DEPLOYMENT_GUIDE.md` gồm:
  - Cài PostgreSQL service
  - cấu hình `pg_hba.conf` allow subnet LAN + firewall
  - tạo role `congno_app` quyền tối thiểu
  - backup scheduled task: pg_dump retention
  - triển khai backend (Windows service/IIS reverse proxy)
  - triển khai frontend build
- [ ] Có `BACKUP_RESTORE_GUIDE.md` (hoặc phần riêng) hướng dẫn restore.
- [ ] Có checklist runbook khi lỗi (log ở đâu, restart service, rollback migration).

---

## 5) Gate tổng nghiệm thu (Go/No-Go)
Chỉ “Go-live” khi:
- [ ] Tất cả mục **Non‑negotiables** được tick.
- [ ] Phase 2 pass các testcase core (import/approve/allocation/lock/export).
- [ ] Phase 3 UI pass end-to-end UAT.
- [ ] Có backup/restore chạy thử thành công.
- [ ] Không có file vi phạm giới hạn LOC/function length (có report).


---

## WEB-ONLY (Client không cài đặt) — Điều kiện bổ sung
- [ ] Client chạy trên **trình duyệt** (Edge/Chrome) trong LAN; **không** build WPF/Electron.
- [ ] Deploy cập nhật **1 nơi trên Server1**; client không cần cài/update.
- [ ] Mọi bảng list dùng **server-side pagination/filter/sort**.
- [ ] Có **PWA minimal** (manifest + icon + install/pin) để dùng như app.


