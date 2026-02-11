param(
  [string]$PgBin = "C:\Program Files\PostgreSQL\16\bin",
  [string]$Database = "congno_golden",
  [string]$Username = "congno_admin",
  [string]$Host = "localhost",
  [int]$Port = 5432,
  [Parameter(Mandatory = $true)][string]$DumpFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$pgRestore = Join-Path $PgBin "pg_restore.exe"
if (-not (Test-Path $pgRestore)) {
  throw "pg_restore.exe not found at $pgRestore"
}

& $pgRestore -h $Host -p $Port -U $Username -d $Database -c $DumpFile
