# DB_REVIEW - Cong No Golden

## Core tables and intent
- users, roles, user_roles: auth and RBAC.
- sellers, customers: master data; customers has `current_balance` cache.
- import_batches, import_staging_rows: staging import with idempotency.
- invoices: revenue/vat/total; `invoice_type=ADJUST` stores negative amounts; `outstanding_amount` cache.
- advances: approved advances count as receivables; `outstanding_amount` cache.
- receipts: receipt workflow and unallocated credit.
- receipt_allocations: allocations with explicit `invoice_id` or `advance_id` and check constraint.
- period_locks: lock accounting periods.
- audit_logs: before/after JSONB.

## Integrity rules
- Soft delete for invoices/advances/receipts.
- Allocation references use FK to invoice/advance.
- ADJUST invoice amounts must be <= 0.
- Idempotency key unique on import batches.

## Cached balance rules
- On commit: set invoice/advance outstanding = total/amount.
- On approve receipt/advance: update outstanding and customers.current_balance.
- Updates occur in application transaction (no DB triggers).

## Search
- pg_trgm enabled for fast name search.
- Optional: store normalized name for no-diacritic search if needed.
