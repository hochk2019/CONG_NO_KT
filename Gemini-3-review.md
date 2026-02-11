# Review & Đánh giá Kế hoạch Dự án App Đối Soát Công Nợ Golden (PostgreSQL + WEB)

Chào bạn,

Sau khi rà soát chi tiết toàn bộ hồ sơ (Spec, Database Schema, API, Acceptance Criteria), mình đánh giá đây là một bản kế hoạch **RẤT CHẤT LƯỢNG**. Tư duy thiết kế (Design Thinking) rất mạch lạc, kiến trúc Clean Architecture chuẩn chỉ, và đặc biệt là độ chi tiết trong các quy tắc nghiệp vụ (Business Rules) rất cao.

Dưới đây là nhận xét chi tiết và các đề xuất **NÂNG CẤP/BỔ SUNG** để dự án hoàn thiện hơn nữa trước khi đưa vào code.

---

## 1. Đánh giá Tổng quan

*   **Kiến trúc (Architecture):** Mô hình **Web Application (React)** + **Backend API (.NET)** + **PostgreSQL** chạy trên Windows Server Local là sự lựa chọn tối ưu cho nhu cầu nội bộ (LAN). Nó giải quyết được bài toán "Deployment Hell" (không cần cài từng máy) và tận dụng được sức mạnh của Server.
*   **Cơ sở dữ liệu (Database):** Schema được thiết kế chuẩn, có sử dụng các tính năng nâng cao của PostgreSQL như `JSONB` (cho audit/staging), `UUID`, và `Partial Index` (cho dedup). Tư duy về "Staging Tables" cho việc Import là rất chuyên nghiệp.
*   **Nghiệp vụ (Business Logic):** Logic **Allocation Engine** (FIFO, By Invoice, By Period) là "trái tim" của hệ thống và đã được mô tả cực kỳ rõ ràng. Việc tách biệt `Advances` (Trả hộ) và `Receipts` (Thu tiền) cũng rất hợp lý.

---

## 2. Các vấn đề cần Sửa đổi / Làm rõ (CRITICAL)

Dù plan rất tốt, vẫn còn một số điểm "chết người" cần làm rõ ngay để tránh bug khó sửa sau này:

### 2.1. Xử lý Hóa đơn Điều chỉnh (Invoice Adjustments)
*   **Vấn đề:** Trong `db_schema_postgresql.sql`, bảng `invoices` có `invoice_type` là `ADJUST` (Điều chỉnh). Tuy nhiên, các trường `revenue_excl_vat`, `vat_amount` đang để kiểu `numeric(18,2)`.
*   **Câu hỏi:** Giá trị tiền của hóa đơn điều chỉnh sẽ được lưu là **Số Âm** hay **Số Dương** kèm flag?
*   **Rủi ro:** Nếu lưu số dương và chỉ dựa vào type `ADJUST` để trừ, thì khi tính tổng công nợ `SUM()` sẽ bị sai (cộng thêm thay vì trừ đi).
*   **Đề xuất:** Quy ước cứng: **Hóa đơn điều chỉnh giảm / Hóa đơn thay thế bị hủy => Lưu giá trị TIỀN là SỐ ÂM** trong database. Như vậy các câu lệnh SQL `SUM(total_amount)` sẽ luôn đúng tự nhiên mà không cần `CASE WHEN` phức tạp.

### 2.2. Hiệu năng tính toán Số dư (Balance Calculation)
*   **Vấn đề:** Hiện tại công nợ được tính "on-the-fly" (tức thì) dựa trên `Allocations`. Khi dữ liệu lên tới hàng chục ngàn dòng, việc query `SUM` lịch sử để ra số dư hiện tại mỗi khi load danh sách khách hàng sẽ rất chậm.
*   **Đề xuất:** Bổ sung trường `current_balance` (cached) vào bảng `customers`.
    *   Trường này sẽ được cập nhật lại mỗi khi có giao dịch `COMMITTED` hoặc `APPROVED` thông qua Database Trigger hoặc code Backend.
    *   Giúp việc hiển thị Dashboard và List khách hàng nhanh tức thì (< 0.5s).

