# QA_REPORT

> [!IMPORTANT]
> This file contains both historical QA notes and newer verification records.
> - Latest verification snapshot: **2026-02-23 (quality gate sync)**.
> - The section `Date: 2026-01-08` is legacy baseline data.

Current status reference: 2026-02-23 (final verification block below)
Tester: Codex CLI

## Summary
- Total cases: 7 (smoke)
- Pass: 7
- Fail: 0
- Blocked: 0

## Checklist status
- QA_CHECKLIST.md: PARTIAL (smoke only)
- UAT_SCRIPT.md: NOT RUN
- scripts/e2e/smoke.ps1: PASSED
- Performance report: DONE (sample dataset)
- Backup/restore test: PASSED (dump + restore to temp DB)

## Issues
- None

## Evidence
- Backup: `tmp/backup/dumps/congno_golden_20260108_211906.dump`
- Offsite copy: `tmp/backup/offsite/congno_golden_20260108_211906.dump`
- Restore verification: roles=4, users=2
- Perf report: `tmp/perf/perf_report.txt`
- Smoke re-run: 2026-01-08, BaseUrl http://localhost:8080 (PASS)

## Verification Update (2026-02-13)

Environment: Docker runtime (`api:18080`, `web:18081`, `db:15432`)

- Runtime Zalo config inside API container:
  - `Zalo__Enabled=false`
  - `Zalo__OaId` empty
  - `Zalo__AccessToken` empty
  - `Zalo__WebhookToken` empty
- Health checks:
  - `GET /health` => `200`
  - `GET /health/ready` => `200`
  - `GET /webhooks/zalo` => `200`
- Zalo link/reminder checks:
  - `GET /zalo/link/status` (admin) => `linked=true`
  - `POST /reminders/run` => `totalCandidates=22`, `sentCount=2`, `failedCount=0`, `skippedCount=42`
  - `GET /reminders/logs?channel=ZALO&status=SENT` => `0` (chưa có bằng chứng OA thật gửi thành công)
- Playwright:
  - Smoke: `npx playwright test e2e/auth-login.spec.ts e2e/dashboard.spec.ts e2e/receipts-flow.spec.ts` => `2 passed, 1 skipped`
  - Full: `npx playwright test` => fail hàng loạt do login timeout khi chạy song song (nhiều test bị giữ ở `/login`)
  - Re-check: `npx playwright test e2e/auth-login.spec.ts --workers=1` => pass
- Test suites:
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`90/90`)
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`36/36`)
  - `npm run -C src/frontend test` => pass (`77/77`)

## Verification Update (2026-02-13, final)

Environment: Local workspace (Windows, PowerShell)

- Backend tests:
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`94/94`)
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`36/36`)
- Frontend tests:
  - `npm run --prefix src/frontend test -- --run` => pass (`83/83`)
- Build verification:
  - `npm run --prefix src/frontend build` => pass
  - `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass

Notes:
- Coverage of this rerun focuses on the completed Medium/Low execution scope (excluding Email channel integration).

## Verification Update (2026-02-23, quality gate sync)

Environment: Local workspace (Windows, PowerShell)

- Frontend quality gate:
  - `npm --prefix src/frontend run lint` => pass (đã xử lý lỗi hooks tại `useTheme` và `RiskAlertsPage`).
  - `npm --prefix src/frontend run test -- --run` => pass (`90/90`).
  - `npm --prefix src/frontend run build` => pass.
- Backend quality gate:
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj --nologo --verbosity minimal` => pass (`116/116`).
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --nologo --verbosity minimal` => pass (`41/41`).
- Tracker sync:
  - `bd close cng-fwg` => bead ERP integration đã đóng.
  - `bd ready --json` => `[]` (không còn task ready/in-progress).
