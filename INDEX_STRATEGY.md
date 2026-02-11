# INDEX_STRATEGY - Cong No Golden

## Current load-bearing indexes (and queries)
1) uq_invoices_dedup: prevent duplicate invoices on import.
2) ix_invoices_customer_date: list invoices by customer/date.
3) ix_invoices_seller_date: list invoices by seller/date.
4) ix_advances_customer_date: list advances by customer/date.
5) ix_receipts_customer_date: list receipts by customer/date.
6) ix_receipts_applied_period: filter receipts by applied_period_start.
7) ix_alloc_receipt: list allocations by receipt.
8) ix_alloc_invoice: list allocations for invoice outstanding.
9) ix_alloc_advance: list allocations for advance outstanding.
10) ix_import_batches_filehash: warn duplicate file imports.
11) ix_import_batches_created_at: import history list.
12) ix_staging_batch + ix_staging_status: preview staging rows by batch/status.
13) ix_customers_name_trgm + ix_customers_name_search_trgm: search customers by name (accent-insensitive).
14) ix_invoices_issue_date: report ranges without customer/seller filter.
15) ix_advances_advance_date: report ranges without customer/seller filter.
16) ix_receipts_approved_date + ix_receipts_customer_date_approved + ix_receipts_seller_date_approved: report ranges for approved receipts.
17) ix_audit_logs_entity + ix_audit_logs_created_at: audit by entity/date.

## Recommended additional indexes
- Consider composite report indexes if query plans show heavy seq scans.
