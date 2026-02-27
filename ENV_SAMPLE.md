# ENV_SAMPLE

Deployment note:
- Runtime triển khai chuẩn hiện tại là Docker Compose.
- Khi chạy Docker, ưu tiên dùng root `.env.example` -> copy sang `.env`.
- File này giữ vai trò reference đầy đủ các biến backend.

## Connection string (preferred)
- ConnectionStrings__Default=Host=localhost;Port=5432;Database=congno_golden;Username=congno_app;Password=CHANGE_ME
- ConnectionStrings__Migrations=Host=localhost;Port=5432;Database=congno_golden;Username=congno_admin;Password=CHANGE_ME
- ConnectionStrings__ReadReplica=Host=localhost;Port=5432;Database=congno_golden;Username=congno_read;Password=CHANGE_ME
- ConnectionStrings__Redis=localhost:6379

## Auth (placeholder)
- Jwt__Secret=PUT_A_RANDOM_SECRET_WITH_AT_LEAST_32_CHARACTERS
- Jwt__Issuer=congno.local
- Jwt__Audience=congno.local
- Jwt__ExpiryMinutes=60
- Jwt__RefreshTokenDays=14
- Jwt__RefreshTokenAbsoluteDays=90
- Jwt__RefreshCookieName=congno_refresh
- Jwt__RefreshCookieSecure=true
- Jwt__RefreshCookieSameSite=Strict
- Jwt__RefreshCookiePath=/
- AuthSecurity__EnableLoginLockout=true
- AuthSecurity__MaxFailedLoginAttempts=5
- AuthSecurity__LockoutMinutes=15
- Cors__AllowedOrigins__0=http://localhost:5173
- Cors__AllowedOrigins__1=http://127.0.0.1:5173

Production note:
- Tuyệt đối không dùng `CHANGE_ME_SUPER_SECRET_32_CHARS!` ở production.
- `Jwt:Secret` không còn được commit trong `appsettings*.json`; bắt buộc inject bằng secret manager hoặc environment variables.
- Nếu deploy direct API (không reverse proxy `/api`), có thể đặt lại `Jwt__RefreshCookiePath=/auth`.

## Seed admin user
- Seed__AdminUsername=admin
- Seed__AdminPassword=CHANGE_ME
- Seed__AdminFullName=System Admin
- Seed__AdminEmail=admin@example.com
- Seed__AdminReset=false

Password policy note:
- Mật khẩu tạo mới qua API admin phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và số.
- Refresh token có ràng buộc context theo dual-signal (device fingerprint + IP prefix): chỉ từ chối khi **đồng thời** lệch cả 2 tín hiệu.

## Migrations
- Migrations__Enabled=false
- Migrations__ScriptsPath=scripts/db/migrations

## Reports
- Reports__TemplatePath=Templates/Mau_DoiSoat_CongNo_Golden.xlsx

## Zalo reminders (optional)
- Zalo__Enabled=false
- Zalo__OaId=2804410978830725257
- Zalo__ApiBaseUrl=https://openapi.zalo.me/v2.0/oa/message
- Zalo__AccessToken=CHANGE_ME
- Zalo__WebhookToken=CHANGE_ME
- Zalo__LinkCodeMinutes=15
- Zalo__RetryMaxAttempts=3
- Zalo__RetryBaseDelayMs=250
- Zalo__RetryMaxDelayMs=2000
- Zalo__CircuitBreakerFailureThreshold=5
- Zalo__CircuitBreakerOpenSeconds=30

## Reminder scheduler
- Reminders__AutoRunEnabled=true
- Reminders__PollMinutes=360

## Data retention scheduler
- DataRetention__AutoRunEnabled=true
- DataRetention__PollMinutes=1440
- DataRetention__AuditLogRetentionDays=365
- DataRetention__ImportStagingRetentionDays=90
- DataRetention__RefreshTokenRetentionDays=30

## Read model cache
- ReadModelCache__Enabled=true
- ReadModelCache__NamespaceVersionHours=24

## Risk ML training scheduler
- RiskModelTraining__AutoRunEnabled=true
- RiskModelTraining__PollMinutes=1440
- RiskModelTraining__ModelKey=risk_overdue_30d
- RiskModelTraining__LookbackMonths=12
- RiskModelTraining__HorizonDays=30
- RiskModelTraining__MinSamples=200
- RiskModelTraining__AutoActivate=true
- RiskModelTraining__MinAucGain=0.005
- RiskModelTraining__LearningRate=0.08
- RiskModelTraining__MaxIterations=900
- RiskModelTraining__L2Penalty=0.02

## Observability (optional)
- Observability__ServiceName=congno-api
- Observability__EnableConsoleExporter=false
- Observability__OtlpEndpoint=
- Observability__EnablePrometheusExporter=true
- Observability__PrometheusScrapeEndpointPath=/metrics
