# Receipt Allocation Status Sync Implementation Plan

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Tài liệu này là kế hoạch/thực thi theo thời điểm viết, có thể chứa giả định cũ.
> Nguồn vận hành hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `task.md`.


> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ensure receipt/advance allocation state is consistent across Receipts, Customers, and Reports after allocations are applied.

**Architecture:** Diagnose whether receipt allocations exist but receipt headers are stale; then compute allocation status/unallocated amount from receipt_allocations for approved receipts and update key read paths (Receipts list, Customer receipts, Dashboard/Reports charts) to use the computed values.

**Tech Stack:** .NET 8, EF Core, Dapper SQL (reports), React (frontend), xUnit integration tests

---

### Task 1: Root cause confirmation (data-level)

**Files:**
- Modify: `docs/plans/2026-02-03-receipt-allocation-sync.md` (append findings once checked)

**Step 1: Query receipt header vs allocations (production or staging DB)**

Run (DB console):
```sql
-- Replace with receipt ids from the customer
SELECT r.id, r.receipt_no, r.status, r.allocation_status, r.unallocated_amount, r.amount
FROM congno.receipts r
WHERE r.customer_tax_code = '0315666756'
ORDER BY r.receipt_date DESC;

SELECT ra.receipt_id, COALESCE(SUM(ra.amount),0) AS allocated
FROM congno.receipt_allocations ra
JOIN congno.receipts r ON r.id = ra.receipt_id
WHERE r.customer_tax_code = '0315666756'
GROUP BY ra.receipt_id;

SELECT a.id, a.advance_no, a.status, a.amount, a.outstanding_amount
FROM congno.advances a
WHERE a.customer_tax_code = '0315666756'
ORDER BY a.advance_date DESC;
```
Expected:
- If allocations exist but receipt headers still show PARTIAL/unallocated: header is stale.
- If no allocations exist: auto-application of credits is missing (feature gap).

**Step 2: Decide fix path**
- If header stale: proceed with Tasks 2–4 (sync on read + charts).
- If allocations missing: add Task 2b (auto-apply unallocated receipts to new advances) before Task 3.

---

### Task 2: Regression test (header stale case)

**Files:**
- Create: `src/backend/Tests.Integration/ReceiptAllocationSyncTests.cs`

**Step 1: Write failing test**
```csharp
[Fact]
public async Task ListAsync_RecomputesAllocationStatus_WhenAllocationsExist()
{
    await using var db = _fixture.CreateContext();
    await ResetAsync(db);
    var (seller, customer) = await SeedMasterAsync(db);

    var receipt = new Receipt { /* APPROVED, amount=1000, unallocated_amount=500, allocation_status='PARTIAL' */ };
    db.Receipts.Add(receipt);

    var advance = new Advance { /* APPROVED, amount=500, outstanding_amount=500 */ };
    db.Advances.Add(advance);
    await db.SaveChangesAsync();

    db.ReceiptAllocations.Add(new ReceiptAllocation
    {
        Id = Guid.NewGuid(),
        ReceiptId = receipt.Id,
        AdvanceId = advance.Id,
        TargetType = "ADVANCE",
        Amount = 500,
        CreatedAt = DateTimeOffset.UtcNow
    });
    advance.OutstandingAmount = 0;
    advance.Status = "PAID";
    await db.SaveChangesAsync();

    var service = BuildReceiptService(db);
    var result = await service.ListAsync(new ReceiptListRequest(seller.SellerTaxCode, customer.TaxCode, null, null, null, null, null, null, null, null, null, null, 1, 20), CancellationToken.None);

    var row = result.Items.Single(i => i.Id == receipt.Id);
    Assert.Equal("ALLOCATED", row.AllocationStatus);
    Assert.Equal(0, row.UnallocatedAmount);
}
```

**Step 2: Run test to verify it fails**
Run: `dotnet test Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter ReceiptAllocationSyncTests`
Expected: FAIL (allocation_status still PARTIAL / unallocated_amount 500)

---

### Task 3: Implement sync on read (approved receipts)

