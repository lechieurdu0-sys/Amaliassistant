# Test du token GitHub
# Récupérer le token GitHub de manière sécurisée
$token = & "$PSScriptRoot\Get-GitHubToken.ps1" -RequireToken
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "ERREUR: Token GitHub requis" -ForegroundColor Red
    exit 1
}
$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

Write-Host "Test du token GitHub..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "https://api.github.com/user" -Headers $headers
    Write-Host "OK - Token valide" -ForegroundColor Green
    Write-Host "Utilisateur: $($response.login)" -ForegroundColor Cyan
} catch {
    Write-Host "ERREUR: Token invalide ou permissions insuffisantes" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Yellow
}







