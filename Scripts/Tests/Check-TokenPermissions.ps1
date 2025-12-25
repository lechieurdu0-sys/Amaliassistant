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

Write-Host "Vérification des permissions du token..." -ForegroundColor Cyan

try {
    # Vérifier les permissions du token
    $response = Invoke-RestMethod -Uri "https://api.github.com/user" -Headers $headers
    Write-Host "✓ Token valide" -ForegroundColor Green
    Write-Host "  Utilisateur: $($response.login)" -ForegroundColor White
    
    # Vérifier les permissions sur le dépôt
    $repoResponse = Invoke-RestMethod -Uri "https://api.github.com/repos/lechieurdu0-sys/Amaliassistant" -Headers $headers
    Write-Host "  Dépôt: $($repoResponse.full_name)" -ForegroundColor White
    
    # Essayer de vérifier les permissions
    Write-Host "`nLe token peut lire mais pas écrire." -ForegroundColor Yellow
    Write-Host "Il faut recréer le token avec la permission 'repo' complète." -ForegroundColor Yellow
    Write-Host "`nÉtapes:" -ForegroundColor Cyan
    Write-Host "1. Allez sur https://github.com/settings/tokens" -ForegroundColor White
    Write-Host "2. Supprimez l'ancien token ou créez-en un nouveau" -ForegroundColor White
    Write-Host "3. Cochez TOUTES les cases sous 'repo' (pas seulement 'public_repo')" -ForegroundColor White
    Write-Host "4. Ou utilisez 'repo' qui donne tous les droits" -ForegroundColor White
}
catch {
    Write-Host "✗ Erreur: $($_.Exception.Message)" -ForegroundColor Red
}