**Files:**
- Modify: `src/backend/Infrastructure/Services/ReceiptService.cs`
- Modify: `src/backend/Infrastructure/Services/CustomerService.cs`
- Modify: `src/backend/Infrastructure/Services/DashboardService.Sql.cs`
- Modify: `src/backend/Infrastructure/Services/ReportService.Charts.cs`

**Step 1: ReceiptService.ListAsync**
- After `rows` load, compute allocation totals for those receipt ids:
```csharp
var receiptIds = rows.Select(r => r.Id).ToList();
var allocatedLookup = receiptIds.Count == 0
  ? new Dictionary<Guid, decimal>()
  : await _db.ReceiptAllocations
      .AsNoTracking()
      .Where(a => receiptIds.Contains(a.ReceiptId))
      .GroupBy(a => a.ReceiptId)
      .Select(g => new { ReceiptId = g.Key, Allocated = g.Sum(x => x.Amount) })
      .ToDictionaryAsync(x => x.ReceiptId, x => x.Allocated, ct);
```
- When mapping list items, if `r.Status == "APPROVED" && r.AllocationStatus != "VOID"`, compute:
```csharp
var allocated = allocatedLookup.TryGetValue(r.Id, out var value) ? value : 0m;
var computedUnallocated = Math.Max(0, r.Amount - allocated);
var computedStatus = computedUnallocated == 0
    ? "ALLOCATED"
    : allocated > 0 ? "PARTIAL" : "UNALLOCATED";
```
- Use computed values in `ReceiptListItem` for approved receipts; otherwise use stored values.

**Step 2: CustomerService.ListReceiptsAsync**
- After `items` load, compute allocation totals for receipt ids on the page (same pattern).
- Override `UnallocatedAmount` for approved receipts when mapping `CustomerReceiptDto`.

**Step 3: Dashboard allocation status + unallocated KPI**
- In `DashboardService.Sql.cs`, update `DashboardAllocationStatusSql` to compute status from receipt_allocations sum instead of `r.allocation_status`.
- Update unallocated KPI queries to derive unallocated from allocations:
```sql
WITH receipt_alloc AS (...)
SELECT SUM(GREATEST(0, r.amount - COALESCE(a.allocated,0))) AS unallocatedReceiptsAmount
```

**Step 4: Report charts allocation status**
- In `ReportService.Charts.cs`, update `ReportAllocationStatusSql` to compute status using receipt_allocations aggregates (same as dashboard).

---

### Task 4: Verify + regression coverage

**Step 1: Run integration test**
Run: `dotnet test Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter ReceiptAllocationSyncTests`
Expected: PASS

**Step 2: Run impacted suites**
Run: `dotnet test Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter ReceiptListTests|DashboardOverviewTests|ReportAgingTests`
Expected: PASS

---

### Task 2b (if allocations missing): Auto-apply credit when new advances appear (optional)

**Files:**
- Modify: `src/backend/Infrastructure/Services/AdvanceService.cs`
- Modify: `src/backend/Infrastructure/Services/ReceiptService.cs` (add helper method)
- Test: `src/backend/Tests.Integration/AdvanceAutoAllocateTests.cs`

**Step 1: Add helper in ReceiptService**
- `AllocateApprovedCreditsAsync(sellerTaxCode, customerTaxCode, ct)`:
  - Find approved receipts with `unallocated_amount > 0`.
  - Load open items (invoices/advances) and apply allocations from oldest receipts FIFO.
  - Update `receipt.unallocated_amount` + `allocation_status`.

**Step 2: Call helper after Advance approve**
- After `advance.Status = APPROVED`, invoke allocation helper to apply credits.

**Step 3: Tests**
- Integration test to ensure an overpaid receipt is allocated to a newly approved advance and status updates to ALLOCATED.

---

## Notes
- Keep module sizes < 800 LOC.
- Add tests for any new behavior.
- Update `progress.md` and `task.md` when done.

---

Plan complete and saved to `docs/plans/2026-02-03-receipt-allocation-sync.md`.
Two execution options:
1) Subagent-Driven (this session)
2) Parallel Session (executing-plans)

