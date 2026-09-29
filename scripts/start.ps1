$ErrorActionPreference = "Stop"
if (-not (Test-Path ".env")) { Copy-Item ".env.example" ".env"; Write-Host "Se creó .env. Actualiza las credenciales de desarrollo antes de continuar." }
Get-Content ".env" | ForEach-Object {
  if ($_ -match '^\s*([^#=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2].Trim(), "Process") }
}
npm --prefix frontend install
docker compose up -d postgres
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project backend/src/Dealership.Api"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "npm --prefix frontend run dev"
Write-Host "Frontend: http://localhost:3000 | API/Swagger: http://localhost:5080/swagger"
