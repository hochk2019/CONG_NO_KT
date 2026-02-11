# IMPORT_TEMPLATES

## INVOICE (hỗ trợ 2 định dạng)
- Hệ thống tự nhận `ExportData` nếu file có sheet này; nếu không sẽ đọc sheet đầu tiên.
- Tự nhận cả template đơn giản và file ReportDetail.xlsx xuất từ phần mềm hóa đơn.
- Thứ tự cột không quan trọng, miễn đúng tên cột theo template/report.

Template đơn giản (sheet: `ImportInvoice`):
- Dòng 1: `MaSoThue` + MST người bán.
- Header dòng 5: `STT`, `BuyerName`, `TaxCode`, `RevenueExcludingVAT`, `VatAmount`, `Note`.
- Header dòng 6: `KyHieuMau`, `SoHieuHoaDon`, `SoHoaDon`, `NgayThangNamPhatHanh`.
- Ngày theo định dạng `yyyy-MM-dd`.
Template file (UI download):
- `src/frontend/public/templates/invoice_template.xlsx`.

## ADVANCE (mẫu đơn giản)
Header (không phân biệt thứ tự, không phân biệt hoa thường):
- `seller_tax_code`
- `customer_tax_code`
- `advance_date` (yyyy-MM-dd)
- `amount`
- `description` (tùy chọn)
Template: `src/frontend/public/templates/advance_template.xlsx`.

## RECEIPT (mẫu đơn giản)
Header (không phân biệt thứ tự, không phân biệt hoa thường):
- `seller_tax_code`
- `customer_tax_code`
- `receipt_date` (yyyy-MM-dd)
- `applied_period_start` (yyyy-MM-dd, ngày đầu tháng)
- `amount`
- `method` (BANK/CASH/OTHER, tùy chọn)
- `description` (tùy chọn)
Template: `src/frontend/public/templates/receipt_template.xlsx`.
