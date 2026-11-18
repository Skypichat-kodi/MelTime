@echo off
setlocal

rem ============================================================
rem  MelTime - construction des installeurs Inno Setup
rem
rem  Prerequis :
rem    - avoir lance build.bat (le dossier publish doit exister)
rem    - Inno Setup 6 installe
rem
rem  Produit : installeurs\sortie\MelTimeServeur-1.0.0.exe
rem            installeurs\sortie\MelTimeClient-1.0.0.exe
rem ============================================================

set "ROOT=%~dp0"
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"

if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

echo ============================================
echo  MelTime - construction des installeurs
echo ============================================
echo.

if not exist "%ISCC%" (
    echo ERREUR : Inno Setup 6 est introuvable.
    echo Installez-le depuis https://jrsoftware.org/isdl.php
    echo.
    exit /b 1
)

if not exist "%ROOT%publish\Serveur" (
    echo ERREUR : publish\Serveur est absent.
    echo Lancez d'abord build.bat.
    echo.
    exit /b 1
)

if not exist "%ROOT%publish\ClientWindows" (
    echo ERREUR : publish\ClientWindows est absent.
    echo Lancez d'abord build.bat.
    echo.
    exit /b 1
)

if exist "%ROOT%installeurs\sortie" rmdir /s /q "%ROOT%installeurs\sortie"

echo --- Installeur du serveur ---
"%ISCC%" /Q "%ROOT%installeurs\Serveur.iss"
if errorlevel 1 goto :Echec
echo     OK
echo.

echo --- Installeur du client Windows ---
"%ISCC%" /Q "%ROOT%installeurs\ClientWindows.iss"
if errorlevel 1 goto :Echec
echo     OK
echo.

echo ============================================
echo  Installeurs produits dans installeurs\sortie
echo ============================================
dir /b "%ROOT%installeurs\sortie"
exit /b 0

:Echec
echo.
echo ============================================
echo  ECHEC DE LA CONSTRUCTION DES INSTALLEURS.
echo ============================================
exit /b 1
