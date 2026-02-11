# QA_CHECKLIST

## Preconditions
- [ ] Backend API running and reachable (health/ready OK).
- [ ] Frontend running and points to correct API base URL.
- [ ] Database seeded with sample data (sellers, customers, users).
- [ ] Admin account available for test.

## Auth and Roles
- [ ] Login as admin succeeds and token stored.
- [ ] Role guard blocks unauthorized pages (viewer cannot open admin pages).
- [ ] Logout clears token and redirects to /login.

## Import Pipeline
- [ ] Upload INVOICE file valid -> staging counts correct.
- [ ] Preview shows OK/WARN/ERROR and filter works.
- [ ] Missing required columns -> ERROR rows.
- [ ] Duplicate invoiceNo or idempotency_key -> handled as expected.
- [ ] Commit with idempotency_key is idempotent (second commit does not duplicate).
- [ ] Rollback reverts balances and marks batch rolled back.
- [ ] Import history shows createdBy, period range, summary.
- [ ] Receipt import parses applied_period_start correctly.
- [ ] Period lock blocks commit unless override.

## Advances
- [ ] Create advance -> status DRAFT, outstanding = amount.
- [ ] Approve by owner -> status APPROVED and balance updated.
- [ ] Approve by non-owner -> forbidden.
- [ ] Void -> status VOID, balance reverted.
- [ ] Period lock blocks approve/void unless override.

## Receipts
- [ ] Preview FIFO allocates by oldest outstanding.
- [ ] Preview BY_PERIOD uses applied_period_start.
- [ ] Preview BY_INVOICE uses selected targets.
- [ ] Overpay -> unallocatedAmount > 0 (credit).
- [ ] Approve persists allocations and updates balances.
- [ ] Void reverses allocations and balances.
- [ ] Period lock blocks approve/void unless override.

## Period Lock
- [ ] Create lock for MONTH / QUARTER / YEAR.
- [ ] Commit import blocked for locked period.
- [ ] Override requires reason and audit log entry.

## Reports and Export
- [ ] Summary report filters seller/customer/owner/period.
- [ ] Statement report shows applied period and running balance.
- [ ] Aging report generates buckets.
- [ ] Export Excel downloads and opens correctly.
- [ ] Dates in export use DD/MM/YYYY format.

## Admin
- [ ] Users list pagination and search work.
- [ ] Update roles for a user and re-login reflects change.
- [ ] Activate/deactivate user works and is logged.
- [ ] Audit log filters and detail view work.

## UI and UX
- [ ] All list screens use server-side pagination.
- [ ] Date inputs use date picker and store YYYY-MM-DD.
- [ ] Date display uses DD/MM/YYYY.

## Automation (optional)
- [ ] Run `scripts/e2e/smoke.ps1` with admin credentials and verify all steps pass.
