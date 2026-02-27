# RUNBOOK

## Deployment mode (current)
- Default: Docker Compose (`db`, `api`, `web`).
- Legacy fallback: Windows Service + IIS (chỉ dùng cho môi trường cũ).

## Health checks
- `GET /health`
- `GET /health/ready` (DB connectivity)

## Services (Docker)
- Backend API: `api` service.
- Frontend: `web` service (Nginx reverse proxy `/api` -> `api`).
- Database: `db` service (PostgreSQL).

## Restart procedures (Docker)
- Restart one service: `docker compose restart api` (or `web`, `db`)
- Restart full stack: `docker compose up -d --build`

## Log locations (Docker)
- `docker compose logs api --tail=200`
- `docker compose logs web --tail=200`
- Optional file log in container (if configured): `/var/lib/congno/logs/api.log`

## Common incidents
1) API 500 / DB errors
   - Verify env in `.env` (`POSTGRES_*`, `JWT_*`, `SEED_*`)
   - Check `db` container status: `docker compose ps`
   - Check API logs: `docker compose logs api --tail=200`

2) Unauthorized / JWT errors
   - Verify `JWT_SECRET` length >= 32 chars
   - Verify `JWT_ISSUER` and `JWT_AUDIENCE`
   - Verify cookie settings in `.env` (`JWT_REFRESH_COOKIE_*`)

3) Import commit blocked
   - Check period locks
   - Use override with reason (Admin/Supervisor)

4) Maintenance jobs chay cham / backlog
   - Kiem tra danh sach job:
     - `GET /admin/maintenance/jobs?take=50`
   - Kiem tra chi tiet job:
     - `GET /admin/maintenance/jobs/{jobId}`
   - Day job vao queue (async):
     - `POST /admin/health/reconcile-balances/queue`
     - `POST /admin/health/run-retention/queue`
   - Theo doi metrics:
     - `congno_maintenance_queue_depth`
     - `congno_maintenance_queue_delay_ms`
     - `congno_maintenance_job_duration_ms`

## Rollback steps
1) Stop app services: `docker compose stop api web`
2) Restore DB from latest dump (see `BACKUP_RESTORE_GUIDE.md`)
3) Checkout previous release tag/commit (if code rollback required)
4) Rebuild and start: `docker compose up -d --build`
5) Verify health (`/health`, `/health/ready`, frontend home)

## Smoke test
- Login
- Import preview
- Approve receipt sample
- Export Excel

## Scale readiness references
- Baseline load test: `docs/performance/LOAD_TESTING_BASELINE.md`
- Queue/worker operations: `docs/performance/QUEUE_WORKER_OPERATIONS.md`
- Read replica routing: `docs/performance/READ_REPLICA_ROUTING.md`
- Autoscaling guardrails: `docs/performance/AUTOSCALING_GUARDRAILS.md`

## Task tracking (task.md + Beads)
- Template and guide: `docs/beads-starter/TASKS_GUIDE.md`
- Starter repo files: `docs/beads-starter/`
- Hướng dẫn khởi tạo dự án mới: `docs/beads-starter/NEW_PROJECT_SETUP.md`
- Quy ước:
  - `task.md` chỉ giữ roadmap/phase/epic.
  - Task thực thi và trạng thái chi tiết dùng Beads.
  - Luôn ghi notes trước khi dừng phiên làm việc.
