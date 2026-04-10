# Agent Notebook

Muc dich: day la so tay cong viec ben vung trong repo de resume dung trang thai khi phien chat bi tat, context bi nen, hoac doi tai khoan.

## Resume Protocol

Khi quay lai task dang do, doc theo thu tu sau:

1. File nay de lay muc tieu, quyet dinh nghiep vu da chot, va buoc tiep theo.
2. `task.md` de xem checklist va phase dang mo.
3. Bead dang active de xem trang thai chinh thuc va lich su cap nhat.
4. Neu task co sua code, doi chieu them `mcp__gitnexus__detect_changes` truoc khi ket thuc.

## Active Work

- Bead: `pending (bd create dang bi chặn bởi routes.jsonl)`
- Task phase: `Phase 136` trong `task.md`
- Last updated: `2026-04-10`
- Current status: Da hoan tat follow-up subtitle shell cho `/advances`, test pass, detect_changes da doi chieu; dang chot tracking va commit theo yeu cau user.

## Goal

Hoan tat follow-up shell copy cho `/advances`:

- doi subtitle shell route `/advances` sang copy nghiep vu `Nhap khoan tra ho cho khach hang vao he thong theo doi cong no`
- giu subtitle role `Admin` mac dinh cho cac route khac, chi override rieng `/advances`
- cap nhat regression test frontend de khoa subtitle moi
- doi chieu scope bang GitNexus truoc khi commit

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

- User yeu cau tren `/advances`: bo dong copy `Uu tien hoan thanh MST ben ban...`, ra soat lai xem `So chung tu` co bat buoc hay khong, neu co thi them badge `Bat buoc` nhu cac o khac, va phai doi chieu ca backend manual + import template.
- Da doc GitNexus context cua repo va chay `impact` cho symbol `ManualAdvancesSection`; ket qua `LOW`, khong co caller/process blocker nen co the sua truc tiep component.
- Da doi chieu rule bat buoc:
  - frontend manual `ManualAdvancesSection` dang chan submit neu `advanceNo` rong va hien loi `Vui long nhap so chung tu.`
  - backend integration test `AdvanceCreateValidationTests.CreateAsync_Rejects_EmptyAdvanceNo` xac nhan service reject khi `advanceNo` rong
  - template generator `scripts/imports/generate_import_templates.py` da danh dau cot `advance_no` la `True` / bat buoc
- Da thu tao bead moi bang `bd create`, nhung Beads CLI van fail voi `cannot use --rig: no routes.jsonl found in any parent .beads directory`; phase nay tam theo doi bang `task.md` + notebook.
- Da sua `src/frontend/src/pages/imports/ManualAdvancesSection.tsx` de bo copy header dai va them badge `Bat buoc` cho truong `So chung tu`; dong thoi them `required` cho input nay de phan anh dung semantics cua form.
- Da cap nhat regression test `src/frontend/src/pages/imports/__tests__/manualAdvancesSection.test.tsx` de khoa viec copy cu da bi bo va `So chung tu` van duoc danh dau bat buoc tren UI.

- User yeu cau ra soat lai review finding tren `/invoices`, doi label nut `Mo import batch` thanh `Import tu Template`, sau do commit ca batch thay doi invoices truoc do + fix moi.
- Da doc GitNexus context va chay `impact` cho `ManualInvoicesSection`; risk `LOW`, khong co caller/process bi anh huong truc tiep nen co the sua copy an toan.
- Da kiem tra lai review finding cu ve invalid DOM nesting trong `InvoicesPage`; ket qua canh bao da stale vi code hien tai khong con pattern `span > div` o linked invoices/reference chips.
- Da thu tao bead moi bang `bd create`, nhung Beads CLI fail voi `cannot use --rig: no routes.jsonl found in any parent .beads directory`; tam thoi phase follow-up nay duoc theo doi bang `task.md` + notebook cho den khi cau hinh CLI duoc sua.
- Da sua `src/frontend/src/pages/imports/ManualInvoicesSection.tsx` de CTA moi hien `Import tu Template`.
- Da cap nhat regression tests cho `ManualInvoicesSection` va `InvoicesPage` de khoa dung copy/casing moi cua CTA.
- Da verify:
  - `npm --prefix src/frontend test -- --run src/pages/imports/__tests__/manualInvoicesSection.test.tsx src/pages/__tests__/invoices-page.test.tsx` => pass (`9/9`)

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

