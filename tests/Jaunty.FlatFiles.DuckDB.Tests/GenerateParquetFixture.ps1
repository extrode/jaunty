# Generate Parquet test fixture for Jaunty.FlatFiles tests
$outputPath = "data\parquet\inventory.parquet"

# Ensure directory exists
$dir = Split-Path $outputPath -Parent
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

# Load DuckDB.NET
Add-Type -Path "bin\Debug\net8.0\DuckDB.NET.Data.dll" -ErrorAction SilentlyContinue

# If assembly not found, use the test project's referenced packages
$connectionString = "DataSource=:memory:"
$connection = New-Object DuckDB.NET.Data.DuckDBConnection($connectionString)
$connection.Open()

$cmd = $connection.CreateCommand()
$cmd.CommandText = @"
CREATE TABLE inventory AS 
SELECT 
    i as sku,
    'Item ' || (i % 100) as name,
    floor(random() * 1000)::int as stockLevel,
    round(random() * 999 + 1, 2) as unitPrice,
    (random() > 0.2) as isActive
FROM range(1, 101) t(i)
"@
$cmd.ExecuteNonQuery()

$escapedPath = $outputPath.Replace("'", "''")
$cmd.CommandText = "COPY inventory TO '$escapedPath' (FORMAT PARQUET)"
$cmd.ExecuteNonQuery()

Write-Host "Generated $outputPath"

# Verify
$cmd.CommandText = "SELECT COUNT(*) FROM read_parquet('$escapedPath')"
$count = $cmd.ExecuteScalar()
Write-Host "Verified: $count rows"

$connection.Close()
$connection.Dispose()
