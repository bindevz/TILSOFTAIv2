param(
    [string]$Server = "localhost",
    [string]$Database = "TILSOFTAI",
    [string]$User = "sa",
    [string]$Password = "123"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$sqlRoot = Join-Path $repoRoot "sql\current"
$migrateScript = Join-Path $PSScriptRoot "migrate-local-tilsoftai.ps1"

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    throw "sqlcmd was not found on PATH. Install SQL Server command line tools to run local migration idempotency tests."
}

function Invoke-TilsoftSqlFile {
    param([string]$Path)

    $name = Split-Path $Path -Leaf
    Write-Host "Validating $name"
    & $sqlcmd.Source -b -S $Server -d $Database -U $User -P $Password -C -i $Path
    if ($LASTEXITCODE -ne 0) {
        throw "SQL validation failed: $name"
    }
}

function Get-SeedCounts {
    $query = @"
SET NOCOUNT ON;
SELECT
    (SELECT COUNT(1) FROM dbo.Model WHERE TenantId = N'default' AND ModelCode IN (N'ABC', N'XYZ', N'SET-DINING-001')) AS ModelCount,
    (SELECT COUNT(1) FROM dbo.Material WHERE TenantId = N'default' AND MaterialCode IN (N'MAT-OAK', N'MAT-STL', N'MAT-LIN', N'MAT-FOAM')) AS MaterialCount,
    (SELECT COUNT(1) FROM dbo.ModelPiece WHERE TenantId = N'default') AS PieceCount,
    (SELECT COUNT(1) FROM dbo.ModelMaterial WHERE TenantId = N'default') AS MaterialLinkCount,
    (SELECT COUNT(1) FROM dbo.ModelPackagingOption WHERE TenantId = N'default') AS PackagingCount;
"@

    $result = & $sqlcmd.Source -b -S $Server -d $Database -U $User -P $Password -C -h -1 -W -s "," -Q $query
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to read seed row counts."
    }

    return ($result | Where-Object { $_ -match '^\d+,\d+,\d+,\d+,\d+$' } | Select-Object -First 1)
}

function Invoke-ValidationPack {
    $schemaValidation = Join-Path $sqlRoot "998_validate_schema_contract.sql"
    if (Test-Path $schemaValidation) {
        Invoke-TilsoftSqlFile -Path $schemaValidation
    }

    Invoke-TilsoftSqlFile -Path (Join-Path $sqlRoot "999_validate_model_runtime.sql")
}

Write-Host "Idempotency pass 1: migrate"
& $migrateScript -Server $Server -Database $Database -User $User -Password $Password
if ($LASTEXITCODE -ne 0) {
    throw "First migration failed."
}

Invoke-ValidationPack
$beforeCounts = Get-SeedCounts
Write-Host "Seed counts after first migration: $beforeCounts"

Write-Host "Idempotency pass 2: migrate"
& $migrateScript -Server $Server -Database $Database -User $User -Password $Password
if ($LASTEXITCODE -ne 0) {
    throw "Second migration failed."
}

Invoke-ValidationPack
$afterCounts = Get-SeedCounts
Write-Host "Seed counts after second migration: $afterCounts"

if ($beforeCounts -ne $afterCounts) {
    throw "Seed row counts changed after repeated migration. Before=$beforeCounts After=$afterCounts"
}

Write-Host "Local SQL migration idempotency test passed."
