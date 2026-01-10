$basePath = "C:\home\syed\code\_projects\Jaunty\20260101\Jaunty\src\Jaunty"

Get-ChildItem -Path $basePath -Recurse -Filter "*.cs" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'partial class Jaunty') {
        # Replace namespaces like Jaunty.Read, Jaunty.Internals, Jaunty.Multiple, Jaunty.Streaming, Jaunty.Fluent
        $newContent = $content -replace 'namespace Jaunty\.(Read|Internals|Multiple|Streaming|Fluent);', 'namespace Jaunty;'
        if ($content -ne $newContent) {
            Set-Content $_.FullName $newContent -NoNewline
            Write-Host "Fixed: $($_.FullName)"
        }
    }
}
