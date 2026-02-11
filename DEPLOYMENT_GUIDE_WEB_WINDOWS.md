# DEPLOYMENT_GUIDE_WEB_WINDOWS - Cong No Golden (Web + PostgreSQL on Windows)

This guide describes a simple production deployment on Windows Server:
- PostgreSQL as the database.
- .NET 8 backend API as a Windows service.
- React frontend served by IIS (static site) with optional reverse proxy to the API.

## 1) Requirements
- Windows Server 2019/2022
- .NET 8 Runtime (ASP.NET Core Runtime)
- PostgreSQL 16+
- IIS + URL Rewrite (and ARR if you want reverse proxy)

## 2) Database setup (PostgreSQL)
1) Create database + roles:
```sql
CREATE DATABASE congno_golden ENCODING 'UTF8';
CREATE ROLE congno_app LOGIN PASSWORD 'CHANGE_ME_STRONG';
CREATE ROLE congno_admin LOGIN PASSWORD 'CHANGE_ME_STRONG_ADMIN';
GRANT ALL PRIVILEGES ON DATABASE congno_golden TO congno_admin;
```

2) Run migrations with admin role (recommended):
- Set environment variables on the server:
```
setx ConnectionStrings__Default "Host=localhost;Port=5432;Database=congno_golden;Username=congno_app;Password=..."
setx ConnectionStrings__Migrations "Host=localhost;Port=5432;Database=congno_golden;Username=congno_admin;Password=..."
setx Migrations__Enabled "true"
```
- Start the API once to apply migrations from `scripts/db/migrations`.

3) If you prefer manual grants (least privilege for app role):
```sql
\c congno_golden
GRANT USAGE ON SCHEMA congno TO congno_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA congno TO congno_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA congno TO congno_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA congno
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO congno_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA congno
GRANT USAGE, SELECT ON SEQUENCES TO congno_app;
```

4) Firewall:
- Keep port 5432 open only to LAN.
- Do not expose DB to the internet.

## 3) Backend deployment (.NET 8)
1) Publish:
```powershell
dotnet publish -c Release -o C:\apps\congno\api
```

2) Configure production settings (env vars or appsettings.Production.json):
- ConnectionStrings__Default
- Jwt__Secret / Jwt__Issuer / Jwt__Audience
- Seed__AdminUsername / Seed__AdminPassword
- (optional) ConnectionStrings__Migrations + Migrations__Enabled

3) Run as Windows service (NSSM example):
```powershell
nssm install CongNoGoldenApi "C:\apps\congno\api\CongNoGolden.Api.exe"
nssm set CongNoGoldenApi AppDirectory "C:\apps\congno\api"
nssm set CongNoGoldenApi AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
nssm start CongNoGoldenApi
```

4) Health check:
- http://localhost:8080/health
- http://localhost:8080/health/ready

## 4) Frontend deployment (IIS)
1) Build:
```powershell
cd src\frontend
npm ci
npm run build
```

2) Copy `dist` to IIS site folder, e.g. `C:\apps\congno\web`.

3) IIS site:
- Physical path: `C:\apps\congno\web`
- Binding: port 80/443, host `congno.local`
- Ensure `web.config` exists in `dist` (Vite copies it from `public/`).

## 5) Reverse proxy (optional)
If you want a single domain:
- Web: http://congno.local/
- API: http://congno.local/api/* -> http://localhost:8080/*

Enable proxy in ARR and add a rewrite rule:
- Match URL: `^api/(.*)`
- Action: Rewrite to `http://localhost:8080/$1`

Then set frontend env:
```
VITE_API_BASE_URL=/api
```

## 6) Smoke test
Use the smoke script after deployment:
```
powershell -ExecutionPolicy Bypass -File scripts\e2e\smoke.ps1 `
  -BaseUrl http://localhost:8080 -Username admin -Password CHANGE_ME
```

## 7) Backup/restore
See `BACKUP_RESTORE_GUIDE.md` for pg_dump/pg_restore steps and scheduling.
