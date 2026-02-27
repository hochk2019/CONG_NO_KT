> [!IMPORTANT]
> **HISTORICAL DOCUMENT**
> Tài liệu này là snapshot/lịch sử để tham khảo, **không phải nguồn vận hành chuẩn hiện tại**.
> Nguồn chuẩn hiện tại:
> - Deploy: DEPLOYMENT_GUIDE_DOCKER.md
> - Runbook: RUNBOOK.md
> - Ops runtime: docs/OPS_ADMIN_CONSOLE.md
# DEPLOYMENT_GUIDE_WEB_WINDOWS — Công nợ Golden (WEB + PostgreSQL) trên Windows Server1

Tài liệu này hướng dẫn triển khai hệ thống **không cần cài client**: người dùng mở trình duyệt trong LAN để dùng.

## 0) Kiến trúc triển khai khuyến nghị

- **Server1 (Windows)**
  - PostgreSQL (DB): `congno_golden`
  - Backend API (.NET 8): chạy như **Windows Service**
  - Frontend (React build): phục vụ qua **IIS** (static) + reverse proxy tới API *(khuyến nghị)*  
- **Client**: Edge/Chrome → truy cập URL nội bộ (VD: `http://congno.local`)

> Có thể dùng HTTP trong LAN, nhưng khuyến nghị bật HTTPS nội bộ nếu có điều kiện.

---

## 1) Yêu cầu hệ thống (Server1)

- Windows Server 2016+ (khuyến nghị 2019/2022)
- CPU 4 cores+, RAM 8–16GB+
- Disk SSD khuyến nghị
- Mạng LAN ổn định, IP tĩnh

### Ports đề xuất
- 5432: PostgreSQL (chỉ LAN)
- 8080: Backend API (nội bộ hoặc chỉ localhost nếu reverse proxy)
- 80/443: IIS (frontend + reverse proxy)

---

## 2) Cài PostgreSQL (Windows Service)

### 2.1 Cài đặt
- Cài PostgreSQL bản stable (khuyến nghị 16+).
- Đặt mật khẩu cho user `postgres`.
- Ghi lại đường dẫn `psql`, `pg_dump` (thường trong `C:\Program Files\PostgreSQL\16\bin`).

### 2.2 Tạo DB + role
Mở `psql` (hoặc pgAdmin) chạy:

```sql
CREATE DATABASE congno_golden ENCODING 'UTF8';
CREATE ROLE congno_app LOGIN PASSWORD 'CHANGE_ME_STRONG';
CREATE ROLE congno_admin LOGIN PASSWORD 'CHANGE_ME_STRONG_ADMIN';

GRANT ALL PRIVILEGES ON DATABASE congno_golden TO congno_admin;
```

Sau khi chạy schema `db_schema_postgresql.sql`, cấp quyền tối thiểu cho `congno_app`:

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

> Nếu bạn muốn chặt hơn: chặn DELETE, chỉ dùng soft-delete/void, và expose thao tác qua API.

### 2.3 pg_hba.conf (chỉ cho LAN)
Mở file `pg_hba.conf` và thêm rule cho subnet LAN (ví dụ 192.168.1.0/24):

```
# Allow app from LAN
host    congno_golden    congno_app     192.168.1.0/24    scram-sha-256
host    congno_golden    congno_admin   192.168.1.0/24    scram-sha-256
```

- Trong `postgresql.conf`, bật:
  - `listen_addresses = '*'` (nếu cần cho máy khác truy cập)
  - `password_encryption = scram-sha-256`

Khởi động lại service PostgreSQL.

### 2.4 Windows Firewall
- Chỉ mở 5432 cho **LAN subnet**.
- Không mở ra internet.

---

## 3) Deploy Backend (.NET 8) trên Windows

### Option A — Backend Windows Service (khuyến nghị)
1) Cài .NET 8 Runtime (ASP.NET Core Runtime) trên Server1.
2) Build publish:
```powershell
dotnet publish -c Release -o C:\apps\congno\api
```

3) Cấu hình `appsettings.Production.json` (hoặc env vars):
- Connection string:
  - Host=localhost;Port=5432;Database=congno_golden;Username=congno_app;Password=...
- JWT secret / auth settings
- AllowedOrigins (LAN) (nếu không dùng reverse proxy)
- Logging path

4) Chạy như Windows Service:
- Khuyến nghị dùng **NSSM** (dễ, ổn định) hoặc built-in WindowsService trong .NET.

**NSSM** (ví dụ):
```powershell
nssm install CongNoGoldenApi "C:\apps\congno\api\CongNoGolden.Api.exe"
nssm set CongNoGoldenApi AppDirectory "C:\apps\congno\api"
nssm set CongNoGoldenApi AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
nssm start CongNoGoldenApi
```

