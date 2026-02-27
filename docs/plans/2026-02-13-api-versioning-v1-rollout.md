# API Versioning Rollout Plan (`/api/v1`)

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Tài liệu này là kế hoạch/thực thi theo thời điểm viết, có thể chứa giả định cũ.
> Nguồn vận hành hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `task.md`.


## Goal
- Introduce `/api/v1/*` routes without breaking existing unversioned clients.
- Keep legacy routes available during migration window.

## Current implementation (2026-02-13)
- Added compatibility middleware:
  - Rewrites `/api/v1/{path}` to legacy `/{path}` for current handlers.
  - Adds `X-Api-Version` response header (`v1` or `unversioned`).
  - Marks unversioned API routes with:
    - `Deprecation: true`
    - `Sunset: Tue, 30 Jun 2026 23:59:59 GMT`
    - `Link: </api/v1{path}>; rel="successor-version"`
- Added unit tests for path rewrite and deprecation header behavior.

## Migration phases
1. **Phase A (now)**
   - Support both unversioned and `/api/v1` entrypoints.
   - Emit deprecation headers on unversioned API paths.
2. **Phase B (client migration)**
   - Frontend and integrations switch all calls to `/api/v1`.
   - Monitor request ratio and error rates by version header.
3. **Phase C (freeze)**
   - Stop adding new features to unversioned path aliases.
   - Keep bug/security fixes only.
4. **Phase D (retire)**
   - Remove unversioned compatibility after sunset date and communication window.

## Verification checklist
- `/api/v1/receipts` reaches the same handler as `/receipts`.
- Health/metrics/swagger routes are not tagged deprecated.
- Unversioned business routes include deprecation headers.
- Monitoring dashboards track request counts by `X-Api-Version`.

