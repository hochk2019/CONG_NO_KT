# UAT_SCRIPT

## Scenario 1: Import invoice and advance
1. Login as accountant or admin.
2. Go to Import.
3. Upload INVOICE file for period range.
4. Review preview (OK/WARN/ERROR counts).
5. Commit batch.
6. Check import history shows committed batch with summary.
Expected:
- Commit success, counts match staging.
- History shows createdBy and period range.

## Scenario 2: Import receipt and allocate
1. Upload RECEIPT file.
2. Preview and commit.
3. Go to Receipts.
4. Preview allocation in FIFO mode.
5. Approve receipt.
Expected:
- Allocation lines created.
- Customer balance and outstanding amounts updated.

## Scenario 3: Manual allocation modes
1. In Receipts, use BY_INVOICE with selected targets.
2. Preview and confirm allocation lines match targets.
3. Use BY_PERIOD with applied_period_start.
Expected:
- Allocations follow selected targets and period.
- Overpay shows unallocated amount.

## Scenario 4: Period lock enforcement
1. In Admin > Period locks, lock current month.
2. Try import commit or approve receipt in locked period.
3. Attempt with override reason.
Expected:
- Blocked without override.
- With override, action succeeds and audit log records override.

## Scenario 5: Advances ownership
1. Create advance for a customer owned by accountant.
2. Approve as owner.
3. Try approve same advance as non-owner role.
Expected:
- Owner can approve.
- Non-owner gets forbidden.

## Scenario 6: Reports and export
1. Go to Reports.
2. Run Summary, Statement, Aging.
3. Export Excel.
Expected:
- Reports load with correct totals.
- Export file opens and dates use DD/MM/YYYY.

## Scenario 7: Admin user management
1. Go to Admin > Users.
2. Update roles for a user.
3. Deactivate user and verify login blocked.
4. Check audit log entry.
Expected:
- Role change effective after re-login.
- Deactivated user cannot login.
- Audit log shows before/after.
