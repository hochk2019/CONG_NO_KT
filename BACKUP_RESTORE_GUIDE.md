# BACKUP_RESTORE_GUIDE

## Deployment mode
- Default runtime: Docker Compose.
- Legacy fallback: direct PostgreSQL on Windows host.

## Backup (Docker, recommended)
1) Ensure backup host folder exists (from `.env`): `BACKUP_HOST_PATH` (default `./data/backup/dumps`).
2) Create dump from `db` container:

```powershell
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$dump = ".\\data\\backup\\dumps\\congno_golden_$ts.dump"
docker compose exec -T db pg_dump -U "$env:POSTGRES_USER" -F c -b "$env:POSTGRES_DB" > $dump
```

3) Verify dump file and keep retention policy (e.g. 30 days) via Task Scheduler script.

Notes:
- Nếu chưa set biến môi trường shell `POSTGRES_USER`/`POSTGRES_DB`, thay trực tiếp bằng giá trị thực tế (ví dụ `congno_app`, `congno_golden`).
- Có thể dùng Ops Agent backup endpoint nếu đã cấu hình `BACKUP_*` trong compose env.

## Restore (Docker, recommended)
1) Stop app services to avoid write conflicts:
```powershell
docker compose stop api web
```

2) Copy dump vào DB container và restore:
```powershell
docker cp ".\\data\\backup\\dumps\\file.dump" congno-db:/tmp/file.dump
docker compose exec -T db pg_restore -U "$env:POSTGRES_USER" -d "$env:POSTGRES_DB" --clean --if-exists --no-owner --no-privileges /tmp/file.dump
```

3) Start services and verify:
```powershell
docker compose up -d api web
```
- Check `GET /health`, `GET /health/ready`, and frontend home page.

## Legacy restore (non-Docker fallback)
Nếu bạn chạy PostgreSQL trực tiếp trên Windows host:
```powershell
pg_restore -h localhost -p 5432 -U congno_admin -d congno_golden -c "C:\\apps\\congno\\backup\\dumps\\file.dump"
```

## Offsite copy
- Copy dumps to NAS / OneDrive / external disk on schedule.
