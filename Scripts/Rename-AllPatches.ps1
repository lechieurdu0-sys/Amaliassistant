# Script pour renommer tous les patches selon la logique correcte
# Format: Amaliassistant_Patch_X.Y.Z.W_to_A.B.C.D.zip
# où X.Y.Z.W est la version PRECEDENTE et A.B.C.D est la version NOUVELLE

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RENOMMAGE DES PATCHES" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$rootPath = "D:\Users\lechi\Desktop\Amaliassistant 2.0"
$patchesDir = Join-Path $rootPath "Patches"

if (-not (Test-Path $patchesDir)) {
    Write-Host "ERREUR: Dossier Patches introuvable" -ForegroundColor Red
    exit 1
}

$allPatches = Get-ChildItem -Path $patchesDir -Filter "Amaliassistant_Patch_*.zip"

Write-Host "Patches trouvés: $($allPatches.Count)" -ForegroundColor Cyan
Write-Host ""

$renamedCount = 0
$skippedCount = 0

foreach ($patch in $allPatches) {
    $currentName = $patch.Name
    
    # Extraire les versions du nom actuel
    if ($currentName -match 'Amaliassistant_Patch_(\d+\.\d+\.\d+\.\d+)_to_(\d+\.\d+\.\d+\.\d+)\.zip') {
        $fromVersion = $matches[1]
        $toVersion = $matches[2]
        
        # Vérifier si le nom est déjà correct (fromVersion < toVersion)
        $fromParts = $fromVersion -split '\.'
        $toParts = $toVersion -split '\.'
        
        $isCorrect = $false
        for ($i = 0; $i -lt 4; $i++) {
            if ([int]$toParts[$i] -gt [int]$fromParts[$i]) {
                $isCorrect = $true
                break
            } elseif ([int]$toParts[$i] -lt [int]$fromParts[$i]) {
                break
            }
        }
        
        if ($isCorrect -or ($fromVersion -eq $toVersion -and $fromVersion -ne $toVersion)) {
            # Le patch est déjà correct ou a une logique valide
            Write-Host "  OK - $currentName (déjà correct)" -ForegroundColor Green
            $skippedCount++
        } else {
            # Le patch a un nom incorrect (ex: 1.0.0.11_to_1.0.0.11 ou 1.0.0.10_to_1.0.0.9)
            # Pour les patches avec fromVersion == toVersion, on doit trouver la vraie version précédente
            
            if ($fromVersion -eq $toVersion) {
                Write-Host "  ATTENTION - $currentName (versions identiques)" -ForegroundColor Yellow
                Write-Host "    Version actuelle: $toVersion" -ForegroundColor Cyan
                
                # Logique séquentielle : trouver le patch qui se termine par cette version
                # La version précédente est la version cible du patch précédent
                $possiblePrevious = $null
                
                # Chercher tous les patches qui se terminent par une version inférieure
                $allPatchesSorted = $allPatches | Sort-Object { 
                    if ($_.Name -match 'Amaliassistant_Patch_(\d+\.\d+\.\d+\.\d+)_to_(\d+\.\d+\.\d+\.\d+)\.zip') {
                        $to = $matches[2]
                        $toParts = $to -split '\.'
                        [int]$toParts[0] * 1000000 + [int]$toParts[1] * 10000 + [int]$toParts[2] * 100 + [int]$toParts[3]
                    } else { 0 }
                }
                
                # Trouver le patch qui se termine juste avant cette version
                foreach ($other in $allPatchesSorted) {
                    if ($other.Name -match 'Amaliassistant_Patch_(\d+\.\d+\.\d+\.\d+)_to_(\d+\.\d+\.\d+\.\d+)\.zip') {
                        $otherTo = $matches[2]
                        $otherToParts = $otherTo -split '\.'
                        $toParts = $toVersion -split '\.'
                        
                        # Comparer les versions
                        $isBefore = $false
                        for ($i = 0; $i -lt 4; $i++) {
                            if ([int]$otherToParts[$i] -lt [int]$toParts[$i]) {
                                $isBefore = $true
                                break
                            } elseif ([int]$otherToParts[$i] -gt [int]$toParts[$i]) {
                                break
                            }
                        }
                        
                        if ($isBefore) {
                            $possiblePrevious = $otherTo
                        }
                    }
                }
                
                # Si on n'a pas trouvé, utiliser la version précédente logique (révision - 1)
                if (-not $possiblePrevious) {
                    $toParts = $toVersion -split '\.'
                    $revision = [int]$toParts[3]
                    if ($revision -gt 0) {
                        $possiblePrevious = "$($toParts[0]).$($toParts[1]).$($toParts[2]).$($revision - 1)"
                    } else {
                        $build = [int]$toParts[2]
                        if ($build -gt 0) {
                            $possiblePrevious = "$($toParts[0]).$($toParts[1]).$($build - 1).9"
                        }
                    }
                }
                
                if ($possiblePrevious) {
                    $newName = "Amaliassistant_Patch_${possiblePrevious}_to_${toVersion}.zip"
                    $newPath = Join-Path $patchesDir $newName
                    
                    if (-not (Test-Path $newPath)) {
                        Write-Host "    Version précédente déterminée: $possiblePrevious" -ForegroundColor Cyan
                        Write-Host "    Renommage en: $newName" -ForegroundColor Cyan
                        Rename-Item -Path $patch.FullName -NewName $newName
                        Write-Host "    OK - Renommé" -ForegroundColor Green
                        $renamedCount++
                    } else {
                        Write-Host "    ERREUR: Le fichier $newName existe déjà" -ForegroundColor Red
                    }
                } else {
                    Write-Host "    Impossible de déterminer la version précédente" -ForegroundColor Red
                    Write-Host "    Ce patch doit être corrigé manuellement" -ForegroundColor Yellow
                }
            } else {
                # fromVersion > toVersion (illogique)
                Write-Host "  ERREUR - $currentName (versions inversées)" -ForegroundColor Red
                Write-Host "    Ce patch doit être corrigé manuellement" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "  ERREUR - $currentName (format invalide)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RENOMMAGE TERMINE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Résumé:" -ForegroundColor Yellow
Write-Host "  Patches renommés: $renamedCount" -ForegroundColor Green
Write-Host "  Patches déjà corrects: $skippedCount" -ForegroundColor Cyan
Write-Host ""

