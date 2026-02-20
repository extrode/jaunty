# Jaunty Standard Build Script
$ErrorActionPreference = "Stop"

Write-Host "--- Restoring Solution ---" -ForegroundColor Cyan
dotnet restore Jaunty.slnx

Write-Host "`n--- Building Solution (Release) ---" -ForegroundColor Cyan
dotnet build Jaunty.slnx -c Release --no-restore

Write-Host "`n--- Running Tests (net8.0) ---" -ForegroundColor Cyan
dotnet test Jaunty.slnx -c Release --no-build --framework net8.0 --filter "FullyQualifiedName~Sqlite"

Write-Host "`n--- Build and Test Successful! ---" -ForegroundColor Green