### 2.3. Logic "Applied Period"
*   **Vấn đề:** Logic `applied_period_start` cho phép ghi nhận thu tiền vào kỳ quá khứ.
*   **Rủi ro:** Nếu User ghi nhận tiền vào tháng 10 (đã chốt sổ/xuất báo cáo) trong khi thực tế đang là tháng 12, báo cáo cũ sẽ bị sai lệch.
*   **Đề xuất:** Cần làm rõ quy tắc **"Lock Period"** (Khóa kỳ). Nếu kỳ 10 đã Locked, thì tuyệt đối không cho phép chọn `applied_period_start` là tháng 10 nữa, trừ khi Admin mở khóa (Unlock).

---

## 3. Các đề xuất Nâng cấp / Bổ sung (NICE TO HAVE)

### 3.1. Về Database (PostgreSQL)
*   **Full Text Search Tiếng Việt:** Schema đang dùng `pg_trgm` (trigram) là tốt cho tìm kiếm tên (`LIKE '%abc%'`). Tuy nhiên, nên cân nhắc thêm cấu hình `unaccent` để tìm kiếm không dấu (ví dụ gõ "hoang kim" ra "Hoàng Kim").
    *   *Action:* Thêm extension `unaccent` và tạo index hỗ trợ hàm này.
*   **Concurrency Control:** Bảng `invoices` có cột `version` cho optimistic locking. Hãy chắc chắn FE và BE gửi kèm `version` này khi update để tránh 2 kế toán sửa cùng 1 khách/hóa đơn đè dữ liệu nhau.

### 3.2. Về Frontend (React)
*   **Import Large File UX:** File Excel có thể rất nặng.
    *   *Action:* Khi upload, UI nên hiển thị **Progress Bar** thật.
    *   *Action:* Nếu file > 1000 dòng, cơ chế Preview nên lười tải (Lazy Load) hoặc phân trang (Pagination) ngay trong màn hình Preview, đừng render toàn bộ DOM một lúc sẽ treo trình duyệt.
*   **Keyboard Shortcuts:** Kế toán rất thích dùng phím tắt. Hãy yêu cầu ChatGPT thêm tính năng: `Enter` để lưu, `Esc` để đóng, `Mũi tên` để di chuyển giữa các dòng nhập liệu.

### 3.3. Về API & Security
*   **API Validation Strictness:** File `openapi` hiện hơi sơ sài về phần Schema `requestBody`. Cần định nghĩa rõ ràng max length, required fields để Frontend gen code TS chuẩn hơn.
*   **Mật khẩu:** Trong `users` table, bắt buộc dùng thuật toán hash mạnh như **BCrypt** hoặc **Argon2**, tuyệt đối không dùng MD5 hay SHA1.
*   **HTTPS:** Mặc dù LAN, nhưng nếu có thể, hãy cấu hình **Self-Signed Certificate** cho IIS để chạy HTTPS. Tránh việc user login bị "sniff" pass trong mạng nội bộ.

### 3.4. Tính năng "Gợi ý phân bổ" (Auto Allocate Suggestion)
*   Khả năng cao kế toán sẽ lười chọn từng hóa đơn.
*   Tính năng: Nút **"Gợi ý"** tự động tích chọn các hóa đơn cũ nhất sao cho tổng bằng đúng số tiền thu được. User chỉ cần review và nhấn Approve.

---

## 4. Kết luận

Kế hoạch này **ĐÃ ĐỦ ĐIỀU KIỆN ĐỂ BẮT ĐẦU CODE (Execution Phase)**.
Bạn chỉ cần gửi lại các điểm mục **2. (Critical)** để ChatGPT xác nhận/update lại Spec một chút là có thể bắt tay vào làm ngay.

**Đánh giá điểm số kế hoạch:** 9/10.

Chúc dự án thành công!

---

# PHẦN BỔ SUNG TỪ OPUS 4.5

Sau khi review sâu hơn các file `UI_PROTOTYPE`, `DEPLOYMENT_GUIDE`, `prompt_antigravity_phases.md` và `ACCEPTANCE_CRITERIA`, mình có thêm một số nhận định quan trọng:

---

