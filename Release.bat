@echo off
REM Script batch simple pour lancer la release complète
REM Lance le script PowerShell Release-Full.ps1

echo ========================================
echo   RELEASE - Amaliassistant
echo ========================================
echo.

REM Vérifier que PowerShell est disponible
powershell -Command "Get-Host" >nul 2>&1
if errorlevel 1 (
    echo ERREUR: PowerShell n'est pas disponible sur ce systeme.
    pause
    exit /b 1
)

REM Lancer le script PowerShell
powershell -ExecutionPolicy Bypass -File "%~dp0Release-Full.ps1" %*

if errorlevel 1 (
    echo.
    echo ERREUR lors de la release.
    pause
    exit /b 1
)

pause




