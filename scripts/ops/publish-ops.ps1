param(
  [string]$RepoRoot = "E:\GPT\CONG_NO_KT",
  [string]$OutRoot = "C:\apps\congno\ops"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$opsRoot = Join-Path $RepoRoot "src\ops"
$agentProj = Join-Path $opsRoot "Ops.Agent\Ops.Agent.csproj"
$consoleProj = Join-Path $opsRoot "Ops.Console\Ops.Console.csproj"
$agentOut = Join-Path $OutRoot "agent"
$consoleOut = Join-Path $OutRoot "console"

if (-not (Test-Path $agentProj)) {
  throw "Ops.Agent.csproj not found: $agentProj"
}

if (-not (Test-Path $consoleProj)) {
  throw "Ops.Console.csproj not found: $consoleProj"
}

dotnet publish $agentProj -c Release -o $agentOut

dotnet publish $consoleProj -c Release -o $consoleOut

Write-Output "[OK] Published Ops.Agent -> $agentOut"
Write-Output "[OK] Published Ops.Console -> $consoleOut"
