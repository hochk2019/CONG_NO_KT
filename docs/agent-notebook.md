# Agent Notebook

Muc dich: day la so tay cong viec ben vung trong repo de resume dung trang thai khi phien chat bi tat, context bi nen, hoac doi agent.

## Resume Protocol

Khi quay lai task dang do, doc theo thu tu sau:

1. File nay de lay muc tieu, quyet dinh da chot, risk dang mo, va buoc tiep theo.
2. `task.md` de xem phase/checklist dang mo.
3. Bead dang active de xem trang thai chinh thuc.
4. Neu task co sua code, doi chieu them `mcp__gitnexus__detect_changes` truoc khi ket thuc.

## Active Work

- Bead: `cng-6zj`
- Task phase: `Phase 143` trong `task.md`
- Last updated: `2026-04-12`
- Current status: baseline backup/offsite changes da duoc commit thanh `6851fa1`; dang noi not env-plumbing Google Drive cho Docker/docs. Secret that (`ClientId` / `ClientSecret`) van chua co tren may hien tai.

## Goal

Nang cap backup local hien tai thanh he thong 2 pha:

1. tao local dump thanh cong;
2. enqueue va xu ly upload offsite rieng len Google Drive.

Muc tieu cu the:

- giu local backup/restore hien tai hoat dong va tuong thich nguoc;
- them Google Drive OAuth user drive voi refresh token ma hoa server-side;
- them upload metadata, retry/backoff, checksum, retention offsite;
- bo sung co che recovery khi service restart thay cho queue in-memory de tranh mat job queued;
- mo rong API/UI admin de hien local/offsite status tach biet;
- them test cho backend/frontend khi doi contract.

## Locked Technical Decisions

- Repo nay bat buoc uu tien GitNexus truoc khi sua symbol hien huu.
- Da refresh index bang `npx -y gitnexus@latest analyze` truoc khi sua.
- Huong trien khai chot:
  - khong upload Drive truc tiep trong buoc `pg_dump`;
  - local backup success khong bi fail nguoc neu offsite upload loi;
  - Drive upload duoc model hoa thanh job/trang thai rieng co the retry va audit;
  - restore van doc lap voi Google Drive, nhung se mo duong de tai file tu Drive ve local khi can.
- Auth chot theo plan:
  - Google Drive dung OAuth user drive;
  - chi luu refresh token da ma hoa;
  - `client_id` / `client_secret` chi nam trong env / secret config, khong di qua UI/DB;
  - action connect/callback/disconnect/test upload/reupload deu phai o quyen `BackupManage`.

## GitNexus Impact Snapshot

Da chay impact truoc khi sua cac symbol hien huu:

- `BackupSettings` => `CRITICAL`
- `BackupJob` => `HIGH`
- `ConGNoDbContext` => `HIGH`
- `BackupQueue` => `MEDIUM`
- `BackupWorkerHostedService` => `LOW`
- `MapBackupEndpoints` => `LOW`
- `IBackupService` => `LOW`

Ghi nho: moi thay doi tren `BackupSettings`, `BackupJob`, `ConGNoDbContext` phai di theo lat nho, co test, va phai bao ro blast radius khi resume.

## Current Code Map

- DTO/contracts:
  - `src/backend/Application/Backups/BackupModels.cs`
  - `src/backend/Application/Backups/IBackupService.cs`
- Entities/EF:
  - `src/backend/Infrastructure/Data/Entities/BackupSettings.cs`
  - `src/backend/Infrastructure/Data/Entities/BackupJob.cs`
  - `src/backend/Infrastructure/Data/ConGNoDbContext.cs`
- Core services:
  - `src/backend/Infrastructure/Services/BackupService.cs`
  - `src/backend/Infrastructure/Services/BackupService.InternalOps.cs`
  - `src/backend/Infrastructure/Services/BackupQueue.cs`
  - `src/backend/Infrastructure/Services/BackupProcessRunner.cs`
- Hosted services:
  - `src/backend/Api/Services/BackupSchedulerHostedService.cs`
  - `src/backend/Api/Services/BackupWorkerHostedService.cs`
