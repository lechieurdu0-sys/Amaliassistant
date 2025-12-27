@echo off
REM Script batch pour extraire le patch et redémarrer l'application
REM Paramètres: %1 = chemin du patch ZIP, %2 = dossier de destination, %3 = chemin de l'exe

setlocal enabledelayedexpansion

set PATCH_FILE=%~1
set DEST_DIR=%~2
set EXE_PATH=%~3

REM Vérifier que les paramètres sont fournis
if "%PATCH_FILE%"=="" (
    echo Erreur: Le fichier patch n'est pas specifie
    pause
    exit /b 1
)

if "%DEST_DIR%"=="" (
    echo Erreur: Le dossier de destination n'est pas specifie
    pause
    exit /b 1
)

if "%EXE_PATH%"=="" (
    echo Erreur: Le chemin de l'executable n'est pas specifie
    pause
    exit /b 1
)

echo ========================================
echo Mise a jour d'Amaliassistant
echo ========================================
echo.
echo Attente de la fermeture de l'application...

REM Attendre que l'application se ferme (GameOverlay.App.exe)
:WAIT_LOOP
timeout /t 1 /nobreak >nul 2>&1
tasklist /FI "IMAGENAME eq GameOverlay.App.exe" 2>NUL | find /I /N "GameOverlay.App.exe">NUL
if "%ERRORLEVEL%"=="0" (
    goto WAIT_LOOP
)

REM Attendre un court instant supplémentaire pour être sûr
timeout /t 2 /nobreak >nul 2>&1

echo Extraction du patch en cours...
echo.

REM Extraire le patch avec PowerShell (méthode la plus fiable)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& {try { Expand-Archive -Path '%PATCH_FILE%' -DestinationPath '%DEST_DIR%' -Force -ErrorAction Stop; Write-Host 'Patch extrait avec succes' } catch { Write-Host \"Erreur lors de l'extraction: $_\"; exit 1 }}"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERREUR: Impossible d'extraire le patch
    echo Fichier: %PATCH_FILE%
    echo Destination: %DEST_DIR%
    pause
    exit /b 1
)

REM Supprimer le fichier temporaire du patch
if exist "%PATCH_FILE%" (
    del /F /Q "%PATCH_FILE%" >nul 2>&1
)

echo.
echo Redemarrage de l'application...

REM Vérifier que l'exe existe
if not exist "%EXE_PATH%" (
    echo ERREUR: Le fichier executable est introuvable: %EXE_PATH%
    pause
    exit /b 1
)

REM Lancer l'application
start "" "%EXE_PATH%"

echo.
echo Mise a jour terminee avec succes!
timeout /t 2 /nobreak >nul 2>&1

endlocal
exit /b 0










