# So sanh quyen giua cac role tai khoan

Tai lieu nay tong hop quyen theo source code hien tai cua repo, tach rieng 3 lop:

- Policy backend: role nao duoc goi endpoint nao.
- Rule nghiep vu trong service: co gioi han them theo owner khach hang hay khong.
- Frontend UI: nut/chuc nang co dang duoc mo ra tren giao dien hay chi ton tai o backend.

## Role hien co

| Role | Nhan tren UI | Nguon |
| --- | --- | --- |
| `Admin` | Quan tri | `src/frontend/src/utils/roles.ts:2` |
| `Supervisor` | Giam sat | `src/frontend/src/utils/roles.ts:3` |
| `Accountant` | Ke toan | `src/frontend/src/utils/roles.ts:4` |
| `Viewer` | Chi xem | `src/frontend/src/utils/roles.ts:5` |

## Ket luan nhanh cho Accountant

- Co the upload file Excel/import du lieu, xem preview, xem lich su import va huy batch chua commit. Khong the commit hoac rollback batch import (`src/backend/Api/Program.cs:241-257`, `src/backend/Api/Endpoints/ImportEndpoints.cs:89`, `src/backend/Api/Endpoints/ImportEndpoints.cs:114`, `src/backend/Api/Endpoints/ImportEndpoints.cs:137`, `src/backend/Api/Endpoints/ImportEndpoints.cs:157`, `src/backend/Api/Endpoints/ImportEndpoints.cs:178`, `src/backend/Api/Endpoints/ImportEndpoints.cs:199`).
- Co the tao khoan tra ho. Trong `AdvanceService.CreateAsync` hien khong co check owner khach hang, chi can seller/customer ton tai (`src/backend/Infrastructure/Services/AdvanceService.cs:24-76`).
- Co the duyet, void, unvoid, cap nhat khoan tra ho neu khach hang do thuoc owner cua chinh ke toan dang dang nhap (`src/backend/Infrastructure/Services/AdvanceService.cs:117`, `src/backend/Infrastructure/Services/AdvanceService.cs:210`, `src/backend/Infrastructure/Services/AdvanceService.cs:300`, `src/backend/Infrastructure/Services/AdvanceService.cs:380`, `src/backend/Infrastructure/Services/AdvanceService.cs:564-586`).
- Co the tao/duyet/phan bo phieu thu cho khach hang minh phu trach; `ReceiptService` co check owner qua `EnsureCanManageCustomer` (`src/backend/Infrastructure/Services/ReceiptService.cs:410-546`, `src/backend/Infrastructure/Services/ReceiptService.OpenItems.cs:23`, `src/backend/Infrastructure/Services/ReceiptService.OpenItems.cs:76-99`).
- Tren frontend hien tai, luong "tao va duyet ngay" o khu manual advance dang chi mo cho `Admin`/`Supervisor`; `Accountant` tren UI duoc mo ta la "Chi tao nhap" du backend van cho phep manage advance thuoc owner cua minh (`src/frontend/src/pages/AdvancesPage.tsx:11`, `src/frontend/src/pages/AdvancesPage.tsx:29`, `src/frontend/src/pages/imports/ManualAdvancesSection.tsx:337`, `src/frontend/src/pages/imports/ManualAdvancesSection.tsx:508-510`, `src/frontend/src/pages/imports/ManualAdvancesSection.tsx:677-678`, `src/frontend/src/pages/imports/ManualAdvancesSection.tsx:793-797`).

## Ma tran quyen chinh

Ky hieu:

- `Yes`: duoc theo policy/backend.
- `Owner-only`: duoc, nhung bi gioi han theo `Customer.AccountantOwnerId`.
- `UI-limited`: backend co the cho phep, nhung UI hien tai dang gioi han.

