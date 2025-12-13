# Script pour surveiller wakfu_chat.log et afficher les messages de vente

$logFilePath = "C:\SteamLibrary\steamapps\common\Wakfu\preferences\logs\wakfu_chat.log"

# Patterns regex similaires à ceux dans SaleNotificationService.cs
# Pattern 1: "Vous avez vendu X objets pour un prix total de Y§ pendant votre absence"
$pattern1 = [regex]'Vous\s+avez\s+vendu\s+(\d+)\s+(?:objets?|items?)\s+pour\s+(?:un\s+)?(?:prix\s+)?total\s+de\s+(\d+(?:\s+\d+)*)\s*§'
# Pattern 2: "Vous avez vendu X items pour un total de Y kamas"
$pattern2 = [regex]'Vous\s+avez\s+vendu\s+(\d+)\s+(?:objets?|items?)\s+(?:pour\s+un\s+total\s+de\s+)?(\d+(?:\s+\d+)*)\s+(?:§|kamas?)'
# Pattern 3: "X items vendus pour Y kamas" ou "X objets vendus pour Y§"
$pattern3 = [regex]'(\d+)\s+(?:objets?|items?)\s+vendus?\s+(?:pour\s+)?(?:un\s+)?(?:prix\s+)?total\s+de\s+?(\d+(?:\s+\d+)*)\s*(?:§|kamas?)'
# Pattern 4: "X items vendus. Total : Y kamas" ou "X items vendus, total Y kamas"
$pattern4 = [regex]'(\d+)\s+(?:objets?|items?)\s+vendus?[.,]\s*(?:Total\s*[:]?\s*)?(\d+(?:\s+\d+)*)\s*(?:§|kamas?)'
# Pattern 5: Recherche séparée des nombres (objets/items et kamas/§ dans n'importe quel ordre)
$pattern5 = [regex]'(\d+)\s+(?:objets?|items?).*?(\d+(?:\s+\d+)*)\s*(?:§|kamas?)|(\d+(?:\s+\d+)*)\s*(?:§|kamas?).*?(\d+)\s+(?:objets?|items?)'

$patterns = @($pattern1, $pattern2, $pattern3, $pattern4, $pattern5)