## 5. Các vấn đề CRITICAL bổ sung

### 5.1. Data Integrity - Thiếu Foreign Key Constraint cho `receipt_allocations.target_id`

**Vấn đề:** Trong `db_schema_postgresql.sql`, bảng `receipt_allocations` có:
```sql
target_type varchar(16) NOT NULL, -- INVOICE/ADVANCE
target_id uuid NOT NULL,
```
Nhưng **KHÔNG CÓ Foreign Key** tới `invoices.id` hoặc `advances.id`. Đây là "Polymorphic Association" - một anti-pattern dễ gây **Orphan Records** (bản ghi mồ côi).

**Rủi ro:** Nếu ai đó xóa invoice mà allocation vẫn trỏ tới → Dữ liệu hỏng, báo cáo sai.

**Đề xuất giải pháp (chọn 1 trong 2):**
1. **Soft Delete Only:** Tuyệt đối không cho phép `DELETE` vật lý trên `invoices`/`advances`. Chỉ dùng `deleted_at` (đã có sẵn trong schema). Thêm CHECK constraint hoặc Trigger để chặn DELETE.
2. **Tách bảng allocation:** Tạo 2 bảng `receipt_invoice_allocations` và `receipt_advance_allocations` với FK rõ ràng.

### 5.2. Thiếu Trường `outstanding_amount` trên Invoice/Advance

**Vấn đề:** Hiện tại để biết "Còn phải thu" của 1 hóa đơn, phải:
```sql
SELECT total_amount - COALESCE(SUM(allocations.amount), 0) 
FROM invoices LEFT JOIN receipt_allocations ...
```
Điều này **rất chậm** khi có nhiều allocations.

**Đề xuất:** Thêm trường `outstanding_amount` (cached) vào `invoices` và `advances`:
- Được cập nhật mỗi khi `receipt_allocations` thay đổi.
- Giúp query "Danh sách hóa đơn chưa thu hết" cực nhanh.

### 5.3. Thiếu Cơ chế Retry/Idempotency cho Import Commit

**Vấn đề:** Nếu user nhấn "Commit" → mạng bị ngắt giữa chừng → transaction có thể bị treo hoặc commit một nửa.

**Đề xuất:**
- Thêm trường `idempotency_key` (UUID do FE generate) vào `import_batches`.
- Backend check: nếu `idempotency_key` đã tồn tại và status = COMMITTED → trả về kết quả cũ, không commit lại.
- Điều này đảm bảo user nhấn Commit 2 lần không bị nhân đôi dữ liệu.

---

## 6. Các vấn đề về UI/UX Prototype

### 6.1. Thiếu Màn Hình "Batch History" cho Import

**Quan sát:** UI Prototype có wizard 3 bước (Upload → Preview → Commit), nhưng **không thấy màn hình xem lịch sử các batch đã import**.

**Đề xuất:** Thêm tab/màn "Import History" hiển thị:
- Danh sách batch đã import (với `file_name`, `created_at`, `status`, `summary_data`)
- Nút "Rollback" cho Admin
- Nút "Xem chi tiết" để debug nếu có lỗi

### 6.2. Thiếu Cơ chế "Undo" cho VOID

**Vấn đề:** Nếu kế toán VOID nhầm 1 phiếu thu (receipts) → không có cách khôi phục (phải tạo lại từ đầu).

**Đề xuất:** Thay vì VOID cứng, có thể:
- VOID tạo 1 bản ghi "reversal" đảo ngược (giống nghiệp vụ kế toán đảo bút toán).
- Cho phép "Un-void" trong vòng 24h nếu chưa lock kỳ.

### 6.3. Dashboard Cần Thêm "Trend Chart"

**Quan sát:** Dashboard hiện chỉ có 4 KPI cards và bảng Top Nợ.

**Đề xuất:** Thêm 1 biểu đồ đường (Line Chart) hiển thị:
- Xu hướng Công nợ theo 6 tháng gần nhất
- Để lãnh đạo nhanh chóng thấy được tình hình tăng/giảm

---

## 7. Các vấn đề về Deployment & Operations

### 7.1. Thiếu Monitoring/Alerting

