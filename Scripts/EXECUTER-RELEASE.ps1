# Script pour exécuter Create-Release.ps1 avec toutes les notes
# Remplacez "VOTRE_TOKEN" par votre token GitHub

# Aller dans le bon répertoire
Set-Location "D:\Users\lechi\Desktop\Amaliassistant 2.0"

# Préparer les notes de release
$releaseNotes = @"
## Version 1.0.0.10

### Corrections
- Correction du système de notifications de ventes en temps réel
- Amélioration de la détection des ventes (FileSystemWatcher optimisé)
- Correction de la persistance des paramètres lors des mises à jour
- Correction de la version (1.0.0.12 -> 1.0.0.10)

### Améliorations
- Nouveau système de logging catégorisé (AdvancedLogger)
- Rotation automatique des logs (3 fichiers max, 1 MB chacun)
- Archivage automatique des logs en ZIP
- Gestion automatique de l'espace disque (suppression des archives > 1 GB)
- Validation automatique des URLs de patch
- Script automatisé pour la création de releases GitHub

### Technique
- Amélioration de la robustesse du SaleTracker
- Retry mechanism avec exponential backoff
- Buffer FileSystemWatcher augmenté à 64 KB
- Système de backup automatique de la configuration
- Correction de la logique de création des patches
"@

# Récupérer le token GitHub de manière sécurisée
$gitHubToken = & "$PSScriptRoot\Get-GitHubToken.ps1" -RequireToken
if ([string]::IsNullOrWhiteSpace($gitHubToken)) {
    Write-Host "ERREUR: Token GitHub requis" -ForegroundColor Red
    exit 1
}

# Exécuter le script
& "$PSScriptRoot\Create-Release.ps1" -Version "1.0.0.10" -GitHubToken $gitHubToken -ReleaseNotes $releaseNotes

