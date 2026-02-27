> [!IMPORTANT]
> **HISTORICAL DOCUMENT**
> Tài liệu này là snapshot/lịch sử để tham khảo, **không phải nguồn vận hành chuẩn hiện tại**.
> Nguồn chuẩn hiện tại:
> - Deploy: DEPLOYMENT_GUIDE_DOCKER.md
> - Runbook: RUNBOOK.md
> - Ops runtime: docs/OPS_ADMIN_CONSOLE.md
# APP THEO DÕI CÔNG NỢ GOLDEN — SPEC TRIỂN KHAI (WEB + LAN + PostgreSQL + Windows)

> **Bản chốt cuối** cho Agent lập trình.  
> Mục tiêu: **đẹp – đủ chức năng – tiện lợi – chạy mượt LAN – an toàn dữ liệu – dễ bảo trì – cập nhật 1 nơi**.

---

## 0) Chốt kiến trúc (QUYẾT ĐỊNH CUỐI)

- **Client = WEB** (React) chạy trên trình duyệt (Edge/Chrome).  
  ✅ Không cài đặt/update từng máy; deploy 1 nơi trên Server1.  
  ✅ Có thể bật **PWA** để “Pin/Install” như app.
- **Server1 (Windows)**:
  - PostgreSQL (DB) — `congno_golden`
  - Backend API (khuyến nghị .NET 8 Web API)
  - Host Frontend (static) bằng IIS hoặc backend serve static
- **KHÔNG cho client truy cập DB trực tiếp**. Client chỉ gọi API.

Quy mô: 5–10 người dùng đồng thời trong LAN.

---

## 1) Nguồn dữ liệu & yêu cầu nghiệp vụ

### 1.1 EasyInvoice (Excel ReportDetail)
- Tải thủ công từ:
  - `https://2300328765.easyinvoice.com.vn/ReportsInv/report`
  - vào báo cáo, chọn kỳ, “Xuất dữ liệu chi tiết” → trang ReportPrint → “Xuất Excel”
- File Excel: chứa các thông tin hóa đơn:
  - MST người mua, Tên công ty, Số hóa đơn, Tiền trước thuế, Tiền VAT, (ngày phát hành nếu có)
- Hệ thống phải có cơ chế:
  - **Import wizard** (staging → preview → commit)
  - cảnh báo **file đã import** (file_hash)
  - cảnh báo trùng hóa đơn (trong DB) và trùng trong file

### 1.2 Multi-seller (đa MST bên bán)
- Seller A: MST 2300328765
- Seller B: MST 2301098313
→ Mọi dữ liệu hóa đơn/thu/đối soát phải filter được theo seller.

### 1.3 Theo dõi công nợ
- Công nợ lấy từ:
  - Hóa đơn: **Doanh thu + VAT**
  - Trả hộ: khoản không xuất hóa đơn, không ghi nhận doanh thu nhưng phải thu hồi nợ

- Quy ước hóa đơn điều chỉnh: `invoice_type = ADJUST` lưu **giá trị âm** (giảm công nợ) để SUM tự nhiên.
- Hiệu năng: dùng `current_balance` (customer) và `outstanding_amount` (invoice/advance) cache, cập nhật khi commit/approve/void.

### 1.4 Thu tiền (Receipt) — chốt nghiệp vụ
- App **có ghi nhận Thu tiền**
- Thu có thể:
  - theo hóa đơn (BY_INVOICE) nếu kế toán nhập rõ hóa đơn
  - theo tháng (BY_PERIOD) nếu khách trả gộp theo tháng
  - hoặc FIFO (trừ nợ cũ nhất)
- Quy tắc mặc định:
  - Nếu user chọn BY_INVOICE → trừ đúng chứng từ chọn
  - Nếu user nhập “gộp” không chỉ định → **trừ lùi từ nợ cũ nhất** (FIFO)
  - Nếu BY_PERIOD → ưu tiên nợ phát sinh trong tháng applied trước, dư → FIFO
