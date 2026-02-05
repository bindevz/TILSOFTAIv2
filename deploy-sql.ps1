# ╔══════════════════════════════════════════════════════════════════════════════╗
# ║                          *** SAMPLE SCRIPT ONLY ***                          ║
# ║                                                                              ║
# ║  This script is provided as a SAMPLE for local development ONLY.            ║
# ║  DO NOT use in production. Configure proper credentials via environment.    ║
# ║                                                                              ║
# ╚══════════════════════════════════════════════════════════════════════════════╝

#Requires -Version 5.1

<#
.SYNOPSIS
Deploy all SQL migration scripts to the target database.

.DESCRIPTION
Executes all SQL files from the sql/ directory in alphabetical order.
Credentials and connection info must be provided via environment variables.

.PARAMETER Server
SQL Server instance. Defaults to $env:TILSOFTAI_SQL_SERVER or '.'.

.PARAMETER Database
Target database name. Defaults to $env:TILSOFTAI_SQL_DATABASE or 'TILSOFTAI'.

.PARAMETER UseIntegratedSecurity
Use Windows Authentication instead of SQL auth.

.EXAMPLE
# Windows Authentication (recommended)
.\deploy-sql.ps1 -UseIntegratedSecurity

# SQL Authentication via environment
$env:TILSOFTAI_SQL_SERVER = "localhost"
$env:TILSOFTAI_SQL_DATABASE = "TILSOFTAI"
$env:TILSOFTAI_SQL_USER = "deploy_user"
$env:TILSOFTAI_SQL_PASSWORD = "***"
.\deploy-sql.ps1
#>

[CmdletBinding()]
param(
    [string]$Server = $env:TILSOFTAI_SQL_SERVER,
    [string]$Database = $env:TILSOFTAI_SQL_DATABASE,
    [switch]$UseIntegratedSecurity
)

$ErrorActionPreference = "Stop"

# Resolve paths relative to script location
$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) { $scriptRoot = Get-Location }
$sqlDir = Join-Path $scriptRoot "sql"

# Validate SQL directory exists
if (-not (Test-Path $sqlDir)) {
    Write-Error "SQL directory not found: $sqlDir"
    exit 1
}

# Default values if not provided
if (-not $Server) { $Server = "." }
if (-not $Database) { $Database = "TILSOFTAI" }

Write-Host "╔══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    TILSOFTAI Database Schema Deployment                       ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server:   $Server" -ForegroundColor Yellow
Write-Host "Database: $Database" -ForegroundColor Yellow
Write-Host "Auth:     $(if ($UseIntegratedSecurity) { 'Windows Integrated' } else { 'SQL Authentication' })" -ForegroundColor Yellow
Write-Host ""

# Build connection arguments
$sqlcmdArgs = @("-S", $Server, "-d", $Database, "-b")

if ($UseIntegratedSecurity) {
    $sqlcmdArgs += "-E"
} else {
    $userId = $env:TILSOFTAI_SQL_USER
    $password = $env:TILSOFTAI_SQL_PASSWORD
    
    if (-not $userId -or -not $password) {
        Write-Error @"
SQL credentials not configured. Please set environment variables:
  TILSOFTAI_SQL_USER     - SQL Server username
  TILSOFTAI_SQL_PASSWORD - SQL Server password

Or use -UseIntegratedSecurity for Windows Authentication.
"@
        exit 1
    }
    
    $sqlcmdArgs += @("-U", $userId, "-P", $password)
}

# Get all SQL files in order
$sqlFiles = Get-ChildItem -Path $sqlDir -Filter "*.sql" -Recurse | Sort-Object FullName

Write-Host "Found $($sqlFiles.Count) SQL files" -ForegroundColor Cyan
Write-Host ""

$successCount = 0
$errorCount = 0
$failedFiles = @()

foreach ($file in $sqlFiles) {
    $relativePath = $file.FullName.Substring($sqlDir.Length + 1)
    Write-Host "Executing: $relativePath" -ForegroundColor Yellow -NoNewline
    
    try {
        $fileArgs = $sqlcmdArgs + @("-i", $file.FullName)
        & sqlcmd @fileArgs 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host " ✓" -ForegroundColor Green
            $successCount++
        }
        else {
            Write-Host " ✗ (exit: $LASTEXITCODE)" -ForegroundColor Red
            $errorCount++
            $failedFiles += $relativePath
        }
    }
    catch {
        Write-Host " ✗ Error: $_" -ForegroundColor Red
        $errorCount++
        $failedFiles += $relativePath
    }
}

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Deployment Complete" -ForegroundColor Green
Write-Host "  Success: $successCount" -ForegroundColor Green
Write-Host "  Errors:  $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan

if ($errorCount -gt 0) {
    Write-Host ""
    Write-Host "Failed scripts:" -ForegroundColor Red
    foreach ($f in $failedFiles) {
        Write-Host "  - $f" -ForegroundColor Red
    }
    exit 1
}
