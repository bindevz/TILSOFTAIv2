$body = @{
    model    = "gpt-4"
    messages = @(
        @{
            role    = "user"
            content = "how many model in season 24/25"
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "Request body:" -ForegroundColor Cyan
Write-Host $body
Write-Host "`nSending query to TILSOFTAI API..." -ForegroundColor Cyan

try {
    $response = Invoke-WebRequest -Uri 'http://localhost:5218/v1/chat/completions' -Method Post -Body $body -ContentType 'application/json'
    
    Write-Host "`n==== SUCCESS ====" -ForegroundColor Green
    Write-Host $response.Content
}
catch {
    Write-Host "`n==== ERROR ====" -ForegroundColor Red
    Write-Host "Status Code:" $_.Exception.Response.StatusCode.value__ -ForegroundColor Red
    Write-Host "Status Description:" $_.Exception.Response.StatusDescription -ForegroundColor Red
    
    if ($_.ErrorDetails) {
        Write-Host "`nError Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message
    }
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        $responseBody = $reader.ReadToEnd()
        Write-Host "`nResponse Body:" -ForegroundColor Yellow
        Write-Host $responseBody
    }
}
