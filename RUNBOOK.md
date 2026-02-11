# RUNBOOK

## Health checks
- GET /health
- GET /health/ready (DB connectivity)

## Services
- Backend API: Windows Service (NSSM) or IIS.
- Frontend: IIS static site.

## Restart procedures
- NSSM: nssm restart CongNoGoldenApi
- IIS site: iisreset or restart site in IIS Manager

## Log locations
- Windows Event Viewer (Application logs) if running as service.
- File logs if configured in appsettings.Production.json.

## Common incidents
1) API 500 / DB errors
   - Verify ConnectionStrings__Default
   - Check PostgreSQL service running
   - Check pg_hba.conf and firewall

2) Unauthorized / JWT errors
   - Verify Jwt__Secret length >= 32 chars
   - Verify Jwt__Issuer and Jwt__Audience

3) Import commit blocked
   - Check period locks
   - Use override with reason (Admin/Supervisor)

## Rollback steps
1) Stop API service
2) Restore previous binaries
3) Restore DB from latest dump (see BACKUP_RESTORE_GUIDE.md)
4) Start API service and verify health

## Smoke test
- Login
- Import preview
- Approve receipt sample
- Export Excel

## Task tracking (task.md + Beads)
- Template and guide: `docs/beads-starter/TASKS_GUIDE.md`
- Starter repo files: `docs/beads-starter/`
- Hướng dẫn khởi tạo dự án mới: `docs/beads-starter/NEW_PROJECT_SETUP.md`
- Quy ước:
  - `task.md` chỉ giữ roadmap/phase/epic.
  - Task thực thi và trạng thái chi tiết dùng Beads.
  - Luôn ghi notes trước khi dừng phiên làm việc.
