# OPS Admin Console

This module provides a Windows Agent service and a WPF Console app for operating the CongNoGolden stack (frontend, backend, database backups).

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
- Ops Console has an **Endpoints** tab to open backend/frontend URLs and manage IIS bindings (HTTP only).
