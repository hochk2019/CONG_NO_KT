param(
  [string]$ServiceName = "CongNoOpsAgent",
  [string]$AgentExe = "C:\apps\congno\ops\agent\Ops.Agent.exe",
  [string]$ConfigPath = "C:\apps\congno\ops\agent-config.json",
  [string]$BaseUrl = "http://0.0.0.0:6090",
  [string]$ApiKey = "",
  [string]$DbConnectionString = "Host=localhost;Port=5432;Database=congno_golden;Username=postgres;Password=CHANGE_ME",
  [string]$PgBinPath = "",
  [int]$RetentionCount = 10,
  [switch]$ForceConfig,
  [switch]$OpenFirewall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $AgentExe)) {
  throw "Agent exe not found: $AgentExe"
}

$shouldWriteConfig = $ForceConfig -or (-not (Test-Path $ConfigPath))
if ($shouldWriteConfig) {
  if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $ApiKey = [Guid]::NewGuid().ToString("N")
  }

  $config = @{
    agent = @{
      baseUrl = $BaseUrl
      apiKey = $ApiKey
      configPath = $ConfigPath
    }
    backend = @{
      serviceName = "CongNoGoldenApi"
      baseUrl = "http://127.0.0.1:8080"
      appPath = "C:\apps\congno\api"
      logPath = "C:\apps\congno\api\logs\api.log"
      exeName = "CongNoGolden.Api.exe"
    }
    frontend = @{
      iisSiteName = "CongNoGoldenWeb"
      appPath = "C:\apps\congno\web"
      publicUrl = "http://localhost:8081"
    }
    database = @{
      connectionString = $DbConnectionString
      pgBinPath = $PgBinPath
      retentionCount = $RetentionCount
    }
    paths = @{
      backupRoot = "C:\apps\congno\backup\ops"
      tempRoot = "C:\apps\congno\ops\tmp"
      logsRoot = "C:\apps\congno\ops\logs"
    }
    security = @{
      adminUser = "admin"
      adminPassword = "CHANGE_ME"
      allowedWindowsUsers = @()
    }
    updates = @{
      mode = "copy"
      repoPath = "C:\apps\congno\repo"
      backendPublishPath = "C:\apps\congno\api"
      frontendPublishPath = "C:\apps\congno\web"
      nssmPath = "C:\apps\congno\tools\nssm.exe"
    }
  }

  $configDir = Split-Path -Parent $ConfigPath
  if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
  }

  $config | ConvertTo-Json -Depth 6 | Out-File -FilePath $ConfigPath -Encoding UTF8
  Write-Output "[OK] Wrote config: $ConfigPath"
} else {
  $existing = Get-Content -Raw $ConfigPath | ConvertFrom-Json
  if (($null -ne $existing) -and ($null -ne $existing.agent)) {
    $ApiKey = $existing.agent.apiKey
  }
  Write-Output "[OK] Using existing config: $ConfigPath"
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
  $binaryPath = "`"$AgentExe`""
  New-Service -Name $ServiceName -BinaryPathName $binaryPath -DisplayName "CongNo Ops Agent" -StartupType Automatic
  Write-Output "[OK] Created service: $ServiceName"
} else {
  $binaryPath = "`"$AgentExe`""
  sc.exe config $ServiceName binPath= $binaryPath | Out-Null
  Write-Output "[OK] Updated service: $ServiceName"
}

if ($OpenFirewall) {
  $port = 6090
  try {
    $uri = [Uri]$BaseUrl
    if ($uri.Port -gt 0) {
      $port = $uri.Port
    }
  } catch {
    $port = 6090
  }

  $ruleName = "CongNo Ops Agent"
  $existingRule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
  if ($null -eq $existingRule) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow | Out-Null
    Write-Output "[OK] Opened firewall port $port"
  } else {
    Write-Output "[OK] Firewall rule exists: $ruleName"
  }
}

Start-Service -Name $ServiceName
$service = Get-Service -Name $ServiceName
$serviceStatus = $service.Status
Write-Output "[OK] Service status: $serviceStatus"
Write-Output "[OK] Agent BaseUrl: $BaseUrl"
if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
  Write-Output "[OK] Agent ApiKey: $ApiKey"
} else {
  Write-Output "[WARN] Agent ApiKey is empty in config"
}