- API:
  - `src/backend/Api/Endpoints/BackupEndpoints.cs`
- Frontend:
  - `src/frontend/src/api/backup.ts`
  - `src/frontend/src/pages/AdminBackupPage.tsx`
  - `src/frontend/src/pages/admin/__tests__/admin-backup-page.test.tsx`
- Tests co san lien quan:
  - `src/backend/Tests.Unit/BackupQueueTests.cs`
  - `src/backend/Tests.Unit/BackupServicePendingScheduledTests.cs`
  - `src/backend/Tests.Unit/BackupServiceRuntimeNormalizationTests.cs`
  - `src/backend/Tests.Unit/BackupEndpointAntiforgeryTests.cs`

## Work Log

### 2026-04-12

- User yeu cau commit toan bo thay doi truoc do, sau do cau hinh not bien moi truong Google Drive that.
- Da chay `mcp__gitnexus__detect_changes(scope: "all")` truoc commit; risk tong the = `critical` vi scope backup/offsite rong, nhung user yeu cau commit toan bo worktree hien co.
- Da stage va commit baseline bang commit `6851fa1`:
  - `feat(backup): add Google Drive offsite backup`
- Da xac nhan worktree sach sau commit baseline.
- Dang bo sung env-plumbing cho:
  - `docker-compose.yml`
  - `.env.example`
  - `ENV_SAMPLE.md`
- Blocker con lai:
  - may hien tai chua co `BACKUP_OFFSITE_GOOGLE_DRIVE_CLIENT_ID` va `BACKUP_OFFSITE_GOOGLE_DRIVE_CLIENT_SECRET` that;
  - co the hoan tat wiring/documentation ngay, nhung de bat ket noi Google Drive that thi can user cung cap cap secret hoac inject qua secret manager/local `.env`.

- User yeu cau trien khai provider Google Drive that cho offsite upload.
- Da refresh lai GitNexus index bang `npx -y gitnexus@latest analyze`.
- Da tao bead `cng-6mo`, chuyen sang `in_progress`, va them `Phase 142` vao `task.md`.
- Da chay GitNexus impact cho cac symbol can dong vao:
  - `BackupWorkerHostedService` = `LOW`
  - `MapBackupEndpoints` = `LOW`
  - `DependencyInjection` = `MEDIUM`
  - `ConGNoDbContext` = `HIGH`
  - `BackupSettings` = `CRITICAL`
- Da chot huong implementation de tranh blast radius cao:
  - khong sua schema/entity `BackupSettings` va `ConGNoDbContext`;
  - giu contract admin API/UI hien tai;
  - provider that se di qua `IBackupOffsiteService`, luu refresh token ma hoa bang Data Protection, va upload bang Google Drive REST API.
- Da hoan thien `GoogleDriveBackupOffsiteService` theo huong production:
  - OAuth connect URL + callback exchange token;
  - ma hoa refresh token bang Data Protection;
  - refresh access token va multipart upload qua Google Drive REST API;
  - retry/fail status cho upload job da queue san.
- Da noi DI qua `BackupOffsiteGoogleDrive` config va fallback ve `NullBackupOffsiteService` khi config chua day du.
- Da bo sung unit tests moi trong `src/backend/Tests.Unit/GoogleDriveBackupOffsiteServiceTests.cs` cho:
  - connect URL;
  - callback luu refresh token;
  - upload success;
  - fail khi thieu local file.
