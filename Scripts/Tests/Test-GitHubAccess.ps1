# Récupérer le token GitHub de manière sécurisée
$token = & "$PSScriptRoot\..\Get-GitHubToken.ps1" -RequireToken
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "ERREUR: Token GitHub requis" -ForegroundColor Red
    exit 1
}
$headers = @{
    "Authorization" = "token $token"
    "Accept" = "application/vnd.github.v3+json"
}

Write-Host "Test d'accès au dépôt GitHub..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "https://api.github.com/repos/lechieurdu0-sys/Amaliassistant" -Headers $headers
    Write-Host "✓ Accès au dépôt: OK" -ForegroundColor Green
    Write-Host "  Nom: $($response.full_name)" -ForegroundColor White
    Write-Host "  Privé: $($response.private)" -ForegroundColor White
    Write-Host "  URL: $($response.html_url)" -ForegroundColor White
}
catch {
    Write-Host "✗ Erreur d'accès" -ForegroundColor Red
    Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode
        Write-Host "  Code: $statusCode" -ForegroundColor Red
        
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "  Détails: $responseBody" -ForegroundColor Red
        }
        catch {
            Write-Host "  Impossible de lire les détails de l'erreur" -ForegroundColor Yellow
        }
    }
}