5) Healthcheck:
- `GET http://localhost:8080/health` OK
- `GET http://localhost:8080/health/ready` OK (kiểm tra kết nối DB)

### 3.6 Monitoring/Alerting (khuyến nghị)
- Bật structured logging + log rotation (file hoặc Windows Event Log).
- Giám sát `/health` + `/health/ready` theo lịch.
- (Tuỳ chọn) Prometheus + Grafana cho metrics.

---

## 4) Deploy Frontend (React) bằng IIS (khuyến nghị)

### 4.1 Cài IIS + module cần thiết
- Bật role **IIS** (Web Server)
- Cài:
  - **URL Rewrite**
  - **ARR (Application Request Routing)** nếu dùng reverse proxy

### 4.2 Build frontend
```powershell
npm ci
npm run build
```
Copy folder build (vd `dist/`) lên Server1:
- `C:\apps\congno\web`

### 4.3 Tạo IIS Site
- Site name: `CongNoGoldenWeb`
- Physical path: `C:\apps\congno\web`
- Binding: port 80 (và 443 nếu HTTPS), host: `congno.local` (nếu có DNS)

### 4.4 SPA fallback (React Router)
Thêm file `web.config` trong thư mục web để route SPA:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="ReactRouter" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

---

## 5) Reverse proxy API qua IIS (để 1 domain duy nhất)

Mục tiêu:
- Web: `http://congno.local/`
- API: `http://congno.local/api/*` → proxy tới `http://localhost:8080/*`

### 5.1 Bật proxy trong ARR
- IIS Manager → Application Request Routing Cache → Server Proxy Settings
- Tick “Enable proxy”

### 5.2 Rule rewrite cho /api
Tạo rewrite rule:
- Match URL: `^api/(.*)`
- Action: Rewrite → `http://localhost:8080/$1`
- Preserve query string

> Cách này giúp tránh CORS phức tạp. Frontend gọi relative `/api/...`.

---

## 6) HTTPS nội bộ (tuỳ chọn nhưng tốt)
- Tạo cert self-signed hoặc dùng CA nội bộ.
- Bind 443 trong IIS.
- Nếu self-signed: cài cert vào “Trusted Root” trên các máy client (GPO hoặc thủ công).

---

## 7) Backup/Restore PostgreSQL (Task Scheduler)

### 7.1 Script backup (backup_daily.bat)
Tạo `C:\apps\congno\backup\backup_daily.bat`:

```bat
@echo off
set PGBIN=C:\Program Files\PostgreSQL\16\bin
set BACKUPDIR=C:\apps\congno\backup\dumps
set DATESTAMP=%DATE:~-4%%DATE:~3,2%%DATE:~0,2%_%TIME:~0,2%%TIME:~3,2%
set DATESTAMP=%DATESTAMP: =0%

if not exist %BACKUPDIR% mkdir %BACKUPDIR%

"%PGBIN%\pg_dump.exe" -h localhost -p 5432 -U congno_admin -F c -b -f "%BACKUPDIR%\congno_golden_%DATESTAMP%.dump" congno_golden

forfiles /p "%BACKUPDIR%" /m *.dump /d -30 /c "cmd /c del @path"
```

- Tạo Task Scheduler chạy 1 lần/ngày (VD 02:00).
- Lưu user/password an toàn (PGPASSFILE/.pgpass).

### 7.2 Restore
```powershell
pg_restore -h localhost -p 5432 -U congno_admin -d congno_golden -c "C:\apps\congno\backup\dumps\file.dump"
```

### 7.3 Offsite copy (khuyến nghị)
- Copy thư mục `C:\apps\congno\backup\dumps` sang:
  - NAS trong LAN, hoặc
  - Cloud sync folder (OneDrive/Google Drive), hoặc
  - USB external (định kỳ)

---

## 8) Quy trình update (khuyến nghị)
1) Backup DB trước.
2) Deploy backend:
   - stop service → replace binaries → start service
3) Chạy migrations (nếu có).
4) Deploy frontend:
   - replace build folder web.
5) Smoke test:
   - login
   - import preview
   - approve receipt sample
   - export excel

Rollback:
- restore DB dump (nếu migration sai)
- rollback binaries (giữ bản cũ)

---

## 9) Go-live checklist (pass/fail)
- [ ] DNS/URL nội bộ hoạt động (`congno.local`)
- [ ] API health OK
- [ ] DB backup task chạy OK
- [ ] Firewall đúng subnet LAN
- [ ] Import staging/preview/commit OK
- [ ] Ownership approve OK
- [ ] Period lock chặn đúng
- [ ] Export Excel mở được đúng template
- [ ] Audit log có before/after cho commit/approve/void


