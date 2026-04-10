# Agent Notebook

Muc dich: day la so tay cong viec ben vung trong repo de resume dung trang thai khi phien chat bi tat, context bi nen, hoac doi tai khoan.

## Resume Protocol

Khi quay lai task dang do, doc theo thu tu sau:

1. File nay de lay muc tieu, quyet dinh nghiep vu da chot, va buoc tiep theo.
2. `task.md` de xem checklist va phase dang mo.
3. Bead dang active de xem trang thai chinh thuc va lich su cap nhat.
4. Neu task co sua code, doi chieu them `mcp__gitnexus__detect_changes` truoc khi ket thuc.

## Active Work

- Bead: `cng-vwl`
- Task phase: `Phase 130` trong `task.md`
- Last updated: `2026-04-09`
- Current status: Da hoan tat phase 130; backend invoice list da fallback dung khi thieu customer master, UX nhap lieu/sidebar da doi ten theo flow moi, tests/build da pass, dang cho lenh tiep theo (chua commit/push)

## Goal

Thuc thi goi fix invoice list + doi ten UX nhap lieu:

- sua `InvoiceService.ListAsync()` de invoice van hien thi du customer master bi thieu
- sua `InvoicesPage` de khong con invalid DOM nesting o linked invoice renderer
- doi ten va sap xep lai menu nhap lieu thanh `Import tu Template`, `Nhap hoa don`, `Nhap tra ho`, `Nhap phieu thu`
- lam ro `/imports` la khu vuc import template va normalize deep-link `type` khong hop le
- cap nhat regression tests backend/frontend va verify lai scope bang GitNexus

## Locked Business Decisions

- Route giu nguyen: `/imports`, `/invoices`, `/advances`, `/receipts`.
- Rename chi ap dung cho ten man hinh va hanh dong nhap lieu, khong doi bua toan bo thuat ngu domain.
- `Import tu Template` la ten hien thi chinh thuc cua `/imports`, khong doi route.
- Invoice list backend phai lay invoice lam nguon du lieu chinh; customer master chi la du lieu bo sung.
- Neu customer master thieu, UI/backend phai fallback ve du lieu san co tren invoice/projection thay vi an dong.

## Known Code Areas

- Backend invoice list:
  - `src/backend/Infrastructure/Services/InvoiceService.cs`
  - `src/backend/Tests.Unit/InvoiceServiceListTests.cs`
- Frontend shell/navigation:
  - `src/frontend/src/layouts/AppShell.tsx`
- Frontend invoices/imports pages:
  - `src/frontend/src/pages/InvoicesPage.tsx`
  - `src/frontend/src/pages/imports/ImportsPage.tsx`
- Frontend tests can tac dong:
  - `src/frontend/src/layouts/__tests__/app-shell.test.tsx`
  - `src/frontend/src/pages/__tests__/invoices-page.test.tsx`
  - `src/frontend/src/pages/imports/__tests__/imports-page.fixed-type.test.tsx`

## Work Log

### 2026-04-09

- User yeu cau implement ke hoach fix invoice list + chinh lai UX nhap lieu.
- Da refresh GitNexus index bang `npx -y gitnexus@latest analyze` tai root repo.
- Da chay GitNexus context/impact cho `InvoiceService.ListAsync` (qua symbol context + enclosing class `InvoiceService`) va `AppShell`; ket qua hien tai khong co warning `HIGH/CRITICAL` blocker cho scope sua.
- Da tao bead moi `cng-vwl`, chuyen sang `IN_PROGRESS`, va dong bo lai `task.md` + so tay nay de resume dung phase hien tai.
- Da sua backend `InvoiceService.ListAsync()` theo huong invoice-first + customer lookup dictionary de khong roi ban ghi khi customer master thieu; fallback `CustomerName` ve tax code neu can.
- Da bo sung regression test backend khoa hanh vi invoice van hien du customer master bi thieu.
- Da sua `InvoicesPage` de bo invalid DOM nesting o linked invoices va doi ten page/header thanh `Nhap hoa don`.
- Da cap nhat `AppShell`, `RoleCockpitSection`, `ImportsPage` theo flow moi: `Import tu Template` -> `Nhap hoa don` -> `Nhap tra ho` -> `Nhap phieu thu`.
- Da normalize deep-link `/imports?tab=batch&type=...` khi `type` khong hop le.
- Da chay verify:
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter InvoiceServiceListTests` => pass
  - `npm test -- --run src/layouts/__tests__/app-shell.test.tsx src/pages/__tests__/invoices-page.test.tsx src/pages/imports/__tests__/imports-page.fixed-type.test.tsx` => pass
  - `npm run build` (frontend) => pass
- Da chay `gitnexus_detect_changes(scope: "all")`; output tong the bao `high` do worktree co san file tai lieu modified, nhung scope code thuc te tap trung dung vao `InvoiceService`, `AppShell`, `InvoicesPage`, `ImportsPage`.

## Next Action

- Neu user yeu cau tiep: tach phan copy/CTA nhap lieu con lai o cac man hinh khac hoac tiep tuc commit/push/PR. Neu resume phase 130, bat dau bang `bd show cng-vwl`, doi chieu `task.md`, va kiem tra worktree truoc khi co thao tac git.

## Resume Checklist

- Chay `bd show cng-vwl`
- Mo `task.md` va tim `Phase 130`
- Mo file nay va tiep tuc tu muc `Next Action`
- Truoc khi sua symbol hien co: chay `mcp__gitnexus__impact` cho symbol do
- Truoc khi ket thuc task co sua code: chay `mcp__gitnexus__detect_changes`