- User yeu cau tren trang `/advances` doi subtitle shell tu `Theo doi van hanh, phan quyen va rui ro he thong.` thanh `Nhap khoan tra ho cho khach hang vao he thong theo doi cong no`, loai bo code thua neu co va commit ngay sau do.
- Da chay GitNexus `impact` cho `AppShell`; risk `LOW`, khong co caller/process `HIGH/CRITICAL`.
- Huong sua duoc chot: chi them page guidance override rieng cho route `/advances` trong `AppShell`, khong thay subtitle mac dinh cua role `Admin` de tranh anh huong dashboard va cac trang quan tri khac.
- Da sua `src/frontend/src/layouts/AppShell.tsx` de route `/advances` hien subtitle moi.
- Da cap nhat regression test `src/frontend/src/layouts/__tests__/app-shell.test.tsx` de khoa subtitle moi tren route `/advances`.
- Da verify:
  - `npm --prefix src/frontend test -- --run src/layouts/__tests__/app-shell.test.tsx src/pages/__tests__/advances-page.test.tsx` => pass (`17/17`)
- Da chay `gitnexus_detect_changes(scope: "all")`; output tong the len `medium` vi worktree co them file doc/tracking, nhung scope code cua request nay chi tap trung vao `AppShell` va regression test shell.

## Next Action

- Khong co buoc ky thuat dang mo. Neu user tiep tuc voi UI shell/route copy, bat dau lai bang `mcp__gitnexus__impact` cho symbol can sua va doi chieu `task.md` phase moi nhat.

## Resume Checklist

- Chay `bd show cng-ljd`
- Mo `task.md` va tim `Phase 136`
- Mo file nay va tiep tuc tu muc `Next Action`
- Truoc khi sua symbol hien co: chay `mcp__gitnexus__impact` cho symbol do
- Truoc khi ket thuc task co sua code: chay `mcp__gitnexus__detect_changes`

### 2026-04-10

- User yeu cau tren `/invoices`: bo block header lap lai trong page body va doi copy shell header thanh `Theo doi danh sach HD, nhap thu cong HD hoac chuyen sang Import tu Template.`
- Da chay GitNexus `impact` cho `InvoicesPage` va `AppShell`; ca hai deu `LOW`, khong co caller/process canh bao `HIGH/CRITICAL`.
- Da tao bead moi `cng-ktd`, chuyen sang `IN_PROGRESS`, va bo sung `Phase 133` vao `task.md`.
- Huong sua duoc chot: bo header lap ngay trong `InvoicesPage`, them page guidance override rieng cho route `/invoices` trong `AppShell` de khong lam thay doi copy `Admin` toan cuc.
- Da sua `InvoicesPage` de bo hoan toan block header lap lai phia tren `ManualInvoicesSection`.
- Da sua `AppShell` de route `/invoices` hien copy shell header moi: `Theo doi danh sach HD, nhap thu cong HD hoac chuyen sang Import tu Template.`
- Da cap nhat regression tests frontend cho `AppShell` va `InvoicesPage` de khoa copy moi va viec bo header trung lap.
- Da verify:
  - `npm --prefix src/frontend test -- --run src/layouts/__tests__/app-shell.test.tsx src/pages/__tests__/invoices-page.test.tsx` => pass (`16/16`)
- Da chay `gitnexus_detect_changes(scope: "all")`; output tong the len `high` do worktree van co file tai lieu modified san (`AGENTS.md`, `CLAUDE.md`) cung tracking files, nhung scope code cua bead chi tap trung vao `AppShell`, `InvoicesPage` va regression tests lien quan.

- User yeu cau commit phan thay doi `/advances` truoc do, sau do tiep tuc rut gon them chu tren giao dien advances va dong bo nhan `Bat buoc`.
- Da commit thanh cong batch thay doi `/advances` truoc do voi commit `3d444da` (`fix(advances): Simplify create copy and mark voucher as required`).
- Da chay GitNexus `impact` cho `ManualAdvancesSection`; risk `LOW`, khong co caller/process `HIGH/CRITICAL`, nen co the tiep tuc sua component an toan.
- Da bo block copy cuoi hang action trong `Tao khoan tra ho KH`, bo mo ta o `Danh sach xu ly`, va bo heading/helper copy cua khu `Bo loc van hanh` de giao dien gon hon.
- Da sua `src/frontend/src/pages/advances/advances.css` de dong bo nhan required sang cung style mau xanh va doi pseudo-label tu `Bat buoc` thanh `Bắt buộc`; dong thoi `So chung tu` khong con dung badge cam `pill-warn`.
- Da cap nhat regression test `src/frontend/src/pages/imports/__tests__/manualAdvancesSection.test.tsx` de khoa cac doan copy da bo va giu semantics required cho truong `So chung tu`.
- Da verify:
  - `npm --prefix src/frontend test -- --run src/pages/imports/__tests__/manualAdvancesSection.test.tsx src/pages/__tests__/advances-page.test.tsx` => pass (`11/11`)