- Da verify:
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --filter "FullyQualifiedName~GoogleDriveBackupOffsiteServiceTests.ProcessNextPendingUploadAsync_WhenQueuedBackupExists_UploadsAndMarksSuccess"` => pass (`1/1`);
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`222/222`).
- Da chay `mcp__gitnexus__detect_changes(scope: "all")`; `risk_level: critical` o muc worktree tong the vi repo dang co nhieu file ban san ngoai scope phase 142. Scope code cua phase nay tap trung vao provider Google Drive, DI/config, appsettings va unit tests backup.
- Da dong bead `cng-6mo`.

### 2026-04-11

- User yeu cau implement ke hoach nang cap backup len Google Drive theo huong production-grade, khong chi upload cho co.
- Da doc lai AGENTS.md, project-doc, va GitNexus instructions cua repo.
- Da refresh GitNexus index bang `npx -y gitnexus@latest analyze`.
- Da tao bead `cng-w5p` voi tieu de `Google Drive offsite backup hardening`, chuyen sang `in_progress`.
- Da them `Phase 139` vao `task.md` de theo doi checklist:
  - mo rong settings/contracts
  - them persistence + abstraction cho Google Drive OAuth
  - chuan hoa luong 2 pha local -> offsite
  - bo sung durability/recovery
  - mo rong admin API/UI
  - cap nhat tests
  - verify + `detect_changes`
- Da chay GitNexus impact cho cac symbol chinh va ghi nhan:
  - `BackupSettings` = `CRITICAL`
  - `BackupJob` = `HIGH`
  - `ConGNoDbContext` = `HIGH`
  - `BackupQueue` = `MEDIUM`
  - cac symbol bieu mat API/worker con lai = `LOW`
- Da ra soat code hien tai:
  - backup local co manual + scheduler + worker + retention + restore;
  - scheduler chi chay khi backend API dang song va settings `Enabled=true`;
  - queue hien tai la `ConcurrentQueue<Guid>` trong memory, khong co recovery sau restart;
  - endpoint backup chua co Google Drive/offsite;
  - `BackupService.ProcessJobAsync` hien tai vua tao dump local vua danh dau success/failure, chua co pha offsite rieng.
- Da chot huong trien khai:
  - `BackupService` van tao local dump;
  - sau local success, neu offsite bat va co ket noi hop le thi tao upload job/record rieng;
  - worker/recovery se lay DB lam source of truth cho job queued/running;
  - restore flow giu doc lap voi Drive.
- Da hoan tat backend offsite hardening:
  - them contracts/process runner/offsite abstraction moi trong `Application/Backups`;
  - them entities `BackupOffsiteConnection`, `BackupOffsiteUpload` + migration `039_backup_offsite_google_drive.sql`;
  - tach them logic `BackupService.Offsite.cs`, `NullBackupOffsiteService`, recovery/polling trong worker, va endpoint admin cho connect/callback/disconnect/test upload/reupload.
- Da hoan tat frontend admin backup:
  - mo rong `src/frontend/src/api/backup.ts` cho offsite settings, connection status va upload queue;
  - cap nhat `AdminBackupPage.tsx` de xu ly Google Drive callback, luu cau hinh offsite, test upload, disconnect va reupload;
  - bo sung regression test `src/frontend/src/pages/admin/__tests__/admin-backup-page.test.tsx`.
- Da verify:
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --no-restore` => pass (`218/218`);
  - `npm --prefix src/frontend test -- --run src/pages/admin/__tests__/admin-backup-page.test.tsx` => pass (`6/6`);
  - `npm --prefix src/frontend run build` => pass.
- Da chay `mcp__gitnexus__detect_changes(scope: "all")`; risk tong the = `critical` vi phase nay cham vao symbol trung tam backup flow va worktree co san thay doi tai lieu (`AGENTS.md`, `CLAUDE.md`). Process bi anh huong van nam trong luong backup/offsite nhu du kien.
- Da dong bead `cng-w5p`; trang thai hien tai la `CLOSED`.

## Next Action

1. Chay `mcp__gitnexus__detect_changes(scope: "all")` sau khi xong env-plumbing de chot scope rieng cua phase 143.
2. Neu user cung cap secret that, inject vao `.env` hoac secret manager ma khong lo ro gia tri trong chat/log.
3. Chi commit tiep phan env-plumbing/push neu user yeu cau ro rang.

## Resume Checklist

- Mo `task.md` va tim `Phase 143`
- `bd show cng-6zj`
- Nho rang commit baseline da xong o `6851fa1`
- Khong commit/push tiep neu user chua yeu cau
- Secret Google Drive that hien van thieu; can inject an toan, khong paste len chat/log
- Truoc khi ket thuc bat ky thay doi code moi: chay lai `mcp__gitnexus__detect_changes`
