# RUN_BACKEND

Deployment note:
- Tài liệu này dành cho chạy backend trực tiếp khi dev/debug.
- Triển khai chuẩn của hệ thống dùng Docker Compose, xem `DEPLOYMENT_GUIDE_DOCKER.md`.

## Prerequisites
- .NET 8 SDK
- PostgreSQL running with database congno_golden

## Configure
Option 1: set environment variable (run these **before** `dotnet run`)
```
setx ConnectionStrings__Default "Host=localhost;Port=5432;Database=congno_golden;Username=congno_app;Password=CongNo@123"
setx ConnectionStrings__Migrations "Host=localhost;Port=5432;Database=congno_golden;Username=congno_admin;Password=CHANGE_ME"
setx Jwt__Secret "PUT_A_RANDOM_SECRET_WITH_AT_LEAST_32_CHARACTERS"
setx Jwt__Issuer "congno.local"
setx Jwt__Audience "congno.local"
setx Jwt__RefreshTokenAbsoluteDays "90"
setx Jwt__RefreshCookieSameSite "Lax"
setx Jwt__RefreshCookiePath "/auth"
setx Zalo__CircuitBreakerFailureThreshold "5"
setx Zalo__CircuitBreakerOpenSeconds "30"
setx DataRetention__AutoRunEnabled "true"
setx DataRetention__PollMinutes "1440"
setx DataRetention__AuditLogRetentionDays "365"
setx DataRetention__ImportStagingRetentionDays "90"
setx DataRetention__RefreshTokenRetentionDays "30"
setx RiskModelTraining__AutoRunEnabled "true"
setx RiskModelTraining__PollMinutes "1440"
setx RiskModelTraining__LookbackMonths "12"
setx RiskModelTraining__HorizonDays "30"
setx RiskModelTraining__MinSamples "200"
setx RiskModelTraining__AutoActivate "true"
setx Seed__AdminUsername "admin"
setx Seed__AdminPassword "CHANGE_ME"
setx Seed__AdminReset "false"
setx Migrations__Enabled "true"
setx Observability__ServiceName "congno-api-dev"
setx Observability__EnableConsoleExporter "true"
# setx Observability__OtlpEndpoint "http://localhost:4317"
```

Option 2: edit `src/backend/Api/appsettings.Development.json` cho DB/local flags, **nhung van phai cung cap `Jwt__Secret` qua env** (vi secret da bo khoi appsettings tracked):
```
$env:Jwt__Secret = "PUT_A_RANDOM_SECRET_WITH_AT_LEAST_32_CHARACTERS"
```

## Run
```
cd src/backend/Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:8080"
dotnet run
```
Then open:
- http://localhost:8080/health
- http://localhost:8080/health/ready

## Login
```
POST http://localhost:8080/auth/login
body: { "username": "admin", "password": "CHANGE_ME" }
```

Response returns access token; refresh token is set as HttpOnly cookie.
Use the access token in `Authorization: Bearer <token>` for protected endpoints.

Refresh:
```
POST http://localhost:8080/auth/refresh
```

Logout:
```
POST http://localhost:8080/auth/logout
```

## Import upload (staging)
```
POST http://localhost:8080/imports/upload (multipart/form-data)
fields:
  type=INVOICE|ADVANCE|RECEIPT
  file=@your.xlsx
```

Preview:
```
GET http://localhost:8080/imports/{batchId}/preview?page=1&pageSize=50
```

Commit:
```
POST http://localhost:8080/imports/{batchId}/commit
body: { "idempotency_key": "uuid" }
```

Rollback:
```
POST http://localhost:8080/imports/{batchId}/rollback
```

Receipt preview:
```
POST http://localhost:8080/receipts/preview
body: { "sellerTaxCode": "...", "customerTaxCode": "...", "amount": 100000, "allocationMode": "FIFO" }
```

Receipt approve:
```
POST http://localhost:8080/receipts/{receiptId}/approve
body: { "version": 0, "selectedTargets": [ { "id": "uuid", "targetType": "INVOICE" } ] }
```

Reports:
```
GET http://localhost:8080/reports/summary?from=2025-01-01&to=2025-01-31&groupBy=customer
GET http://localhost:8080/reports/statement?customerTaxCode=...&from=2025-01-01&to=2025-01-31
GET http://localhost:8080/reports/aging?asOfDate=2025-01-31
GET http://localhost:8080/reports/export?from=2025-01-01&to=2025-01-31&asOfDate=2025-01-31
```

## Notes
- Use `global.json` to pin SDK version.
- De reset mat khau admin: dat `Seed__AdminReset=true` va `Seed__AdminPassword`, chay API mot lan roi tat flag.
- Migrations are disabled by default; enable only when `ConnectionStrings__Migrations` uses an admin role.
- API se fail-fast neu `Jwt__Secret` ngan hon 32 ky tu. O moi truong khong phai Development, placeholder mac dinh bi chan.
- Mat khau tao moi qua `/admin/users` phai dat policy toi thieu 8 ky tu, co chu hoa + chu thuong + so.
- Refresh token dung sliding expiry (`Jwt__RefreshTokenDays`) nhung bi gioi han boi absolute expiry (`Jwt__RefreshTokenAbsoluteDays`).
- Refresh token co dual-signal binding (device fingerprint + IP prefix), chi chan khi lech dong thoi ca hai tin hieu.
- `/health/ready` tra ve `checks` gom DB + cac worker toggle (reminder/invoice reconcile/customer reconcile/data retention).
- Observability ho tro OpenTelemetry metrics/tracing; bat console exporter cho local hoac dat `Observability__OtlpEndpoint` de day du lieu toi collector.
- Zalo client co retry/backoff + circuit breaker (`Zalo__CircuitBreakerFailureThreshold`, `Zalo__CircuitBreakerOpenSeconds`) de giam spam call khi downstream outage.
- Co endpoint admin trigger retention thu cong: `POST /admin/health/run-retention`.
- Co endpoint admin cho Risk ML:
  - `POST /admin/risk-ml/train`
  - `GET /admin/risk-ml/models`
  - `GET /admin/risk-ml/runs`
  - `POST /admin/risk-ml/models/{id}/activate`
- `/health/ready` co them `checks.riskModelWorker` de theo doi scheduler train model.
