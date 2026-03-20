# IMPORT_TEMPLATES

Ba template do UI cung cap deu theo mot chuan chung:

- Sheet dau tien la `Data` va duoc he thong dung de staging import.
- Dong 1 la header co dinh, dong 2 la du lieu mau de tham chieu.
- Sheet thu hai la `HuongDan` de mo ta cot, quy tac nhap lieu va quy tac chong trung.
- Khong doi ten header. Co the sap xep lai thu tu cot, nhung khuyen nghi giu nguyen de doi soat noi bo.
- Ngay ho tro: `yyyy-MM-dd`, `dd/MM/yyyy`, `dd-MM-yyyy`.

## INVOICE

He thong ho tro 2 nguon:

1. Template chuan tai `src/frontend/public/templates/invoice_template.xlsx`
2. `ReportDetail.xlsx` neu file co sheet `ExportData`

Header chuan cua template:

- `seller_tax_code`
- `customer_tax_code`
- `customer_name`
- `invoice_template_code`
- `invoice_series`
- `invoice_no`
- `issue_date`
- `revenue_excl_vat`
- `vat_amount`
- `total_amount`
- `note`

Quy tac chong trung voi du lieu da co tren he thong:

- `seller_tax_code + customer_tax_code + invoice_series + invoice_no + issue_date`

Ghi chu:

- `total_amount` co the de `0`; he thong se tu tinh `revenue_excl_vat + vat_amount`.
- Hoa don dieu chinh giam van duoc ho tro nhu truoc thong qua `note` va parser hien hanh.

## ADVANCE

Template chuan: `src/frontend/public/templates/advance_template.xlsx`

Header chuan:

- `seller_tax_code`
- `customer_tax_code`
- `advance_no`
- `advance_date`
- `amount`
- `description`

Quy tac nghiep vu:

- `advance_no` la bat buoc.
- He thong chong trung voi du lieu da co theo:
  - `seller_tax_code + customer_tax_code + advance_no`
- Trong chinh file import, he thong cung dedup theo `advance_no`; dong trung se bi canh bao va `SKIP`.

## RECEIPT

Template chuan: `src/frontend/public/templates/receipt_template.xlsx`

Header chuan:

- `seller_tax_code`
- `customer_tax_code`
- `receipt_no`
- `receipt_date`
- `applied_period_start`
- `amount`
- `method`
- `description`

Quy tac nghiep vu:

- `receipt_no` la bat buoc.
- `applied_period_start` nen la ngay dau thang cua ky doi soat.
- `method` ho tro `BANK`, `CASH`, `OTHER`.
- He thong chong trung voi du lieu da co theo:
  - `seller_tax_code + customer_tax_code + receipt_no`
- Trong chinh file import, he thong cung dedup theo `receipt_no`; dong trung se bi canh bao va `SKIP`.
