@echo off
setlocal ENABLEDELAYEDEXPANSION

REM Chemins
set ROOT=%~dp0
set APP_PROJ=%ROOT%GameOverlay.App\GameOverlay.App.csproj
set PUBLISH_DIR=%ROOT%publish
set ISCC="%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
set ISS_FILE=%ROOT%installer.iss
set INSTALLER_DIR=%ROOT%InstallerAppData

echo ========================================
echo   Construction de l'installateur
echo   Amaliassistant (AppData/Roaming)
echo ========================================
echo.

echo [1/4] Publication .NET (Release)...
if not exist "%APP_PROJ%" (
  echo ERREUR: Projet introuvable: %APP_PROJ%
  pause
  exit /b 1
)

if not exist "%PUBLISH_DIR%" mkdir "%PUBLISH_DIR%"

dotnet publish "%APP_PROJ%" -c Release -o "%PUBLISH_DIR%" --self-contained false /p:DebugType=None /p:DebugSymbols=false
if errorlevel 1 (
  echo ERREUR: Echec de la publication .NET
  pause
  exit /b 1
)
echo OK - Publication reussie
echo.

echo [1.5/4] Nettoyage du dossier publish...
REM Supprimer les fichiers de debug restants
del /Q "%PUBLISH_DIR%\*.pdb" 2>nul
REM Supprimer les fichiers obsolètes
del /Q "%PUBLISH_DIR%\GameOverlay.Video.*" 2>nul
del /Q "%PUBLISH_DIR%\GameOverlay.ZQSD.*" 2>nul
REM Supprimer les logs
if exist "%PUBLISH_DIR%\logs" rmdir /S /Q "%PUBLISH_DIR%\logs" 2>nul
REM Supprimer le dossier Release s'il existe
if exist "%PUBLISH_DIR%\Release" rmdir /S /Q "%PUBLISH_DIR%\Release" 2>nul
REM Supprimer les .deps.json inutiles (garder seulement GameOverlay.App.deps.json)
if exist "%PUBLISH_DIR%\GameOverlay.Kikimeter.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Kikimeter.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.Models.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Models.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.Themes.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Themes.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.Windows.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Windows.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.Video.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Video.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.ZQSD.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.ZQSD.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.ServerSessions.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.ServerSessions.deps.json" 2>nul
echo OK - Nettoyage termine
echo.

echo [2/4] Verification des prerequis...
set PREREQ_DIR=%ROOT%Prerequisites
if not exist "%PREREQ_DIR%" (
  echo ERREUR: Le dossier Prerequisites n'existe pas: %PREREQ_DIR%
  echo.
  echo Fichiers requis dans Prerequisites\:
  echo   - windowsdesktop-runtime-8.0.21-win-x64.exe
  echo   - windowsdesktop-runtime-8.0.21-win-x86.exe
  echo   - windowsdesktop-runtime-8.0.21-win-arm64.exe
  echo   - MicrosoftEdgeWebView2RuntimeInstallerx64.exe
  echo   - MicrosoftEdgeWebView2RuntimeInstallerx86.exe
  echo   - MicrosoftEdgeWebView2RuntimeInstallerARM64.exe
  pause
  exit /b 1
)
echo OK - Prerequis trouves
echo.

echo [3/4] Creation du dossier de sortie...
if not exist "%INSTALLER_DIR%" mkdir "%INSTALLER_DIR%"
echo OK - Dossier cree: %INSTALLER_DIR%
echo.

echo [4/4] Compilation de l'installateur Inno Setup...
if not exist %ISCC% (
  echo ERREUR: ISCC.exe introuvable.
  echo Chemin attendu: %ISCC%
  echo.
  echo Installez Inno Setup 6 depuis: https://jrsoftware.org/isdl.php
  pause
  exit /b 1
)

if not exist "%ISS_FILE%" (
  echo ERREUR: Script Inno introuvable: %ISS_FILE%
  pause
  exit /b 1
)

echo Compilation en cours...
cd /d "%ROOT%"
%ISCC% /O"%INSTALLER_DIR%" "%ISS_FILE%"
if errorlevel 1 (
  echo ERREUR: Echec de la compilation Inno Setup
  pause
  exit /b 1
)

echo.
echo ========================================
echo   TERMINE AVEC SUCCES
echo ========================================
echo.
echo Installeur cree dans: %INSTALLER_DIR%
echo Fichier: %INSTALLER_DIR%\Amaliassistant_Setup.exe
echo.
echo L'application sera installee dans: %%APPDATA%%\Amaliassistant
echo.
pause
exit /b 0
