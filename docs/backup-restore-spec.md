# Database Backup & Restore System - Technical Spec

## 1. Overview
Xay dung he thong sao luu/khai phuc CSDL PostgreSQL cho CongNo, bao gom backup thu cong va tu dong, thao tac tren frontend, co audit log, bao mat quyen, va co the download/restore file `.dump`. Thiet ke toi uu cho Windows + DB local, de mo rong neu can.

## 2. Scope
### In scope
- Backup manual + scheduled (day of week + time)
- UI config backup path, retention, pg_bin path
- Download dump (token TTL)
- Restore tu backup hoac upload file
- Audit log + maintenance mode

### Out of scope
- PITR, incremental backup
- Offsite/cloud backup
- Multi-region replication

## 3. Requirements
### Functional
- Backup manual via UI (Admin + Ke toan truong).
- Backup scheduled theo ngay trong tuan + gio (config tren UI).
- Retention theo so luong, default 10, ap dung cho ca manual + scheduled.
- Cho phep cau hinh thu muc luu backup (default `C:\apps\congno\backup\dumps`).
- Cho phep cau hinh duong dan `pg_dump` / `pg_restore`.
- Cho phep download file `.dump` tu UI, token TTL 30 phut.
- Cho phep upload file `.dump` de restore (ngoai danh sach backup).
- Restore chi Admin, co xac nhan 2 buoc (type "RESTORE").
- UI co toast + banner khi job chay/hoan tat.
- Backup size khong gioi han (tuy DB).
- Audit log day du (ai, luc nao, ket qua, loi).
- Neu trung job backup thi xep hang va thong bao.
- Timezone theo Windows server.
- Format backup dung `pg_dump` custom format `.dump`.

### Non-functional
- Duong dan file va lenh backup/restore phai hoat dong tren Windows.
- Bao mat file dump khi download (token TTL, kiem tra quyen).

## 4. Architecture Summary
### Backend modules
- BackupSettingsStore: doc/DB access for backup_settings.
- BackupJobStore: CRUD + pagination for backup_jobs.
- BackupAuditStore: write/read audit events.
- BackupService: chay `pg_dump`, tao file dump, cap nhat job + audit.
- RestoreService: chay `pg_restore`, maintenance mode on/off, cap nhat audit.
- BackupSchedulerHostedService: doc settings, tinh next run, chạy job dinh ky.
- BackupQueue: hang doi job manual + scheduled (de xu ly trung job).

### Concurrency & safety
- Su dung PostgreSQL advisory lock de tranh chay job trung (future scale).
- Hang doi job backup: neu manual bam khi job dang chay, job vao queue.
- Restore lock rieng (khong cho backup/restore song song).

### File storage
- Backup files luu local folder (configurable).
- Metadata luu DB (backup_jobs), file duoc stream qua API khi download.

### Maintenance mode
- Khi restore, backend bat maintenance mode -> chan request thong thuong.
- Frontend hien banner "Dang phuc hoi du lieu" trong thoi gian restore.

## 5. Data Model
### Table: backup_settings (single row)
- id (uuid, pk)
- enabled (bool)
- backup_path (text)
- retention_count (int, default 10)
- schedule_day_of_week (int, 0-6)
- schedule_time (text, HH:mm)
- timezone (text, default Windows local timezone id)
- pg_bin_path (text) - folder chua pg_dump/pg_restore
- last_run_at (timestamp)
- updated_at (timestamp)

### Table: backup_jobs
- id (uuid, pk)
- type (text: manual | scheduled)
- status (text: queued | running | success | failed)
- started_at (timestamp)
- finished_at (timestamp)
- file_name (text)
- file_size (bigint)
- file_path (text)
- created_by (uuid, nullable for scheduled)
- error_message (text, nullable)
- stdout_log (text, nullable) - log chi tiet (co the truncate o backend)
- stderr_log (text, nullable) - log chi tiet (co the truncate o backend)
- download_token (text, nullable)
- download_token_expires_at (timestamp, nullable)

### Table: backup_audit
- id (uuid, pk)
- action (text: backup_manual | backup_scheduled | restore | download | settings_update)
- actor_id (uuid, nullable for scheduled)
- timestamp (timestamp)
- result (text: success | failed)
- details (json/text)

### Table: backup_uploads
- id (uuid, pk)
- file_name (text)
- file_size (bigint)
- file_path (text)
- created_by (uuid, nullable)
- created_at (timestamp)
- expires_at (timestamp)

## 6. API Design
Base path: `/admin/backup`

### Settings
- `GET /settings` -> backup_settings
- `PUT /settings`
  - body: enabled, backup_path, retention_count, schedule_day_of_week, schedule_time, pg_bin_path
  - audit: settings_update

### Backup jobs
- `POST /run` (manual backup)
  - action: create job (queued), enqueue
- `GET /jobs?page=&status=&type=`
- `GET /jobs/{id}`
- `POST /jobs/{id}/download-token` -> token TTL 30 minutes
- `GET /download/{jobId}` (stream file)
  - requires valid download_token (TTL 30 minutes)
  - action: stream file if exists

### Restore
- `POST /restore`
  - body: { jobId } OR { uploadId }
  - only Admin
  - requires confirm phrase "RESTORE"
  - maintenance mode on during restore

### Upload for restore
- `POST /upload` (multipart/form-data)
  - field: file (.dump)
  - returns uploadId (temporary record) + metadata

