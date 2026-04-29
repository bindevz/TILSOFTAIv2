param(
    [string]$Server = "localhost",
    [string]$Database = "TILSOFTAI",
    [string]$User = "sa",
    [string]$Password = "123"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$sqlRoot = Join-Path $repoRoot "sql\current"

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    throw "sqlcmd was not found on PATH. Install SQL Server command line tools to run local SQL validation."
}

foreach ($name in @("998_validate_schema_contract.sql", "999_validate_model_runtime.sql")) {
    $path = Join-Path $sqlRoot $name
    if (-not (Test-Path $path)) {
        throw "Validation SQL file not found: $path"
    }

    Write-Host "Executing $name"
    & $sqlcmd.Source -b -S $Server -d $Database -U $User -P $Password -C -i $path
    if ($LASTEXITCODE -ne 0) {
        throw "SQL validation failed: $name"
    }
}

Write-Host "TILSOFTAI local SQL schema/runtime validation completed."
