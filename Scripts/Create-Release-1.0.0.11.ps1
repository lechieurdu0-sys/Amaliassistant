# Script pour créer la release GitHub 1.0.0.11
# Exécute Create-Release.ps1 avec les notes appropriées

# Aller dans le bon répertoire
Set-Location "D:\Users\lechi\Desktop\Amaliassistant 2.0"

# Préparer les notes de release pour la version 1.0.0.11
$releaseNotes = @"
## Version 1.0.0.11

### Améliorations
- Optimisation du système de mise à jour
- MessageBox de mise à jour affiché rapidement (< 1 seconde)
- Mise à jour silencieuse après confirmation utilisateur
- Redémarrage automatique après la mise à jour

### Optimisations
- Délai initial de vérification réduit : 3s → 0.5s
- Timeout pour update.xml : 5s (au lieu de 5 minutes)
- Suppression des Dispatcher.Invoke inutiles
- Mode silencieux pour l'installateur complet

### Technique
- Amélioration de la réactivité du système de mise à jour
- Gestion optimisée des timeouts réseau
- Processus de mise à jour non-bloquant
"@

# Récupérer le token GitHub de manière sécurisée
$gitHubToken = & "$PSScriptRoot\Get-GitHubToken.ps1" -RequireToken
if ([string]::IsNullOrWhiteSpace($gitHubToken)) {
    Write-Host "ERREUR: Token GitHub requis" -ForegroundColor Red
    exit 1
}

# Exécuter le script
& "$PSScriptRoot\Create-Release.ps1" -Version "1.0.0.11" -GitHubToken $gitHubToken -ReleaseNotes $releaseNotes







