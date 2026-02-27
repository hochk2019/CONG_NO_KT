param(
  [string]$ProfileName = "Docker Local",
  [string]$BaseUrl = "http://127.0.0.1:6090",
  [string]$ApiKey = "",
  [string]$AgentConfigPath = "C:\apps\congno\ops\agent-config.json",
  [string]$SettingsPath = "",
  [int]$AutoRefreshSeconds = 10,
  [bool]$AdvancedModeEnabled = $true,
  [switch]$SetActive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
  $SettingsPath = Join-Path $env:APPDATA "CongNoOps\console-settings.json"
}

if ([string]::IsNullOrWhiteSpace($ApiKey) -and (Test-Path $AgentConfigPath)) {
  try {
    $agentConfig = Get-Content -Raw $AgentConfigPath | ConvertFrom-Json
    if (($null -ne $agentConfig) -and ($null -ne $agentConfig.agent) -and -not [string]::IsNullOrWhiteSpace($agentConfig.agent.apiKey)) {
      $ApiKey = $agentConfig.agent.apiKey
    }
  } catch {
    Write-Warning "Cannot read agent config at ${AgentConfigPath}: $($_.Exception.Message)"
  }
}

if (Test-Path $SettingsPath) {
  $settings = Get-Content -Raw $SettingsPath | ConvertFrom-Json
} else {
  $settings = [pscustomobject]@{
    activeProfileId = ""
    profiles = @()
    autoRefreshSeconds = $AutoRefreshSeconds
    advancedModeEnabled = $AdvancedModeEnabled
  }
}

if (-not ($settings.PSObject.Properties.Name -contains "profiles") -or $null -eq $settings.profiles) {
  $settings | Add-Member -NotePropertyName profiles -NotePropertyValue @() -Force
}

$profiles = @($settings.profiles)
$target = $profiles | Where-Object { $_.name -eq $ProfileName } | Select-Object -First 1

if ($null -eq $target) {
  $target = [pscustomobject]@{
    id = [Guid]::NewGuid().ToString("N")
    name = $ProfileName
    baseUrl = $BaseUrl
    apiKey = $ApiKey
    lastUsedAt = $null
  }
  $profiles += $target
} else {
  $target.baseUrl = $BaseUrl
  $target.apiKey = $ApiKey
}

$settings.profiles = $profiles
$settings.autoRefreshSeconds = $AutoRefreshSeconds
$settings.advancedModeEnabled = $AdvancedModeEnabled

if ($SetActive -or [string]::IsNullOrWhiteSpace($settings.activeProfileId)) {
  $settings.activeProfileId = $target.id
}

$settingsDir = Split-Path -Parent $SettingsPath
if (-not (Test-Path $settingsDir)) {
  New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
}

$settings | ConvertTo-Json -Depth 8 | Out-File -FilePath $SettingsPath -Encoding UTF8

Write-Output "[OK] Updated console profile: $ProfileName"
Write-Output "[OK] BaseUrl: $BaseUrl"
Write-Output "[OK] Settings path: $SettingsPath"
if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
  Write-Output "[OK] ApiKey loaded"
} else {
  Write-Output "[WARN] ApiKey is empty; update manually if needed"
}
if ($SetActive -or -not [string]::IsNullOrWhiteSpace($settings.activeProfileId)) {
  Write-Output "[OK] Active profile ID: $($settings.activeProfileId)"
}
