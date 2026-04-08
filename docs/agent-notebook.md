# Agent Notebook

Muc dich: day la so tay cong viec ben vung trong repo de resume dung trang thai khi phien chat bi tat, context bi nen, hoac doi tai khoan.

## Resume Protocol

Khi quay lai task dang do, doc theo thu tu sau:

1. File nay de lay muc tieu, quyet dinh nghiep vu da chot, va buoc tiep theo.
2. `task.md` de xem checklist va phase dang mo.
3. Bead dang active de xem trang thai chinh thuc va lich su cap nhat.
4. Neu task co sua code, doi chieu them `mcp__gitnexus__detect_changes` truoc khi ket thuc.

## Active Work

- Bead: `cng-79p`
- Task phase: `Phase 128` trong `task.md`
- Last updated: `2026-04-09`
- Current status: Phase 128 da hoan tat; bead `cng-79p` da dong sau khi verify va doi chieu `detect_changes`

## Goal

Chot follow-up cho correction flow khoan tra ho sau Phase 127:

- cap nhat lai `current_balance` / `status` / `outstanding_amount` dung khi sua chung tu da `APPROVED`
- tu dong tai phan bo receipt credit con ranh neu correction lam tang outstanding
- reload lai workspace sau khi sua de tranh stale data
- lam gon cum action `Sua`, `Lich su sua`, `Phe duyet`, `Huy`
- khoa lai bang regression tests

## Locked Business Decisions

- Correction khoan tra ho da `APPROVED` phai tinh lai so du khach hang ngay trong transaction, khong de state trung gian sai lech.
- Neu correction lam tang outstanding cua khoan tra ho dang `APPROVED` va khach hang con receipt credit auto-allocatable, he thong duoc phep tu dong ap dung phan credit con ranh do.
- Auto-allocation follow-up chi dung cho receipt thuc su dang `AUTO` + `UNALLOCATED`; test fixture da duoc khoa lai de khong vo tinh bien receipt da dung thanh receipt con ranh.
- Sau khi correction thanh cong, UI phai reload lai danh sach ngay thay vi cho user tu refresh.
- 4 action o danh sach khoan tra ho duoc nhom thanh 2 lane: utility (`Sua`, `Lich su sua`) va commit (`Phe duyet`, `Huy`/`Bo huy`) de nhin nhanh hon.

## Known Code Areas

- Backend correction logic:
  - `src/backend/Infrastructure/Services/AdvanceService.cs`
  - `src/backend/Tests.Integration/AdvanceCorrectionTests.cs`
- Frontend advances workspace:
  - `src/frontend/src/pages/imports/ManualAdvancesSection.tsx`
  - `src/frontend/src/pages/imports/manualAdvancesColumns.tsx`
  - `src/frontend/src/pages/advances/advances.css`
  - `src/frontend/src/pages/imports/__tests__/manualAdvancesColumns.test.tsx`
  - `src/frontend/src/pages/imports/__tests__/manualAdvancesSection.test.tsx`

## Work Log

### 2026-04-09

- User yeu cau tiep tuc thuc hien ke hoach follow-up sau Phase 127.
- Da tiep tuc tren bead `cng-79p` voi muc tieu chot correction flow khoan tra ho.
- Da sua `AdvanceService.UpdateAsync` de:
  - load customer truoc correction
  - tinh `balanceDelta` theo transition status/value
  - wrap update trong transaction
  - cap nhat `Customer.CurrentBalance`
  - tu dong goi `ApplyReceiptCreditsToAdvanceAsync` khi correction giu trang thai non-draft va con outstanding
- Da mo rong integration tests `AdvanceCorrectionTests`:
  - khoa assertion `current_balance` sau correction
  - them regression test cho case correction lam tang outstanding va receipt credit con ranh duoc auto-reallocate
  - sua helper `SeedApprovedReceiptAsync` de mac dinh quay ve fixture receipt da allocate, chi bat auto/unallocated khi goi ro rang
- Da sua frontend `ManualAdvancesSection` de tang `listReload` ngay sau correction thanh cong.
- Da redesign cum action trong `manualAdvancesColumns.tsx` + `advances.css` thanh 2 lane, co accent ro cho nut `Phe duyet`.
- Da cap nhat frontend tests de khoa layout moi va xac nhan list reload sau correction.
- Verification da xanh:
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~AdvanceCorrectionTests" -v minimal` => pass (`4/4`)
  - `npm --prefix src/frontend test -- --run src/pages/imports/__tests__/manualAdvancesColumns.test.tsx src/pages/imports/__tests__/manualAdvancesSection.test.tsx` => pass (`8/8`)
  - `docker compose config -q` => pass
  - `docker compose build api web` => pass
- Da chay `mcp__gitnexus__detect_changes(repo="CONG_NO_KT", scope="unstaged")`; ket qua risk `high` do anh huong cua service dung chung `AdvanceService` va ca file tracker (`task.md`), khong lo blocker moi rieng cho correction follow-up.
- Da dong bead `cng-79p`.

## Next Action

Neu user mo rong them correction scope cho khoan tra ho/phieu thu, tao bead moi thay vi tiep tuc chong len Phase 128 da dong.

## Resume Checklist

- Chay `bd show cng-79p`
- Mo `task.md` va tim `Phase 128`
- Mo file nay va tiep tuc tu muc `Next Action`
- Truoc khi sua symbol hien co: chay `mcp__gitnexus__impact` cho symbol do
- Truoc khi ket thuc task co sua code: chay `mcp__gitnexus__detect_changes`
