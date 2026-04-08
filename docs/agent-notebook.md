# Agent Notebook

Muc dich: day la so tay cong viec ben vung trong repo de resume dung trang thai khi phien chat bi tat, context bi nen, hoac doi tai khoan.

## Resume Protocol

Khi quay lai task dang do, doc theo thu tu sau:

1. File nay de lay muc tieu, quyet dinh nghiep vu da chot, va buoc tiep theo.
2. `task.md` de xem checklist va phase dang mo.
3. Bead dang active de xem trang thai chinh thuc va lich su cap nhat.
4. Neu task co sua code, doi chieu them `mcp__gitnexus__detect_changes` truoc khi ket thuc.

## Active Work

- Bead: `cng-952`
- Task phase: `Phase 127` trong `task.md`
- Last updated: `2026-04-08`
- Current status: Phase 127 da hoan tat; bead `cng-952` da dong sau khi correction flow, history va verification duoc chot

## Goal

Cho phep ke toan chinh sua du lieu da nhap cho:

- khoan tra ho
- phieu thu

Yeu cau:

- co audit reason bat buoc
- xem duoc lich su sua ngay tren tung chung tu
- thay doi anh huong phan bo cua phieu thu phai quay ve `DRAFT`
- co co che chong quen viec dang lam ngay trong repo

## Locked Business Decisions

- Khoan tra ho v1 cho sua: `advanceNo`, `advanceDate`, `amount`, `description`.
- Khoan tra ho khong duoc giam `amount` thap hon tong da phan bo.
- Khoan tra ho v1 khong mo cho doi doi tuong cong no/tax code lien ket.
- Phieu thu neu chi sua metadata (`receiptNo`, `receiptDate`, `method`, `description`) thi cap nhat tai cho.
- Phieu thu neu sua truong anh huong phan bo (`amount`, `allocationMode`, `allocationPriority`, `appliedPeriodStart`, `selectedTargets`) thi dua ve `DRAFT` de duyet lai.
- V1 khong them status moi; tai su dung `DRAFT`.
- Moi correction bat buoc co `reason`.
- Lich su sua phai xem duoc ngay tren chung tu, khong bat nguoi dung vao trang audit admin chung.

## Known Code Areas

- Advances endpoint/service:
  - `src/backend/Api/Endpoints/AdvanceEndpoints.cs`
  - `src/backend/Application/Advances/AdvanceUpdateRequest.cs`
  - `src/backend/Infrastructure/Services/AdvanceService.cs`
- Receipts endpoint/service:
  - `src/backend/Api/Endpoints/ReceiptEndpoints.cs`
  - `src/backend/Application/Receipts/ReceiptDraftUpdateRequest.cs`
  - `src/backend/Infrastructure/Services/ReceiptService.Draft.cs`
- Audit/history:
  - `src/backend/Api/Admin/AdminAuditEndpoints.cs`
  - `src/frontend/src/pages/AdminAuditPage.tsx`
- Frontend:
  - `src/frontend/src/pages/imports/ManualAdvancesSection.tsx`
  - `src/frontend/src/pages/receipts/ReceiptListSection.tsx`

## Work Log

### 2026-04-08

- User chap thuan implement plan correction flow va bo sung co che chong quen task.
- Da tao bead `cng-952` va chuyen sang `in_progress`.
- Da them `Phase 127` vao `task.md`.
- Da xac nhan GitNexus index `CONG_NO_KT` con moi trong ngay.
- Da mo rong backend correction/history cho advances va receipts, bao gom audit reason bat buoc va rule reopen receipt ve `DRAFT` khi sua truong anh huong phan bo.
- Da cap nhat frontend de ke toan co the `Sua` va `Lich su sua` ngay tren danh sach khoan tra ho va phieu thu.
- Da bo sung regression tests backend/frontend cho correction flow va history.
- Da fix lint React cho 2 correction modal bang cach doi sang keyed form state, tranh `set-state-in-effect`.
- Verification da pass:
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj -v minimal` => pass (`206/206`)
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~AdvanceCorrectionTests|FullyQualifiedName~ReceiptCorrectionTests" -v minimal` => pass (`6/6`)
  - `npm --prefix src/frontend run lint` => pass
  - `npm --prefix src/frontend test -- --run src/pages/receipts/__tests__/receipt-list-section.test.tsx src/pages/receipts/__tests__/receipts-modules.test.tsx src/pages/imports/__tests__/manualAdvancesColumns.test.tsx src/pages/imports/__tests__/manualAdvancesSection.test.tsx` => pass
  - `npm --prefix src/frontend run build` => pass
- Da chay `mcp__gitnexus__detect_changes(repo=\"CONG_NO_KT\", scope=\"unstaged\")`; ket qua risk `critical` do workspace dang co nhieu thay doi unstaged rong hon rieng Phase 127, khong phai do 1 fix nho cuoi cung.
- Da dong bead `cng-952` sau khi dong bo `task.md` va notebook.

## Next Action

Neu user mo rong them correction scope hoac audit workflow, tao bead/phase moi thay vi tiep tuc chong len Phase 127 da dong.

## Resume Checklist

- Chay `bd show cng-952`
- Mo `task.md` va tim `Phase 127`
- Mo file nay va tiep tuc tu muc `Next Action`
- Truoc khi sua symbol hien co: chay `mcp__gitnexus__impact` cho symbol do
- Truoc khi ket thuc task co sua code: chay `mcp__gitnexus__detect_changes`