**Vấn đề:** `DEPLOYMENT_GUIDE` rất chi tiết về cài đặt, nhưng **không đề cập Health Monitoring**.

**Đề xuất:**
- Thêm endpoint `/health/ready` (kiểm tra DB connection) bên cạnh `/health`.
- Cấu hình Windows Event Log hoặc file log rotation.
- Có thể dùng **Prometheus + Grafana** (miễn phí) để theo dõi metrics.

### 7.2. Backup Chỉ Có Local - Thiếu Offsite

**Vấn đề:** Script backup chỉ lưu vào `C:\apps\congno\backup\dumps` trên cùng server.

**Rủi ro:** Nếu server chết (ổ cứng hỏng, cháy nổ) → mất toàn bộ.

**Đề xuất:** Thêm bước copy backup sang:
- NAS trong LAN, hoặc
- Cloud storage (OneDrive/Google Drive sync folder), hoặc
- USB external drive định kỳ

---

## 8. Edge Cases cần Test (Bổ sung cho QA)

| # | Scenario | Expected Behavior |
|---|----------|-------------------|
| 1 | Import file có 2 invoice trùng nhau trong cùng 1 file | Cảnh báo WARN, chỉ insert 1 |
| 2 | Receipt amount > tổng outstanding của customer | Tạo `unallocated_amount` (credit) |
| 3 | Approve receipt khi customer đang không có outstanding | Credit 100% |
| 4 | Lock kỳ 12/2025 → User tạo receipt `applied_period=12/2025` | Chặn, trừ khi Admin override |
| 5 | Rollback batch sau khi đã có allocation vào invoice của batch đó | Phải từ chối hoặc cascade void |
| 6 | 2 kế toán approve cùng 1 advance cùng lúc (race condition) | Chỉ 1 thành công (optimistic lock) |
| 7 | Import file Excel có encoding không phải UTF-8 | Parser phải xử lý được hoặc báo lỗi rõ ràng |

---

## 9. Nhận xét về Phase Assignment

File `prompt_antigravity_phases.md` chia rất hợp lý:
- **Phase 1 (Opus):** Architecture - đúng vì cần tư duy toàn cục
- **Phase 2-3 (Sonnet):** Implementation - đúng vì cần code nhanh
- **Phase 4 (Opus):** QA & Deploy - đúng vì cần review kỹ

**Tuy nhiên**, nên bổ sung:
- **Phase 2.5 (Sonnet hoặc Opus):** Integration Testing - test toàn bộ flow từ Import → Approve → Allocate → Report
- Hiện tại chỉ có "unit tests cho AllocationEngine", chưa đủ cover hết

---

## 10. Tổng kết Bổ sung

| Hạng mục | Trước (Gemini review) | Sau (Opus bổ sung) |
|----------|----------------------|-------------------|
| Data Integrity | ✅ Covered | ⚠️ Thêm FK concern |
| Performance | ✅ `current_balance` cache | ⚠️ Thêm `outstanding_amount` |
| Error Handling | ❌ Chưa đề cập | ⚠️ Idempotency, Retry |
| Monitoring | ❌ Chưa đề cập | ⚠️ Health check, Logs |
| Backup | ✅ Local script | ⚠️ Offsite backup |
| Edge Cases | ❌ Chưa đề cập | ✅ 7 test scenarios |

---

## 11. Kết luận cuối cùng

Kế hoạch này vẫn **RẤT TỐT** (9/10). Các vấn đề mình bổ sung phần lớn là **"hardening"** (gia cố) chứ không phải thiết kế lại từ đầu.

**Khuyến nghị:** Gửi file review này cho ChatGPT và yêu cầu:
1. Xác nhận/Update các điểm Critical (Section 2 + 5)
2. Bổ sung edge cases vào `ACCEPTANCE_CRITERIA_BY_PHASE.md`
3. Thêm `idempotency_key` vào schema
4. Thêm `outstanding_amount` cached vào `invoices`/`advances`

**Sau đó có thể bắt tay vào Phase 1 (Architecture) được rồi!**

---

*Review bởi: Gemini + Opus 4.5*  
*Ngày: 2026-01-07*
