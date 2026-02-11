# ENV_SAMPLE

## Connection string (preferred)
- ConnectionStrings__Default=Host=localhost;Port=5432;Database=congno_golden;Username=congno_app;Password=CHANGE_ME
- ConnectionStrings__Migrations=Host=localhost;Port=5432;Database=congno_golden;Username=congno_admin;Password=CHANGE_ME

## Auth (placeholder)
- Jwt__Secret=CHANGE_ME_SUPER_SECRET_32_CHARS!
- Jwt__Issuer=congno.local
- Jwt__Audience=congno.local
- Jwt__ExpiryMinutes=60
- Jwt__RefreshTokenDays=14
- Jwt__RefreshCookieName=congno_refresh
- Jwt__RefreshCookieSecure=false

## Seed admin user
- Seed__AdminUsername=admin
- Seed__AdminPassword=CHANGE_ME
- Seed__AdminFullName=System Admin
- Seed__AdminEmail=admin@example.com
- Seed__AdminReset=false

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

## Reminder scheduler
- Reminders__AutoRunEnabled=true
- Reminders__PollMinutes=360
