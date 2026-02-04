# Fix SQL reserved keyword rowCount in all affected scripts
$files = @(
    "sql\02_modules\model\003_sps_model.sql",
    "sql\03_actions\003_sps_ai_action.sql",
    "sql\02_modules\model_enterprise\002_sps_model_enterprise.sql",
    "sql\90_template_module\002_sps_ai_template.sql"
)

foreach ($file in $files) {
    $content = Get-Content $file -Raw
    # Replace "AS rowCount" with "AS [rowCount]"
    $content = $content -replace ' AS rowCount([,\s])', ' AS [rowCount]$1'
    Set-Content -Path $file -Value $content -NoNewline
    Write-Host "Fixed: $file" -ForegroundColor Green
}

Write-Host "`nAll rowCount fixes applied!" -ForegroundColor Cyan
