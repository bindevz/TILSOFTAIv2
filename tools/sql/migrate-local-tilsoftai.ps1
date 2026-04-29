param(
    [string]$Server = "localhost",
    [string]$Database = "TILSOFTAI",
    [string]$User = "sa",
    [string]$Password = "123"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$sqlRoot = Join-Path $repoRoot "sql\current"

if (-not (Test-Path $sqlRoot)) {
    throw "SQL migration folder not found: $sqlRoot"
}

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    throw "sqlcmd was not found on PATH. Install SQL Server command line tools to run local migration."
}

function Invoke-TilsoftSqlFile {
    param(
        [string]$Path,
        [string]$TargetDatabase
    )

    $name = Split-Path $Path -Leaf
    Write-Host "Executing $name"

    & $sqlcmd.Source `
        -b `
        -S $Server `
        -d $TargetDatabase `
        -U $User `
        -P $Password `
        -C `
        -i $Path `
        -v "DatabaseName=$Database"

    if ($LASTEXITCODE -ne 0) {
        throw "SQL script failed: $name"
    }
}

$scripts = Get-ChildItem -Path $sqlRoot -Filter "*.sql" | Sort-Object Name

foreach ($script in $scripts) {
    $target = if ($script.Name -eq "000_create_database.sql") { "master" } else { $Database }
    Invoke-TilsoftSqlFile -Path $script.FullName -TargetDatabase $target
}

Write-Host "TILSOFTAI local SQL migration completed."
