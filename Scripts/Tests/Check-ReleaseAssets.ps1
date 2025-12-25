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

Write-Host "Vérification des assets de la release v1.0.0.4..." -ForegroundColor Cyan

try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/lechieurdu0-sys/Amaliassistant/releases/tags/v1.0.0.4" -Headers $headers
    Write-Host "Release trouvée: $($release.tag_name)" -ForegroundColor Green
    Write-Host "Nombre d'assets: $($release.assets.Count)" -ForegroundColor White
    Write-Host ""
    
    if ($release.assets.Count -gt 0) {
        Write-Host "Assets disponibles:" -ForegroundColor Yellow
        foreach ($asset in $release.assets) {
            $sizeMB = [math]::Round($asset.size / 1MB, 2)
            Write-Host "  - $($asset.name) ($sizeMB MB)" -ForegroundColor White
            Write-Host "    URL: $($asset.browser_download_url)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "Erreur: $($_.Exception.Message)" -ForegroundColor Red
}






