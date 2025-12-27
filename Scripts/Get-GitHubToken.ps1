# Script helper pour récupérer le token GitHub de manière sécurisée
# Priorité : Variable d'environnement > Fichier local > Demande interactive

param(
    [Parameter(Mandatory=$false)]
    [switch]$RequireToken
)

$ErrorActionPreference = "Stop"

# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Méthode 1: Variable d'environnement (recommandé)
$token = $env:GITHUB_TOKEN

if ([string]::IsNullOrWhiteSpace($token)) {
    # Méthode 2: Fichier local (TokenGitHub.txt)
    $tokenFile = Join-Path $rootPath "TokenGitHub.txt"
    if (Test-Path $tokenFile) {
        try {
            $token = Get-Content $tokenFile -Raw | ForEach-Object { $_.Trim() }
            if ([string]::IsNullOrWhiteSpace($token)) {
                $token = $null
            }
        } catch {
            Write-Warning "Impossible de lire le fichier TokenGitHub.txt: $($_.Exception.Message)"
        }
    }
}

# Méthode 3: Demande interactive si nécessaire
if ([string]::IsNullOrWhiteSpace($token)) {
    if ($RequireToken) {
        Write-Host ""
        Write-Host "Token GitHub requis" -ForegroundColor Yellow
        Write-Host "Configurez-le via une des méthodes suivantes :" -ForegroundColor Cyan
        Write-Host "  1. Variable d'environnement: `$env:GITHUB_TOKEN" -ForegroundColor White
        Write-Host "  2. Fichier local: TokenGitHub.txt (à la racine du projet)" -ForegroundColor White
        Write-Host ""
        $token = Read-Host "Ou entrez le token maintenant (sera utilisé uniquement pour cette session)"
    } else {
        Write-Warning "Aucun token GitHub trouvé. Configurez GITHUB_TOKEN ou créez TokenGitHub.txt"
        return $null
    }
}

if ([string]::IsNullOrWhiteSpace($token)) {
    return $null
}

return $token.Trim()







