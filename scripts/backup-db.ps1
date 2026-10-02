$ErrorActionPreference = "Stop"

$database = "dealership"
$user = "dealership"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupDir = Join-Path $PSScriptRoot "..\backups"
$backupFile = Join-Path $backupDir "dealership-$timestamp.dump"

New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

Write-Host "Creating PostgreSQL backup..."

docker compose exec -T postgres pg_dump `
    -U $user `
    -d $database `
    -F c `
    > $backupFile

if ($LASTEXITCODE -ne 0) {
    Remove-Item $backupFile -ErrorAction SilentlyContinue
    throw "PostgreSQL backup failed."
}

Write-Host ""
Write-Host "Backup created:"
Write-Host (Resolve-Path $backupFile)