# Agent Notebook

Muc dich: day la so tay cong viec ben vung trong repo de resume dung trang thai khi phien chat bi tat, context bi nen, hoac doi tai khoan.

## Resume Protocol

Khi quay lai task dang do, doc theo thu tu sau:

1. File nay de lay muc tieu, quyet dinh nghiep vu da chot, va buoc tiep theo.
2. `task.md` de xem checklist va phase dang mo.
3. Bead dang active de xem trang thai chinh thuc va lich su cap nhat.
4. Neu task co sua code, doi chieu them `mcp__gitnexus__detect_changes` truoc khi ket thuc.

## Active Work

- Bead: `cng-0pv`
- Task phase: `Phase 129` trong `task.md`
- Last updated: `2026-04-09`
- Current status: Da hoan tat tach root module `/invoices`, verify backend/frontend xong, con buoc dong bead va ghi nhan ket qua cuoi cung

## Goal

Thuc thi refactor frontend cho nghiep vu hoa don:

- tao route goc `/invoices` song song voi `/advances` va `/receipts`
- tren `/invoices` hien thi list/search hoa don va form nhap tay gon trong cung workspace
- giu batch import o `/imports`, co CTA tu `/invoices` deep-link sang `/imports?tab=batch&type=INVOICE`
- bo tab nhap tay khoi `/imports` de trang nay tap trung vao import/history
- khoa lai contract dieu huong va UI bang regression tests

## Locked Business Decisions

- `/invoices` la entry point goc cho hoa don, song song vai tro voi `/advances` va `/receipts`.
- Batch import van nam o `/imports`; manual invoice entry khong nam o day nua.
- CTA import tu `/invoices` phai deep-link truc tiep den `/imports?tab=batch&type=INVOICE`.
- UI can gon, nhan ngan, tranh copy dai; thong tin phu dua vao tooltip neu that su can.
- Uu tien tai su dung list/search hoa don hien co thay vi dung them workspace trung lap.

## Known Code Areas

- Frontend routing/navigation:
  - `src/frontend/src/App.tsx`
  - `src/frontend/src/pages/pageLoaders.ts`
  - `src/frontend/src/layouts/AppShell.tsx`
- Invoices/imports workspace:
  - `src/frontend/src/pages/AdvancesPage.tsx`
  - `src/frontend/src/pages/imports/ImportsPage.tsx`
  - `src/frontend/src/pages/imports/ManualInvoicesSection.tsx`
- Frontend tests can tac dong:
  - `src/frontend/src/layouts/__tests__/app-shell.test.tsx`
  - `src/frontend/src/pages/__tests__/page-loaders.test.ts`
  - `src/frontend/src/pages/imports/__tests__/imports-page.fixed-type.test.tsx`
  - `src/frontend/src/pages/imports/__tests__/manualInvoicesSection.test.tsx`

## Work Log

### 2026-04-09

- User yeu cau "thuc thi ke hoach" cho refactor invoices.
- Da mo bead `cng-0pv` va doi chieu bead/task tracker; xac nhan bead dang `IN_PROGRESS`.
- Da doc lai pattern `/advances` de dung page root mong, deep-link import qua `/imports`.
- Da doc `ImportsPage`, `ManualInvoicesSection`, `App.tsx`, `pageLoaders.ts`, `AppShell.tsx` va test lien quan de chuan bi tach `/invoices`.
- Da chay GitNexus impact cho cac symbol du kien sua:
  - `ImportsPage`, `AppShell`, `ManualInvoicesSection`, `MapInvoiceEndpoints`, `InvoiceService`, `IInvoiceService`, `CustomerTransactionsSection` => khong co warning HIGH/CRITICAL blocker cho scope frontend hien tai
- Da them backend list API `/invoices` va contract frontend de page goc co the tai danh sach hoa don theo filter/search.
- Da tao `src/frontend/src/pages/InvoicesPage.tsx`, them route `/invoices`, doi nav/AppShell/pageLoaders sang flow invoices-first.
- Da rut `src/frontend/src/pages/imports/ImportsPage.tsx` ve batch-only, bo tab manual, va them CTA mo import batch tu `ManualInvoicesSection`.
- Da cap nhat regression tests cho page loader, imports fixed-type, va them test moi cho invoices page.
- Verify da pass:
  - `dotnet build src/backend/Api/CongNoGolden.Api.csproj`
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter InvoiceServiceListTests`
  - `npm test -- --run src/pages/__tests__/invoices-page.test.tsx src/pages/imports/__tests__/imports-page.fixed-type.test.tsx src/pages/__tests__/page-loaders.test.ts` (cwd `src/frontend`)
  - `npm run build` (cwd `src/frontend`)
- Da chay `mcp__gitnexus__detect_changes(scope: "all", base_ref: "main")`; output tong the la `high` vi worktree co sua doi tai `AGENTS.md`, `CLAUDE.md`, `docs/agent-notebook.md`, nhung phan code cua bead van nam trong cum invoices/imports/backend list API.

## Next Action

- Dong bead `cng-0pv`, giu nguyen worktree cho user review hoac commit khi duoc yeu cau.

## Resume Checklist

- Chay `bd show cng-0pv`
- Mo `task.md` va tim `Phase 129`
- Mo file nay va tiep tuc tu muc `Next Action`
- Truoc khi sua symbol hien co: chay `mcp__gitnexus__impact` cho symbol do
- Truoc khi ket thuc task co sua code: chay `mcp__gitnexus__detect_changes`