### Audit
- `GET /audit?page=`

### Status
- `GET /status` -> maintenance flag + message

## 7. Scheduler & Job Execution
### Scheduler (BackgroundService)
- Doc backup_settings khi start va moi chu ky.
- Tinh `nextRunAt` dua tren (day_of_week, time, timezone).
- Sleep den `nextRunAt`, sau do enqueue job scheduled.
- Neu settings thay doi, cap nhat lich tiep theo.

### Queue & Locks
- BackupQueue la hang doi in-memory (FIFO).
- Job manual neu dang co job running -> vao queue, UI hien trang thai "Queued".
- Truoc khi chay job, acquire PostgreSQL advisory lock:
  - `pg_try_advisory_lock(hashtext('backup_job'))`
  - Neu fail -> skip (khong chay trung).
- Restore co lock rieng, khong cho backup/restore song song.

### Job lifecycle
- queued -> running -> success/failed
- Cap nhat backup_jobs + audit o moi trang thai.
- Luu stdout/stderr (co truncate).

## 8. Backup Flow (Manual + Scheduled)
### Manual
1) User nhan "Tao ban sao luu".
2) API `POST /admin/backup/run` tao job (queued).
3) BackupQueue chay job -> running.
4) BackupService chay pg_dump, tao file .dump.
5) Update job -> success/failed, luu log, ghi audit.
6) Retention cleanup (giu N ban).
7) UI cap nhat trang thai + hien nut "Tai ve".

### Scheduled
1) Scheduler enqueue job theo lich.
2) BackupQueue chay job voi advisory lock.
3) Luu job + audit + retention giong manual.

## 9. Restore Flow
### Restore from existing backup
1) Admin chon job -> bam "Restore".
2) UI canh bao 2 buoc (type "RESTORE").
3) API `POST /admin/backup/restore` (jobId).
4) Backend bat maintenance mode.
5) RestoreService chay pg_restore -c.
6) Update audit -> success/failed.
7) Tat maintenance mode.

### Restore from upload
1) Admin upload file `.dump`.
2) API `POST /admin/backup/upload` -> uploadId.
3) UI chon file vua upload -> restore (2-step confirm).
4) Flow giong restore tu job.

## 10. Security & Permissions
### Roles
- Backup (manual/scheduled config + download): Admin + Ke toan truong
- Restore: Admin only
- Audit view: Admin + Ke toan truong

### Access control rules
- Moi endpoint `/admin/backup/*` bat buoc auth.
- Download phai co token TTL 30 phut, kiem tra role.
- Restore bat buoc xac nhan 2 buoc (type "RESTORE").
- Upload file `.dump` chi cho Admin.

## 11. UI/UX Design
### Location
- Dat trong menu "Admin" (neu khong co, dat trong Settings/Tools).

### Layout
1) **Tu dong sao luu**
   - Toggle enabled
   - Chon ngay trong tuan + gio
   - Backup path
   - Retention count
   - Pg bin path
   - Button "Luu cau hinh"

2) **Sao luu thu cong**
   - Button "Tao ban sao luu ngay"
   - Hien trang thai: queued/running/success/failed
   - Neu running: banner thong bao

3) **Danh sach backup**
   - Columns: Thoi gian, Loai, Trang thai, Kich thuoc, Nguoi tao
   - Actions: Tai ve, Restore (Admin), Xem log/loi

4) **Upload & Restore**
   - Upload file `.dump`
   - Xac nhan 2 buoc (type "RESTORE")

5) **Audit log**
   - Action, actor, timestamp, result

### Notifications
- Toast when job queued/running/completed/failed.
- Banner during running/restore maintenance mode.

## 12. Logging & Audit
### Logs
- Luu stdout/stderr vao `backup_jobs` (co truncate).
- Hien log trong UI neu job failed.

### Audit
- Ghi `backup_audit` moi khi:
  - settings_update
  - backup_manual / backup_scheduled
  - download
  - restore
- Luu result (success/failed) + message.

## 13. Testing Strategy
### Unit tests
- BackupService: tao file path, parse process exit, retention logic.
- RestoreService: maintenance mode on/off, error handling.
- Scheduler: tinh nextRunAt, enqueue logic.

### Integration tests
- API permissions (Admin vs Ke toan truong).
- Manual backup -> job status -> download token.
- Restore flow -> maintenance mode set/unset.

### E2E (frontend)
- UI config settings
- Manual backup + queued status
- Download with token TTL
- Restore confirm flow (Admin only)

## 14. Rollout Plan
1) Deploy backend with new tables + services.
2) Deploy frontend UI (admin page).
3) Enable feature toggle (backup enabled = false by default).
4) Ops config: set backup path + pg_bin_path.
5) Run manual backup test.
6) Enable schedule.

## 15. Risks & Mitigations
- **Disk full** -> retention + warning banner.
- **pg_dump not found** -> validate pg_bin_path on save.
- **Large DB** -> long running job; UI shows running, no timeout.
- **Concurrent instances** -> advisory lock prevents duplicate run.
- **Restore failure** -> maintenance mode off, audit + error log.

## 16. Decision Log
- Scheduler in-app (BackgroundService) instead of Task Scheduler.
- Retention by count (default 10).
- Backup format: pg_dump custom `.dump`.
- Download token TTL 30 minutes.
- Restore requires 2-step confirm and maintenance mode.