- Có trường hợp muốn ghi nhận vào kỳ trước dù tiền về kỳ sau → phải có:
  - `receipt_date` (ngày tiền về)
  - `applied_period_start` (ngày đầu tháng đối soát, YYYY-MM-01)

### 1.5 Trả hộ (Advance) — duyệt theo ownership
- Kế toán theo dõi khách nào thì kế toán đó duyệt
- Kế toán trưởng giám sát/duyệt hộ

---

### 1.6 Receipt import (Excel)
- Import receipts from a template (seller_tax_code, customer_tax_code, receipt_date,
  applied_period_start, amount, method, description).
- Imported receipts are created as DRAFT; approval/allocations happen later.


## 2) Phân quyền (RBAC) & ownership

Roles:
- ADMIN
- SUPERVISOR (kế toán trưởng)
- ACCOUNTANT
- VIEWER

Ownership:
- Customer có `accountant_owner_id`
- ACCOUNTANT chỉ approve/void với khách mình phụ trách
- SUPERVISOR/ADMIN approve/void tất cả (có lý do + audit)

---

## 3) Module chức năng (bắt buộc tách module)

### 3.1 Master data
- Sellers (MST bên bán)
- Customers (MST người mua là khóa nghiệp vụ)
- Users/roles

### 3.2 Import module (Invoices + Advances + Receipts)
- Batch import: upload file → parse → staging rows
- Preview: OK/WARN/ERROR + lý do
- Commit: transactional, tạo customer mới nếu cần, insert invoices/advances, log audit
- 
- Receipt import creates DRAFT receipts (no allocations on import).
- Required fields: seller_tax_code, customer_tax_code, receipt_date,
  applied_period_start, amount. Method defaults to BANK if missing.

Rollback batch (admin)

- Commit phải **idempotent** (client gửi `idempotency_key`; gọi lại không tạo dữ liệu trùng).

### 3.3 Advances (Trả hộ)
- DRAFT → APPROVED
- APPROVED mới tính công nợ
- Không sửa amount sau khi có allocation; chỉ VOID + reversal

### 3.4 Receipts + AllocationEngine (cốt lõi)
- Receipt DRAFT → APPROVED
- allocation_mode: BY_INVOICE / BY_PERIOD / FIFO
- Allocation preview trước khi approve
- Overpay → `unallocated_amount` (credit)
- Unit tests cho AllocationEngine (bắt buộc)

### 3.5 Reports + Excel export
- Dashboard tổng
- Báo cáo công nợ theo kỳ (ngày/tháng/quý/năm)
- Statement theo khách
- Aging
- Export Excel theo template nội bộ

### 3.6 Period lock + Audit
- Lock kỳ MONTH/QUARTER/YEAR
- Khi lock: chặn commit/approve/void trong kỳ (trừ override)
- Audit log before/after JSONB cho mọi hành động quan trọng

---

## 4) UI/UX (WEB) — đẹp & tiện

### 4.1 Navigation
- Dashboard
- Customers (list + detail tabs)
- Import wizard
- Advances
- Receipts (allocation preview)
- Reports + Export
- Admin (Users/Roles, Period locks, Audit viewer)

### 4.2 Nguyên tắc trải nghiệm mượt
- Tất cả bảng list: **server-side pagination/filter/sort**
- Search debounce
- Loading/empty/error states rõ ràng
- Actions nguy hiểm có confirm + yêu cầu lý do (approve/void/rollback/override lock)

### 4.3 PWA minimal
- manifest + icons + installable
- không yêu cầu offline

---

## 5) Tiêu chuẩn code & bảo trì (bắt buộc)
- Clean Architecture: Domain / Application / Infrastructure / API / Web
- Không nhồi code: file ≤ 300 LOC; function ≤ 50 LOC
- Controller mỏng; business logic nằm trong usecase/service; tách module rõ


