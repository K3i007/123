param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

Write-Host "Restoring PostgreSQL database..."
Write-Host "Backup: $BackupFile"

Get-Content -Path $BackupFile -AsByteStream |
    docker compose exec -T postgres pg_restore `
        -U dealership `
        -d dealership `
        --clean `
        --if-exists

if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL restore failed."
}

Write-Host ""
Write-Host "Database restored successfully."