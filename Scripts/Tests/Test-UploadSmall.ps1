# Test d'upload du petit fichier update.xml
# Récupérer le token GitHub de manière sécurisée
$token = & "$PSScriptRoot\..\Get-GitHubToken.ps1" -RequireToken
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "ERREUR: Token GitHub requis" -ForegroundColor Red
    exit 1
}
$version = "1.0.0.2"
$tagName = "v$version"

$headers = @{
    "Authorization" = "token $token"
    "Accept" = "application/vnd.github.v3+json"
}

Write-Host "Test d'upload de update.xml..." -ForegroundColor Cyan

# Récupérer la release
try {
    $releaseUrl = "https://api.github.com/repos/lechieurdu0-sys/Amaliassistant/releases/tags/$tagName"
    $release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers
    Write-Host "✓ Release trouvée (ID: $($release.id))" -ForegroundColor Green
}
catch {
    Write-Host "✗ Erreur: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Upload de update.xml
$updateXmlPath = "update.xml"
if (Test-Path $updateXmlPath) {
    $fileSize = (Get-Item $updateXmlPath).Length
    Write-Host "Taille de update.xml: $fileSize octets" -ForegroundColor Cyan
    
    try {
        $uploadUrl = "https://uploads.github.com/repos/lechieurdu0-sys/Amaliassistant/releases/$($release.id)/assets?name=update.xml"
        $fileBytes = [System.IO.File]::ReadAllBytes($updateXmlPath)
        
        Write-Host "Upload en cours..." -ForegroundColor Yellow
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/xml"
        Write-Host "✓ update.xml uploadé avec succès!" -ForegroundColor Green
        Write-Host "  URL: $($uploadResponse.browser_download_url)" -ForegroundColor Cyan
    }
    catch {
        Write-Host "✗ Erreur lors de l'upload: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode
            Write-Host "  Code HTTP: $statusCode" -ForegroundColor Red
        }
    }
} else {
    Write-Host "✗ Fichier update.xml introuvable" -ForegroundColor Red
}






