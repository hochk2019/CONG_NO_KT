# BACKUP_RESTORE_GUIDE

## Backup (pg_dump)
1) Create backup folder, e.g. C:\apps\congno\backup\dumps
2) Example script (daily):

@echo off
set PGBIN=C:\Program Files\PostgreSQL\16\bin
set BACKUPDIR=C:\apps\congno\backup\dumps
set DATESTAMP=%DATE:~-4%%DATE:~3,2%%DATE:~0,2%_%TIME:~0,2%%TIME:~3,2%
set DATESTAMP=%DATESTAMP: =0%

if not exist %BACKUPDIR% mkdir %BACKUPDIR%
"%PGBIN%\pg_dump.exe" -h localhost -p 5432 -U congno_admin -F c -b -f "%BACKUPDIR%\congno_golden_%DATESTAMP%.dump" congno_golden
forfiles /p "%BACKUPDIR%" /m *.dump /d -30 /c "cmd /c del @path"

3) Schedule with Task Scheduler (daily at 02:00).
4) Store password via PGPASSFILE or .pgpass.
5) Optional: use the checked-in script `scripts/db/backup_daily.bat` and customize paths.

## Restore
1) Stop API service.
2) Run:
pg_restore -h localhost -p 5432 -U congno_admin -d congno_golden -c "C:\apps\congno\backup\dumps\file.dump"
3) Start API service and verify health.
4) Optional: use `scripts/db/restore.ps1`:
```
powershell -ExecutionPolicy Bypass -File scripts\db\restore.ps1 -DumpFile "C:\apps\congno\backup\dumps\file.dump"
```

## Offsite copy
- Copy dumps to NAS / OneDrive / external disk on schedule.
