param(
    [string]$Root = (Join-Path $PSScriptRoot "..")
)

$ErrorActionPreference = "Stop"

$testsRoot = Join-Path $Root "tests\Jaunty.Tests"
$integrationRoot = Join-Path $testsRoot "Integration"

$targets = @{
    "Sqlite\Read" = "Read"
    "Sqlite\Write" = "Write"
    "Sqlite\Multiple" = "Multiple"
    "Sqlite\Streaming" = "Streaming"
}

foreach ($target in $targets.GetEnumerator()) {
    $destination = Join-Path $integrationRoot $target.Value
    if (-not (Test-Path $destination)) {
        New-Item -ItemType Directory -Path $destination | Out-Null
    }

    $source = Join-Path $integrationRoot $target.Key
    if (-not (Test-Path $source)) {
        continue
    }

    Get-ChildItem -Path $source -File -Filter *.cs | ForEach-Object {
        $destFile = Join-Path $destination $_.Name
        Move-Item -Path $_.FullName -Destination $destFile -Force

        $content = Get-Content -Path $destFile -Raw
        $content = $content.Replace("namespace Jaunty.Tests.Integration.Sqlite.$($target.Value);", "namespace Jaunty.Tests.Integration.$($target.Value);")
        Set-Content -Path $destFile -Value $content
    }
}

$foldersToRemove = @(
    (Join-Path $integrationRoot "Sqlite\Read"),
    (Join-Path $integrationRoot "Sqlite\Write"),
    (Join-Path $integrationRoot "Sqlite\Multiple"),
    (Join-Path $integrationRoot "Sqlite\Streaming")
)

foreach ($folder in $foldersToRemove) {
    if ((Test-Path $folder) -and -not (Get-ChildItem -Path $folder -Force)) {
        Remove-Item -Path $folder -Force
    }
}

Write-Output "Reorganization move complete."