function ParseSaleInfo($line) {
    # Nettoyer la ligne (enlever les préfixes timestamp et [Information (jeu)])
    $cleanLine = $line
    if ($line -match "\[Information \(jeu\)\] (.+)") {
        $cleanLine = $matches[1].Trim()
    } elseif ($line -match "\] (.+)") {
        $cleanLine = $matches[1].Trim()
    }
    
    Write-Host "[DEBUG] Ligne nettoyee: $cleanLine" -ForegroundColor DarkGray
    
    # Essayer chaque pattern
    $patternIndex = 0
    foreach ($pattern in $patterns) {
        $match = $pattern.Match($cleanLine)
        Write-Host "[DEBUG] Pattern $patternIndex : match = $($match.Success)" -ForegroundColor DarkGray
        if ($match.Success) {
            Write-Host "[DEBUG] Pattern $patternIndex a matche! Groups.Count = $($match.Groups.Count)" -ForegroundColor DarkGray
            for ($i = 0; $i -lt $match.Groups.Count; $i++) {
                Write-Host "[DEBUG]   Group[$i] = '$($match.Groups[$i].Value)'" -ForegroundColor DarkGray
            }
            $itemCount = 0
            $totalKamas = 0
            
            # Pour les patterns avec ordre variable (pattern 4 et suivants, index 4+), les groupes peuvent être dans un ordre différent
            $patternIndex = [Array]::IndexOf($patterns, $pattern)
            if ($patternIndex -ge 4) {
                if ($match.Groups[1].Success -and $match.Groups[2].Success) {
                    # Ordre: items puis kamas
                    if ([int]::TryParse($match.Groups[1].Value, [ref]$itemCount)) {
                        $kamasStr = $match.Groups[2].Value -replace '\s', ''
                        [long]::TryParse($kamasStr, [ref]$totalKamas)
                    }
                } elseif ($match.Groups[3].Success -and $match.Groups[4].Success) {
                    # Ordre: kamas puis items
                    $kamasStr = $match.Groups[3].Value -replace '\s', ''
                    if ([long]::TryParse($kamasStr, [ref]$totalKamas)) {
                        [int]::TryParse($match.Groups[4].Value, [ref]$itemCount)
                    }
                }
            } else {
                # Patterns normaux : groupe 1 = items, groupe 2 = kamas
                if ($match.Groups.Count -ge 3) {
                    if ([int]::TryParse($match.Groups[1].Value, [ref]$itemCount)) {
                        $kamasStr = $match.Groups[2].Value
                        Write-Host "[DEBUG] Kamas string brut: '$kamasStr'" -ForegroundColor DarkGray
                        $kamasStr = $kamasStr -replace '\s', '' -replace [char]0x00A0, '' -replace [char]0x2009, '' -replace [char]0x202F, ''
                        Write-Host "[DEBUG] Kamas string nettoye: '$kamasStr'" -ForegroundColor DarkGray
                        if (-not [long]::TryParse($kamasStr, [ref]$totalKamas)) {
                            Write-Host "[DEBUG] Echec du parsing de kamas: '$kamasStr'" -ForegroundColor DarkGray
                        }
                    }
                }
            }
            
            $patternIndex++
            
            if ($itemCount -gt 0 -and $totalKamas -gt 0) {
                return @{
                    ItemCount = $itemCount
                    TotalKamas = $totalKamas
                    OriginalLine = $line
                    CleanLine = $cleanLine
                }
            }
        }
    }
    
    return $null
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SURVEILLANCE DES VENTES - wakfu_chat.log" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Fichier surveille: $logFilePath" -ForegroundColor Yellow
Write-Host ""

if (-not (Test-Path $logFilePath)) {
    Write-Host "ERREUR: Le fichier n'existe pas!" -ForegroundColor Red
    exit 1
}

# Lire la position initiale (taille du fichier)
$lastPosition = (Get-Item $logFilePath).Length
$lastReadLines = @()

Write-Host "Surveillance active... Lancez le jeu maintenant!" -ForegroundColor Green
Write-Host "Appuyez sur Ctrl+C pour arreter" -ForegroundColor Gray
Write-Host ""

while ($true) {
    try {
        if (-not (Test-Path $logFilePath)) {
            Write-Host "[ERREUR] Fichier introuvable!" -ForegroundColor Red
            Start-Sleep -Seconds 2
            continue
        }
        
        # Lire les nouvelles lignes depuis la dernière position
        # Utiliser FileShare.ReadWrite pour pouvoir lire même si le fichier est ouvert par un autre processus
        $fileStream = New-Object System.IO.FileStream(
            $logFilePath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite
        )
        $fileStream.Position = $lastPosition
        
        if ($fileStream.Position -lt $fileStream.Length) {
            $reader = New-Object System.IO.StreamReader($fileStream)
            $newLines = @()
            
            while (-not $reader.EndOfStream) {
                $line = $reader.ReadLine()
                if ($line) {
                    $newLines += $line
                    
                    # Vérifier si c'est un message de vente
                    $saleInfo = ParseSaleInfo $line
                    if ($saleInfo) {
                        Write-Host "========================================" -ForegroundColor Green
                        Write-Host "[VENTE DETECTEE!]" -ForegroundColor Green -BackgroundColor Black
                        Write-Host "========================================" -ForegroundColor Green
                        Write-Host "Ligne originale:" -ForegroundColor White
                        Write-Host "  $($saleInfo.OriginalLine)" -ForegroundColor Gray
                        Write-Host ""
                        Write-Host "Ligne nettoyee:" -ForegroundColor White
                        Write-Host "  $($saleInfo.CleanLine)" -ForegroundColor Gray
                        Write-Host ""
                        Write-Host "Items vendus: $($saleInfo.ItemCount)" -ForegroundColor Yellow
                        Write-Host "Total kamas: $($saleInfo.TotalKamas)" -ForegroundColor Yellow
                        Write-Host "========================================" -ForegroundColor Green
                        Write-Host ""
                    }
                }
            }
            
            $lastPosition = $fileStream.Position
            $reader.Close()
        }
        
        $fileStream.Close()
    }
    catch {
        Write-Host "[ERREUR] $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 500
}

