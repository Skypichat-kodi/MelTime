@echo off
chcp 65001 >nul
setlocal

REM ============================================================
REM  MelTime - construction du paquet CLIENT WINDOWS et de son installeur
REM
REM  1. arrete le client s'il est ouvert
REM  2. publie le client dans publish\ClientWindows
REM  3. compile l'installeur Inno Setup
REM
REM  Resultat : Installers\MelTimeClient-<version>.exe
REM
REM  Usage : build-client.bat [--auto]
REM          --auto : pas de pause, pas d'ouverture du dossier
REM ============================================================

for /F "delims=" %%A in ('echo prompt $E^| cmd') do set "ESC=%%A"
set "VERT=%ESC%[92m"
set "ROUGE=%ESC%[91m"
set "JAUNE=%ESC%[93m"
set "CYAN=%ESC%[96m"
set "RAZ=%ESC%[0m"

set "ROOT=%~dp0"
set "PUB=%ROOT%publish\ClientWindows"
set "DEST=%PUB%\ProgramFiles\MelTimeClient"
set "INSTALLERS=%ROOT%Installers"
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

echo %CYAN%============================================%RAZ%
echo %CYAN%  MelTime - build CLIENT WINDOWS%RAZ%
echo %CYAN%============================================%RAZ%
echo.

echo %JAUNE%  [INFO]%RAZ% Arret du client MelTime...
taskkill /IM MelTimeClientWindows.exe /F >nul 2>&1
echo   %VERT%[OK]%RAZ%   client arrete ou deja ferme
echo.

echo %JAUNE%  [INFO]%RAZ% Nettoyage de publish\ClientWindows...
if exist "%PUB%" rmdir /s /q "%PUB%"
echo.

call :Publier "MelTimeClientWindows\MelTimeClientWindows.csproj" "Client Windows" "%DEST%"
if errorlevel 1 goto :Echec

echo %JAUNE%  [INFO]%RAZ% Compilation de l'installeur...
echo.

if not exist "%ISCC%" (
    echo   %ROUGE%[ERREUR]%RAZ% Inno Setup 6 est introuvable.
    echo   Installez-le depuis https://jrsoftware.org/isdl.php
    goto :Echec
)

if not exist "%INSTALLERS%" mkdir "%INSTALLERS%"

"%ISCC%" /Q "%ROOT%inno\ClientWindows.iss"
if errorlevel 1 (
    echo   %ROUGE%[ERREUR]%RAZ% Compilation de l'installeur impossible.
    goto :Echec
)

for %%F in ("%INSTALLERS%\MelTimeClient-*.exe") do echo   %VERT%[OK]%RAZ%   %%~nxF

echo.
echo %VERT%============================================%RAZ%
echo %VERT%  BUILD CLIENT TERMINE%RAZ%
echo %VERT%============================================%RAZ%
echo.
echo   Installeur dans : %INSTALLERS%
goto :Fin

:Echec
echo.
echo %ROUGE%============================================%RAZ%
echo %ROUGE%  ECHEC DU BUILD CLIENT%RAZ%
echo %ROUGE%============================================%RAZ%
if /I not "%~1"=="--auto" pause
exit /b 1

:Fin
if /I not "%~1"=="--auto" start "" "%INSTALLERS%"
if /I not "%~1"=="--auto" pause
exit /b 0

REM ============================================================
REM  Publication d'un projet
REM  %1 = chemin du .csproj, relatif a la racine
REM  %2 = nom affiche
REM  %3 = dossier de destination
REM ============================================================
:Publier
set "PROJET=%~1"
set "NOM=%~2"
set "CIBLE=%~3"
echo   [....] %NOM%
dotnet publish "%ROOT%%PROJET%" -c Release -r win-x64 --self-contained false -p:PlatformTarget=x64 -o "%CIBLE%" --nologo -v quiet
if errorlevel 1 (
    echo   %ROUGE%[ERREUR]%RAZ% publication de %NOM% impossible
    exit /b 1
)
echo   %VERT%[OK]%RAZ%   %NOM%
echo.
exit /b 0
