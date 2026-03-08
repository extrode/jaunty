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

# Publish with AOT enabled
# -p:PublishAot=true : Enables NativeAOT
# -p:IsTrimmable=true : Tells the compiler the project is intended to be trimmable
# --self-contained : Required for AOT
dotnet publish $Project `
    -c Release `
    -r $Runtime `
    -f net8.0 `
    --self-contained `
    -p:PublishAot=true `
    -p:IsTrimmable=true `
    -p:TargetFrameworks=net8.0 `
    -o "./publish-aot"

$BinaryPath = Join-Path "./publish-aot" $BinaryName

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n--- AOT Publish Completed Successfully! ---" -ForegroundColor Green
    $size = (Get-Item $BinaryPath).Length / 1MB
    Write-Host ("Binary Size: {0:N2} MB" -f $size)
} else {
    Write-Host "`n--- AOT Publish Failed or produced critical warnings ---" -ForegroundColor Red
}
