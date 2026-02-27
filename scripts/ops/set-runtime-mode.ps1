param(
  [ValidateSet("windows-service", "docker")]
  [string]$Mode = "docker",
  [string]$ConfigPath = "C:\apps\congno\ops\agent-config.json",
  [string]$ComposeFilePath = "C:\apps\congno\docker-compose.yml",
  [string]$WorkingDirectory = "C:\apps\congno",
  [string]$ProjectName = "congno",
  [string]$BackendService = "api",
  [string]$FrontendService = "web",
  [switch]$RestartAgent,
  [string]$AgentServiceName = "CongNoOpsAgent"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Set-NoteProperty {
  param(
    [Parameter(Mandatory = $true)] [psobject]$Object,
    [Parameter(Mandatory = $true)] [string]$Name,
    [AllowNull()] [object]$Value
  )

  $existing = $Object.PSObject.Properties[$Name]
  if ($null -ne $existing) {
    $existing.Value = $Value
  } else {
    $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
  }
}

if (-not (Test-Path $ConfigPath)) {
  throw "Config file not found: $ConfigPath"
}

$config = Get-Content -Raw $ConfigPath | ConvertFrom-Json

if (-not ($config.PSObject.Properties.Name -contains "runtime")) {
  $config | Add-Member -NotePropertyName runtime -NotePropertyValue ([pscustomobject]@{})
}

Set-NoteProperty -Object $config.runtime -Name "mode" -Value $Mode

if ($Mode -eq "docker") {
  if (-not ($config.runtime.PSObject.Properties.Name -contains "docker")) {
    $config.runtime | Add-Member -NotePropertyName docker -NotePropertyValue ([pscustomobject]@{})
  }

  Set-NoteProperty -Object $config.runtime.docker -Name "composeFilePath" -Value $ComposeFilePath
  Set-NoteProperty -Object $config.runtime.docker -Name "workingDirectory" -Value $WorkingDirectory
  Set-NoteProperty -Object $config.runtime.docker -Name "projectName" -Value $ProjectName
  Set-NoteProperty -Object $config.runtime.docker -Name "backendService" -Value $BackendService
  Set-NoteProperty -Object $config.runtime.docker -Name "frontendService" -Value $FrontendService
}

$config | ConvertTo-Json -Depth 12 | Out-File -FilePath $ConfigPath -Encoding UTF8
Write-Output "[OK] Updated runtime mode: $Mode"
Write-Output "[OK] Config path: $ConfigPath"

if ($Mode -eq "docker") {
  Write-Output "[OK] Docker compose: $ComposeFilePath"
  Write-Output "[OK] Docker project: $ProjectName"
  Write-Output "[OK] Services: backend=$BackendService, frontend=$FrontendService"
}

if ($RestartAgent) {
  Restart-Service -Name $AgentServiceName -ErrorAction Stop
  $service = Get-Service -Name $AgentServiceName -ErrorAction Stop
  Write-Output "[OK] Restarted service ${AgentServiceName}: $($service.Status)"
}
