param(
  [string]$BaseUrl = "http://localhost:8080",
  [string]$Username = $env:CONGNO_SMOKE_USERNAME,
  [string]$Password = $env:CONGNO_SMOKE_PASSWORD
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$BaseUrl = $BaseUrl.TrimEnd("/")

function Assert-Status {
  param(
    [string]$Name,
    [int]$Expected,
    [int]$Actual
  )
  if ($Actual -ne $Expected) {
    throw "$Name expected $Expected but got $Actual"
  }
  Write-Host "[OK] $Name -> $Actual"
}

function Require-Value {
  param(
    [string]$Name,
    [string]$Value
  )
  if ([string]::IsNullOrWhiteSpace($Value)) {
    throw "$Name is required. Set CONGNO_SMOKE_USERNAME/CONGNO_SMOKE_PASSWORD or pass -$Name."
  }
}

Require-Value "Username" $Username
Require-Value "Password" $Password

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

$health = Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing
Assert-Status "health" 200 $health.StatusCode

$ready = Invoke-WebRequest -Uri "$BaseUrl/health/ready" -UseBasicParsing
Assert-Status "health/ready" 200 $ready.StatusCode

$loginBody = @{ username = $Username; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$BaseUrl/auth/login" -Method Post -Body $loginBody -ContentType "application/json" -WebSession $session
if (-not $login.accessToken) {
  throw "login did not return accessToken"
}
Write-Host "[OK] login -> token received"

$refresh = Invoke-RestMethod -Uri "$BaseUrl/auth/refresh" -Method Post -WebSession $session
if (-not $refresh.accessToken) {
  throw "refresh did not return accessToken"
}
Write-Host "[OK] refresh -> token received"

$authHeader = @{ Authorization = "Bearer $($refresh.accessToken)" }
$customers = Invoke-WebRequest -Uri "$BaseUrl/customers?page=1&pageSize=1" -Headers $authHeader -UseBasicParsing
Assert-Status "customers" 200 $customers.StatusCode

$logout = Invoke-WebRequest -Uri "$BaseUrl/auth/logout" -Method Post -WebSession $session -UseBasicParsing
Assert-Status "logout" 204 $logout.StatusCode

try {
  Invoke-WebRequest -Uri "$BaseUrl/auth/refresh" -Method Post -WebSession $session -UseBasicParsing
  throw "refresh after logout should fail but succeeded"
} catch {
  $status = $_.Exception.Response.StatusCode.Value__
  Assert-Status "refresh-after-logout" 401 $status
}

Write-Host "[OK] smoke test completed"