| Chuc nang | Admin | Supervisor | Accountant | Viewer | Co so source code |
| --- | --- | --- | --- | --- | --- |
| Xem dashboard, bao cao, rui ro, danh sach khach hang | Yes | Yes | Yes | Yes | `src/backend/Api/Program.cs:247-249`, `src/backend/Api/Program.cs:254` |
| Upload file import / xem preview | Yes | Yes | Yes | No | `src/backend/Api/Program.cs:241`, `src/backend/Api/Endpoints/ImportEndpoints.cs:89`, `src/backend/Api/Endpoints/ImportEndpoints.cs:114` |
| Xem lich su import | Yes | Yes | Yes | No | `src/backend/Api/Program.cs:243`, `src/backend/Api/Endpoints/ImportEndpoints.cs:137` |
| Huy batch import chua commit | Yes | Yes | Yes | No | `src/backend/Api/Program.cs:243`, `src/backend/Api/Endpoints/ImportEndpoints.cs:199` |
| Commit / rollback batch import | Yes | Yes | No | No | `src/backend/Api/Program.cs:242`, `src/backend/Api/Endpoints/ImportEndpoints.cs:157`, `src/backend/Api/Endpoints/ImportEndpoints.cs:178` |
| Tao khoan tra ho | Yes | Yes | Yes | No | `src/backend/Api/Program.cs:244`, `src/backend/Api/Endpoints/AdvanceEndpoints.cs:74`, `src/backend/Infrastructure/Services/AdvanceService.cs:24-76` |
| Xem danh sach khoan tra ho | Yes | Yes | Owner-only | No | `src/backend/Api/Program.cs:244`, `src/backend/Infrastructure/Services/AdvanceService.cs:476-483`, `src/backend/Infrastructure/Services/AdvanceService.cs:540` |
| Duyet / void / unvoid / cap nhat khoan tra ho | Yes | Yes | Owner-only | No | `src/backend/Api/Program.cs:244`, `src/backend/Api/Endpoints/AdvanceEndpoints.cs:76-154`, `src/backend/Infrastructure/Services/AdvanceService.cs:564-586` |
| Duyet nhanh manual advance tren UI | Yes | Yes | UI-limited | No | `src/frontend/src/pages/AdvancesPage.tsx:11`, `src/frontend/src/pages/imports/ManualAdvancesSection.tsx:508-510`, `src/frontend/src/pages/imports/ManualAdvancesSection.tsx:677-678` |
| Tao / xem / duyet / phan bo phieu thu | Yes | Yes | Owner-only | No | `src/backend/Api/Program.cs:245`, `src/backend/Api/Endpoints/ReceiptEndpoints.cs:62`, `src/backend/Api/Endpoints/ReceiptEndpoints.cs:225`, `src/backend/Infrastructure/Services/ReceiptService.OpenItems.cs:76-99`, `src/backend/Infrastructure/Services/ReceiptService.cs:410-546` |
| Quan ly khach hang (thay doi du lieu) | Yes | Yes | No | No | `src/backend/Api/Program.cs:249` |
| Quan ly hoa don | Yes | Yes | No | No | `src/backend/Api/Program.cs:250` |
| Khoa ky, audit, health, ERP, backup manage | Yes | Yes | No | No | `src/backend/Api/Program.cs:246`, `src/backend/Api/Program.cs:252-257` |
| Quan ly user | Yes | No | No | No | `src/backend/Api/Program.cs:251` |
| Restore backup | Yes | No | No | No | `src/backend/Api/Program.cs:257` |

## Luu y quan trong

- `Accountant` import du lieu duoc, nhung khong phai nguoi chot batch import. Frontend cung phan tach ro `canStage` va `canCommit` cho trang import (`src/frontend/src/pages/imports/ImportsPage.tsx:35-36`, `src/frontend/src/pages/imports/ImportsPage.tsx:102`).
- Quy tac owner cua `AdvanceService` va `ReceiptService` khong doi xung:
  - `AdvanceService.CreateAsync` khong check owner (`src/backend/Infrastructure/Services/AdvanceService.cs:24-76`).
  - `AdvanceService` list/approve/update/void/unvoid co check owner (`src/backend/Infrastructure/Services/AdvanceService.cs:476-483`, `src/backend/Infrastructure/Services/AdvanceService.cs:564-586`).
  - `ReceiptService` create/list/approve deu co check owner (`src/backend/Infrastructure/Services/ReceiptService.cs:410-546`, `src/backend/Infrastructure/Services/ReceiptService.OpenItems.cs:76-99`).
- Neu muc tieu nghiep vu la "Ke toan chi duoc tao khoan tra ho cho khach hang minh phu trach", thi backend hien tai chua enforce quy tac do o buoc `AdvanceService.CreateAsync`.

## Tra loi truc tiep cho cau hoi

- `Accountant (Ke toan)` co the lam gi?
  - Xem dashboard, bao cao, rui ro, khach hang.
  - Import file, preview, xem lich su import, huy batch chua commit.
  - Tao khoan tra ho.
  - Quan ly khoan tra ho va phieu thu trong pham vi khach hang minh phu trach.
- Co the import file Excel du lieu hay khong?
  - Co, nhung chi toi muc upload/preview/history/cancel; khong duoc commit/rollback batch.
- Co the tao va duyet khoan tra ho hay khong?
  - Tao: Co.
  - Duyet: Co theo backend neu do la khach hang do minh phu trach.
  - Tren frontend hien tai: luong manual advance dang co dau hieu chi mo "duyet ngay" cho `Admin`/`Supervisor`.
