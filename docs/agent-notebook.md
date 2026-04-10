# Agent Notebook

Muc dich: day la so tay cong viec ben vung trong repo de resume dung trang thai khi phien chat bi tat, context bi nen, hoac doi tai khoan.

## Resume Protocol

Khi quay lai task dang do, doc theo thu tu sau:

1. File nay de lay muc tieu, quyet dinh nghiep vu da chot, va buoc tiep theo.
2. `task.md` de xem checklist va phase dang mo.
3. Bead dang active de xem trang thai chinh thuc va lich su cap nhat.
4. Neu task co sua code, doi chieu them `mcp__gitnexus__detect_changes` truoc khi ket thuc.

## Active Work

- Bead: `cng-ljd`
- Task phase: `Phase 132` trong `task.md`
- Last updated: `2026-04-10`
- Current status: Dang chot follow-up cuoi cho `/imports`: bo row hero lap lai trong `ImportBatchSection`, dua 2 nut tien ich vao header cua `Buoc 1`, cap nhat test/notebook/task, verify scope bang GitNexus roi commit cung batch follow-up chua commit

## Goal

Chot follow-up toi uu header `/imports` sau phase 131:

- bo row hero lap lai con sot lai trong `ImportBatchSection`
- dua `Tai template` va `Lich su nhap` vao header cua card `Buoc 1`
- giu shell header `/imports` la noi duy nhat hien copy quy trinh
- cap nhat regression tests frontend cho layout moi
- doi chieu lai scope thay doi bang GitNexus, dong bead va commit batch follow-up

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
  - `src/frontend/src/pages/imports/ImportBatchSection.tsx`
- Frontend tests can tac dong:
  - `src/frontend/src/layouts/__tests__/app-shell.test.tsx`
  - `src/frontend/src/pages/__tests__/invoices-page.test.tsx`
  - `src/frontend/src/pages/imports/__tests__/imports-page.fixed-type.test.tsx`
  - `src/frontend/src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx`

## Work Log

### 2026-04-10

- User yeu cau bo hang `Nhap file, kiem tra truoc khi ghi du lieu` con sot lai tren giao dien `/imports`, dua 2 nut `Tai template` va `Lich su nhap` vao goc tren ben phai khu vuc `Buoc 1`, sau do commit toan bo thay doi follow-up hien co.
- Da refresh GitNexus index bang `npx -y gitnexus@latest analyze` de chac chan graph khop workspace moi nhat.
- Da chay GitNexus `impact` cho `ImportBatchSection`; risk `LOW`, khong co caller/process blocker, nen co the sua truc tiep component nay.
- Da tao bead moi `cng-ljd`, chuyen sang `IN_PROGRESS`, va them `Phase 132` vao `task.md`.
- Da sua `ImportBatchSection` de bo row hero lap lai va dua 2 nut tien ich vao header card `Buoc 1`; dong thoi bo sung regression test cho layout moi.
- Da verify:
  - `npm test -- --run src/layouts/__tests__/app-shell.test.tsx src/pages/imports/__tests__/imports-page.fixed-type.test.tsx src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx` => pass (`27/27`)
- Da chay `gitnexus_detect_changes(scope: "all")`; output tong the len `high` do worktree van co them file tai lieu/shell dang modified (`AGENTS.md`, `CLAUDE.md`, notebook, task), nhung scope code cua bead tap trung vao `AppShell`, `ImportsPage`, `ImportBatchSection` va regression tests lien quan.

- User yeu cau commit batch thay doi truoc do, sau do rut gon header trang `/imports` vi copy dang bi lap.
- Da commit thanh cong batch truoc voi commit `6af5cbf` (`fix(imports): Preserve invoice rows and streamline input UX`).
- Da chay GitNexus `impact` cho `ImportsPage` va `AppShell`; ca hai deu o muc `LOW`, khong co caller/process blocker canh bao `HIGH/CRITICAL`.
- Da tao bead moi `cng-joy`, chuyen sang `IN_PROGRESS`, va bo sung `Phase 131` vao `task.md`.
- Da sua `src/frontend/src/pages/imports/ImportsPage.tsx` de bo hoan toan block header lap lai.
- Da sua `src/frontend/src/layouts/AppShell.tsx` de route `/imports` hien copy shell header: `Quy trinh: chuan bi template -> tai file -> xem truoc -> ghi du lieu.` thay cho dong mo ta vai tro mac dinh.
- Da cap nhat frontend regression tests cho `AppShell` va `ImportsPage` de phan anh copy moi va viec xoa header cu.
- Da verify:
  - `npm test -- --run src/layouts/__tests__/app-shell.test.tsx src/pages/imports/__tests__/imports-page.fixed-type.test.tsx` => pass (`18/18`)
- Da chay `gitnexus_detect_changes(scope: "all")`; output tong the len `medium` do worktree con san `AGENTS.md` va `CLAUDE.md`, nhung scope code cua bead tap trung dung vao `AppShell`, `ImportsPage`, tests lien quan va `task.md`.

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

- Dong bead `cng-ljd`, stage rieng batch follow-up `/imports` (bo qua `AGENTS.md` va `CLAUDE.md`), roi commit theo yeu cau nguoi dung.

## Resume Checklist

- Chay `bd show cng-ljd`
- Mo `task.md` va tim `Phase 132`
- Mo file nay va tiep tuc tu muc `Next Action`
- Truoc khi sua symbol hien co: chay `mcp__gitnexus__impact` cho symbol do
- Truoc khi ket thuc task co sua code: chay `mcp__gitnexus__detect_changes`
