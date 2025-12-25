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

Write-Host "Vérification de la release v1.0.0.2..." -ForegroundColor Cyan

try {
    $releases = Invoke-RestMethod -Uri "https://api.github.com/repos/lechieurdu0-sys/Amaliassistant/releases" -Headers $headers
    $release = $releases | Where-Object {$_.tag_name -eq "v1.0.0.2"}
    
    if ($release) {
        Write-Host "✓ Release v1.0.0.2 trouvée!" -ForegroundColor Green
        Write-Host "  URL: $($release.html_url)" -ForegroundColor Cyan
        Write-Host "  Nombre d'assets: $($release.assets.Count)" -ForegroundColor White
        
        if ($release.assets.Count -gt 0) {
            Write-Host "`n  Fichiers uploadés:" -ForegroundColor Yellow
            foreach ($asset in $release.assets) {
                $sizeMB = [math]::Round($asset.size / 1MB, 2)
                Write-Host "    - $($asset.name) ($sizeMB MB)" -ForegroundColor White
            }
        } else {
            Write-Host "  ⚠ Aucun fichier uploadé pour le moment" -ForegroundColor Yellow
        }
    } else {
        Write-Host "✗ Release v1.0.0.2 non trouvée" -ForegroundColor Red
    }
}
catch {
    Write-Host "✗ Erreur: $($_.Exception.Message)" -ForegroundColor Red
}






