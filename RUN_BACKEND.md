# RUN_BACKEND

## Prerequisites
- .NET 8 SDK
- PostgreSQL running with database congno_golden

## Configure
Option 1: set environment variable (run these **before** `dotnet run`)
```
setx ConnectionStrings__Default "Host=localhost;Port=5432;Database=congno_golden;Username=congno_app;Password=CongNo@123"
setx ConnectionStrings__Migrations "Host=localhost;Port=5432;Database=congno_golden;Username=congno_admin;Password=CHANGE_ME"
setx Jwt__Secret "CHANGE_ME_SUPER_SECRET_32_CHARS!"
setx Jwt__Issuer "congno.local"
setx Jwt__Audience "congno.local"
setx Seed__AdminUsername "admin"
setx Seed__AdminPassword "CHANGE_ME"
setx Seed__AdminReset "false"
setx Migrations__Enabled "true"
```

Option 2: edit `src/backend/Api/appsettings.Development.json` (ensure DB password matches your local Postgres)

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
