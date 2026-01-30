$ErrorActionPreference = "Stop"

$patterns = @(
    '(^|/)\.vs/',
    '(^|/)(bin|obj)/',
    '(^|/)TestResults/'
)

$files = & git ls-files
if ($LASTEXITCODE -ne 0) {
    Write-Error "git ls-files failed; ensure git is available in PATH."
    exit 1
}

$bad = @()
foreach ($file in $files) {
    $path = $file -replace '\\', '/'
    foreach ($pattern in $patterns) {
        if ($path -match $pattern) {
            $bad += $file
            break
        }
    }
}

if ($bad.Count -gt 0) {
    Write-Host "Tracked build artifacts found:" -ForegroundColor Red
    $bad | ForEach-Object { Write-Host $_ }
    exit 1
}

Write-Host "Repo clean: no tracked build artifacts found."
