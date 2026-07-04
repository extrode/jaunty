# Jaunty NativeAOT evaluation script
$ErrorActionPreference = "Continue" # Allow script to continue so we see all warnings

$Project = "src/Jaunty.Scaffolding.Cli/Jaunty.Scaffolding.Cli.csproj"

# Auto-detect runtime identifier and binary name based on OS
if ($IsLinux) {
    $Runtime = "linux-x64"
    $BinaryName = "Jaunty.Scaffolding.Cli"
} elseif ($IsMacOS) {
    $Runtime = "osx-x64"
    $BinaryName = "Jaunty.Scaffolding.Cli"
} else {
    $Runtime = "win-x64"
    $BinaryName = "Jaunty.Scaffolding.Cli.exe"
}

Write-Host "--- Attempting NativeAOT Publish for Jaunty.Scaffolding.Cli ---" -ForegroundColor Cyan
Write-Host "Runtime: $Runtime" -ForegroundColor Gray

# Publish with AOT enabled. PublishAot comes from the Cli csproj: passing
# it as -p: makes it a GLOBAL property that flows into referenced projects,
# and the netstandard2.0 source generator fails NETSDK1207 under it.
# --self-contained : Required for AOT
dotnet publish $Project `
    -c Release `
    -r $Runtime `
    --self-contained `
    -o "./publish-aot"

$BinaryPath = Join-Path "./publish-aot" $BinaryName

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n--- AOT Publish Completed Successfully! ---" -ForegroundColor Green
    $size = (Get-Item $BinaryPath).Length / 1MB
    Write-Host ("Binary Size: {0:N2} MB" -f $size)
} else {
    Write-Host "`n--- AOT Publish Failed or produced critical warnings ---" -ForegroundColor Red
}
