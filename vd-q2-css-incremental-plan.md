# VĐ-Q2 - Ke hoach tach `index.css` theo huong incremental

## Goal
Giam kich thuoc va do phuc tap cua `src/frontend/src/index.css` ma khong gay regression giao dien.  
Pham vi tai lieu nay chi la **plan**, chua thuc thi code.

## Nguyen tac
- Khong big-bang refactor.
- Moi dot tach phai build pass + so sanh UI voi baseline.
- Giu nguyen thu tu cascade, uu tien an toan truoc.

## Tasks
- [x] Task 1: Chot baseline truoc khi tach  
  Verify: co screenshot baseline cho `/dashboard`, `/customers`, `/reports`, `/admin` + `npm run build` pass.

- [x] Task 2: Lap mapping selector trong `index.css` theo nhom  
  Nhom muc tieu: `tokens-base`, `primitives`, `feedback`, `data-display`, `forms-filters`, `responsive-overrides`.  
  Verify: co bang mapping selector -> file dich, khong bo sot nhom lon.

- [x] Task 3: Dot 1 (rui ro thap)  
  Tao va tach sang: `tokens.css`, `base.css`, `primitives.css`, `feedback.css`.  
  Verify: `index.css` con import theo thu tu chuan, build pass, UI khong lech.

- [x] Task 4: Dot 2 (rui ro trung binh)  
  Tach sang: `data-display.css`, `forms-filters.css`, `responsive.css`.  
  Verify: build pass + screenshot diff trong nguong chap nhan.

- [x] Task 5: Don duplicate va khoa import order  
  Thu tu import de xuat: `tokens -> base -> primitives -> feedback -> data-display -> forms-filters -> responsive`.  
  Verify: khong con duplicate quan trong, khong co rule bi override sai y dinh.

- [x] Task 6: Regression pass desktop/mobile  
  Viewport: `1920x1080`, `1366x768`, `390x844`.  
  Verify: dark/light mode, table, modal, chart hover, filter grid van dung.

- [x] Task 7: Hoan tat tracking  
  Cap nhat `codex_v5_tasks.md` va `task.md` (neu can), dong bo bead sau khi xong.  
  Verify: trang thai task ro rang, de tiep tuc o session sau.

## Done when
- [x] `index.css` duoc tach theo 2 dot, khong regression UI/chuc nang.
- [x] Build frontend pass sau moi dot va sau cung.
- [x] Tai lieu tracking (task log) duoc cap nhat day du.

## Ghi chu cho session sau
- Da hoan thanh tach module + lock import order:
  - `tokens.css`, `base.css`, `primitives.css`, `app-shell.css`, `feedback.css`, `data-display.css`, `forms-filters.css`, `responsive.css`.
- `primitives.css` da giam xuong `745` lines (truoc do `966`).
- Baseline e2e CSS da chay pass cho routes chinh (`e2e/css-baseline.spec.ts`).
- Regression matrix full viewport + dark/light da chay pass:
  - `npm run test:e2e -- e2e/css-regression-matrix.spec.ts` (voi `E2E_API_TARGET=http://127.0.0.1:18080`) => `2 passed`.
- `npm run test -- --run` hien con 2 test fail unrelated voi dot tach CSS:
  - `src/layouts/__tests__/app-shell.test.tsx` (text assertion)
  - `src/pages/__tests__/dashboard-page.test.tsx` (copy assertion)
