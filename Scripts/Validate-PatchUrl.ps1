# Script pour valider que l'URL du patch est correcte et que le fichier existe sur GitHub
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$Repository = "lechieurdu0-sys/Amaliassistant",
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubToken = ""
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   VALIDATION DE L'URL DU PATCH" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Lire update.xml
$updateXmlPath = Join-Path $PSScriptRoot "update.xml"
if (-not (Test-Path $updateXmlPath)) {
    Write-Host "ERREUR: update.xml introuvable" -ForegroundColor Red
    exit 1
}

$updateXml = [xml](Get-Content $updateXmlPath)
$patchUrl = $updateXml.item.patch_url

if ([string]::IsNullOrWhiteSpace($patchUrl)) {
    Write-Host "Aucun patch_url dans update.xml" -ForegroundColor Yellow
    Write-Host "C'est normal si aucun patch n'a été créé pour cette version" -ForegroundColor Yellow
    exit 0
}

Write-Host "URL du patch dans update.xml:" -ForegroundColor Cyan
Write-Host "  $patchUrl" -ForegroundColor White
Write-Host ""

# Vérifier le format de l'URL
$expectedPattern = "https://github.com/$Repository/releases/download/v$Version/Amaliassistant_Patch_.*_to_$Version\.zip"
if ($patchUrl -notmatch $expectedPattern) {
    Write-Host "ATTENTION: Le format de l'URL ne correspond pas au pattern attendu" -ForegroundColor Yellow
    Write-Host "Pattern attendu: https://github.com/$Repository/releases/download/v$Version/Amaliassistant_Patch_*_to_$Version.zip" -ForegroundColor White
    Write-Host ""
}

# Extraire le nom du fichier
$fileName = $patchUrl -replace '.*/', ''
Write-Host "Nom du fichier: $fileName" -ForegroundColor Cyan

# Vérifier que le fichier existe localement
$patchDir = Join-Path $PSScriptRoot "Patches"
$localPatchFile = Join-Path $patchDir $fileName

if (Test-Path $localPatchFile) {
    $fileSize = (Get-Item $localPatchFile).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
    Write-Host "  OK - Fichier trouvé localement: $fileSizeMB MB" -ForegroundColor Green
} else {
    Write-Host "  ATTENTION: Fichier patch introuvable localement dans Patches\" -ForegroundColor Yellow
    Write-Host "  Chemin attendu: $localPatchFile" -ForegroundColor White
}

Write-Host ""

# Vérifier sur GitHub si un token est fourni
if (-not [string]::IsNullOrEmpty($GitHubToken)) {
    Write-Host "Vérification sur GitHub..." -ForegroundColor Cyan
    
    try {
        $headers = @{
            "Authorization" = "token $GitHubToken"
            "Accept" = "application/vnd.github.v3+json"
        }
        
        $tagName = "v$Version"
        $releaseUrl = "https://api.github.com/repos/$Repository/releases/tags/$tagName"
        
        try {
            $release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers
            Write-Host "  OK - Release trouvée sur GitHub" -ForegroundColor Green
            
            # Chercher le fichier patch dans les assets
            $patchAsset = $release.assets | Where-Object { $_.name -eq $fileName }
            
            if ($patchAsset) {
                $assetSizeMB = [math]::Round($patchAsset.size / 1MB, 2)
                Write-Host "  OK - Patch trouvé sur GitHub: $assetSizeMB MB" -ForegroundColor Green
                Write-Host "  URL de téléchargement: $($patchAsset.browser_download_url)" -ForegroundColor Cyan
                
                # Comparer avec l'URL dans update.xml
                if ($patchAsset.browser_download_url -eq $patchUrl) {
                    Write-Host "  OK - URL correspond exactement" -ForegroundColor Green
                } else {
                    Write-Host "  ATTENTION: URL différente de celle dans update.xml" -ForegroundColor Yellow
                    Write-Host "    Dans update.xml: $patchUrl" -ForegroundColor White
                    Write-Host "    Sur GitHub: $($patchAsset.browser_download_url)" -ForegroundColor White
                }
            } else {
                Write-Host "  ERREUR: Patch non trouvé dans les assets de la release" -ForegroundColor Red
                Write-Host "  Fichiers disponibles:" -ForegroundColor Yellow
                foreach ($asset in $release.assets) {
                    Write-Host "    - $($asset.name) ($([math]::Round($asset.size / 1MB, 2)) MB)" -ForegroundColor White
                }
            }
        } catch {
            Write-Host "  ERREUR: Release $tagName non trouvée sur GitHub" -ForegroundColor Red
            Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ERREUR lors de la vérification GitHub: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "Token GitHub non fourni, vérification GitHub ignorée" -ForegroundColor Yellow
    Write-Host "Utilisez -GitHubToken pour valider sur GitHub" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   VALIDATION TERMINEE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

