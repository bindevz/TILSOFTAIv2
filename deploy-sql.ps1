# Deploy all SQL migration scripts
$ErrorActionPreference = "Stop"
$sqlDir = "d:\DevProject\Source\bindevz\TILSOFTAIv2\sql"
$server = "."
$database = "TILSOFTAI"
$userId = "sa"
$password = "123"

Write-Host "Deploying TILSOFTAI database schema..." -ForegroundColor Green

# Get all SQL files in order
$sqlFiles = Get-ChildItem -Path $sqlDir -Filter "*.sql" -Recurse | Sort-Object FullName

Write-Host "Found $($sqlFiles.Count) SQL files" -ForegroundColor Cyan

$successCount = 0
$errorCount = 0
$failedFiles = @()

foreach ($file in $sqlFiles) {
    $relativePath = $file.FullName.Substring($sqlDir.Length + 1)
    Write-Host "`nExecuting: $relativePath" -ForegroundColor Yellow
    
    try {
        sqlcmd -S $server -d $database -U $userId -P $password -i $file.FullName -b 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Success" -ForegroundColor Green
            $successCount++
        }
        else {
            Write-Host "  Failed (exit code: $LASTEXITCODE)" -ForegroundColor Red
            $errorCount++
            $failedFiles += $relativePath
        }
    }
    catch {
        Write-Host "  Error: $_" -ForegroundColor Red
        $errorCount++
        $failedFiles += $relativePath
    }
}

Write-Host "`n========================================"  -ForegroundColor Cyan
Write-Host "Deployment Complete" -ForegroundColor Green
Write-Host "Success: $successCount" -ForegroundColor Green
Write-Host "Errors: $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })
Write-Host "========================================" -ForegroundColor Cyan

if ($errorCount -gt 0) {
    Write-Host "`nFailed scripts:" -ForegroundColor Red
    foreach ($f in $failedFiles) {
        Write-Host "  - $f" -ForegroundColor Red
    }
    exit 1
}
