$basePath = "C:\home\syed\code\_projects\Jaunty\20260101\Jaunty\tests\Jaunty.Tests"

Get-ChildItem -Path $basePath -Recurse -Filter "*.cs" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $newContent = $content

    # Replace old namespace references
    $newContent = $newContent -replace 'using Jaunty\.PublicApi\.Configuration;', 'using Jaunty.Configuration;'
    $newContent = $newContent -replace 'using Jaunty\.PublicApi\.Interfaces;', 'using Jaunty.Interfaces;'
    $newContent = $newContent -replace 'using Jaunty\.PublicApi;', 'using Jaunty.Core;'

    if ($content -ne $newContent) {
        Set-Content $_.FullName $newContent -NoNewline
        Write-Host "Fixed: $($_.FullName)"
    }
}
