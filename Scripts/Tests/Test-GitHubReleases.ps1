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

Write-Host "Test d'accès aux releases GitHub..." -ForegroundColor Cyan

# Tester la lecture des releases
try {
    $releases = Invoke-RestMethod -Uri "https://api.github.com/repos/lechieurdu0-sys/Amaliassistant/releases" -Headers $headers
    Write-Host "✓ Lecture des releases: OK" -ForegroundColor Green
    Write-Host "  Nombre de releases: $($releases.Count)" -ForegroundColor White
}
catch {
    Write-Host "✗ Erreur de lecture des releases" -ForegroundColor Red
    Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Red
}

# Tester la création d'une release (sans vraiment la créer)
Write-Host "`nTest de création de release..." -ForegroundColor Cyan
$testBody = @{
    tag_name = "v1.0.0.2-test"
    name = "Test Release"
    body = "Test"
    draft = $true
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "https://api.github.com/repos/lechieurdu0-sys/Amaliassistant/releases" -Method Post -Headers $headers -Body $testBody -ContentType "application/json"
    Write-Host "✓ Création de release: OK" -ForegroundColor Green
    Write-Host "  Release ID: $($response.id)" -ForegroundColor White
    
    # Supprimer la release de test
    Write-Host "`nSuppression de la release de test..." -ForegroundColor Yellow
    Invoke-RestMethod -Uri "https://api.github.com/repos/lechieurdu0-sys/Amaliassistant/releases/$($response.id)" -Method Delete -Headers $headers
    Write-Host "✓ Release de test supprimée" -ForegroundColor Green
}
catch {
    Write-Host "✗ Erreur de création de release" -ForegroundColor Red
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
            Write-Host "  Impossible de lire les détails" -ForegroundColor Yellow
        }
    }
}






