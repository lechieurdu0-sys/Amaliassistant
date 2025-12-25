@echo off
setlocal ENABLEDELAYEDEXPANSION

set PUBLISH_DIR=%~dp0publish

if not exist "%PUBLISH_DIR%" (
  echo ERREUR: Le dossier publish n'existe pas: %PUBLISH_DIR%
  pause
  exit /b 1
)

echo ========================================
echo   Nettoyage du dossier publish
echo ========================================
echo.

echo Suppression des fichiers .pdb...
del /Q "%PUBLISH_DIR%\*.pdb" 2>nul
if errorlevel 0 (
  echo OK - Fichiers .pdb supprimes
) else (
  echo Aucun fichier .pdb trouve
)

echo.
echo Suppression des fichiers obsolètes (Video, ZQSD)...
del /Q "%PUBLISH_DIR%\GameOverlay.Video.*" 2>nul
del /Q "%PUBLISH_DIR%\GameOverlay.ZQSD.*" 2>nul
if errorlevel 0 (
  echo OK - Fichiers obsolètes supprimes
) else (
  echo Aucun fichier obsolète trouve
)

echo.
echo Suppression des logs...
if exist "%PUBLISH_DIR%\logs" (
  rmdir /S /Q "%PUBLISH_DIR%\logs" 2>nul
  echo OK - Dossier logs supprime
) else (
  echo Aucun dossier logs trouve
)

echo.
echo Suppression du dossier Release...
if exist "%PUBLISH_DIR%\Release" (
  rmdir /S /Q "%PUBLISH_DIR%\Release" 2>nul
  echo OK - Dossier Release supprime
) else (
  echo Aucun dossier Release trouve
)

echo.
echo Suppression des .deps.json inutiles...
if exist "%PUBLISH_DIR%\GameOverlay.Kikimeter.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Kikimeter.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.Models.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Models.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.Themes.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Themes.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.Windows.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Windows.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.Video.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.Video.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.ZQSD.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.ZQSD.deps.json" 2>nul
if exist "%PUBLISH_DIR%\GameOverlay.ServerSessions.deps.json" del /Q "%PUBLISH_DIR%\GameOverlay.ServerSessions.deps.json" 2>nul
echo OK - Fichiers .deps.json inutiles supprimes

echo.
echo ========================================
echo   Nettoyage termine!
echo ========================================
echo.

REM Afficher la taille du dossier
for /f "tokens=3" %%a in ('dir /-c "%PUBLISH_DIR%" ^| find "bytes"') do set SIZE=%%a
echo Taille du dossier publish: %SIZE% bytes

pause

