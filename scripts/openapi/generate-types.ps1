$ErrorActionPreference = 'Stop'

Write-Host "Generating OpenAPI types for frontend..." -ForegroundColor Cyan
Write-Host "Expecting backend swagger at: http://localhost:8080/swagger/v1/swagger.json" -ForegroundColor DarkGray

npm --prefix src/frontend run openapi:gen

Write-Host "Done." -ForegroundColor Green
