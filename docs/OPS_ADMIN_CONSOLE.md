# OPS Admin Console

This module provides a Windows Agent service and a WPF Console app for operating the CongNoGolden stack (frontend, backend, database backups).

Runtime is now configurable:
- `docker`: backend/frontend as Docker Compose services (default).
- `windows-service`: backend via Windows Service + frontend via IIS (legacy/fallback).

## Build / Publish

```powershell
powershell -ExecutionPolicy Bypass -File scripts\ops\publish-ops.ps1
```

Outputs:
- `C:\apps\congno\ops\agent`
- `C:\apps\congno\ops\console`

## Install Agent as Windows Service (prod)

Run in Windows PowerShell (Admin):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\ops\install-agent.ps1
```

This will:
- Create `C:\apps\congno\ops\agent-config.json` if missing.
- Install and start the Windows service `CongNoOpsAgent`.
- Print the API key for Console connection.

Optional:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\ops\install-agent.ps1 -OpenFirewall
```

## Config

Default config path:
```
C:\apps\congno\ops\agent-config.json
```

Update the following fields before using in production:
- `database.connectionString`
- `database.pgBinPath` (optional, auto-detects common PostgreSQL install paths)
- `updates.mode` (`copy` or `git`)
- `updates.repoPath` (if `git` mode)
- `frontend.publicUrl` (URL to open frontend from Ops Console)
- `runtime.mode` (`windows-service` or `docker`)
- `runtime.docker.composeFilePath` (e.g. `C:\\apps\\congno\\docker-compose.yml`)
- `runtime.docker.workingDirectory` (folder to run `docker compose`)
- `runtime.docker.projectName` (compose project name)
- `runtime.docker.backendService` / `runtime.docker.frontendService` (service names in compose)

## Quick setup for Docker runtime

1) Switch agent runtime to Docker mode:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\ops\set-runtime-mode.ps1 `
  -Mode docker `
  -ConfigPath "C:\apps\congno\ops\agent-config.json" `
  -ComposeFilePath "C:\apps\congno\docker-compose.yml" `
  -WorkingDirectory "C:\apps\congno" `
  -ProjectName "congno" `
  -BackendService "api" `
  -FrontendService "web" `
  -RestartAgent
```

2) Create/update Ops Console profile and set it active:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\ops\set-console-profile.ps1 `
  -ProfileName "Docker Local" `
  -BaseUrl "http://127.0.0.1:6090" `
  -AgentConfigPath "C:\apps\congno\ops\agent-config.json" `
  -SetActive
```

Script will read API key from agent config automatically when available.

## Docker runtime validation checklist

Sau khi đổi runtime sang Docker, chạy tối thiểu các bước sau:

1) API health:

```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:8080/health"
Invoke-RestMethod -Uri "http://127.0.0.1:8080/health/ready"
```

2) Frontend:

```powershell
Invoke-WebRequest -Uri "http://127.0.0.1:8081" -UseBasicParsing
```

3) Nếu bật module ML risk: smoke endpoints admin `/admin/risk-ml/*` (models/runs/active/train/activate).

## Run Console (dev)

```powershell
dotnet run --project .\Ops.Console\Ops.Console.csproj
```

In production, run:
```
C:\apps\congno\ops\console\Ops.Console.exe
```

## Agent Endpoints

- `GET /health`
- `GET /config`
- `PUT /config`
- `GET /runtime/info`
- `GET /status`
- `POST /services/backend/start|stop|restart`
- `POST /services/frontend/start|stop`
- `GET /backups`
- `POST /backup/create`
- `POST /backup/restore` (body: `{ "filePath": "C:\\path\\file.dump" }`)
- `GET /logs/tail?path=...&lines=200`
- `GET /diagnostics`
- `POST /update/backend`
- `POST /update/frontend`
- `POST /services/backend/install`

All endpoints require `X-Api-Key` header (generated on first run).

## Notes

- Restore uses `pg_restore --clean --if-exists --no-owner --no-privileges` to avoid owner errors.
- Update `Updates.Mode` in config to `git` or `copy`.
- For `git` mode, ensure `git`, `dotnet`, and `npm` are in PATH on the server.
- Agent default BaseUrl is `http://0.0.0.0:6090`.
- If the console runs on another machine, open firewall port 6090 or use a reverse proxy.
- Ops Console has an **Endpoints** tab to open backend/frontend URLs.
- IIS binding management is only relevant in `windows-service` mode.
- In Docker mode, service actions map to `docker compose` (`up -d`, `stop`, `restart`) for configured service names.
- Nếu `GET /runtime/info` trả `404`, khả năng cao Ops Agent đang chạy bản cũ: cần publish + replace binary agent và restart service `CongNoOpsAgent`.
- Nếu service actions trả access denied, cần chạy agent/service dưới account có quyền quản trị service Docker/Windows Service tương ứng.
