@echo off
setlocal

rem ============================================================
rem  MelTime - compilation et publication
rem  Cible : Windows x64 / .NET 8 (framework-dependent)
rem  Prerequis machine : .NET 8 Desktop Runtime installe
rem  Produit : publish\Service, publish\Tray, publish\UI, publish\ClientWindows
rem ============================================================

set "ROOT=%~dp0"
set "CONFIG=Release"
set "RID=win-x64"
set "OUT=%ROOT%publish"

echo ============================================
echo  MelTime - compilation
echo ============================================
echo.

if exist "%OUT%" (
    echo Nettoyage du dossier publish...
    rmdir /s /q "%OUT%"
)
echo.

call :Publier "MelTime.Server\MelTime.Service\MelTime.Service.csproj" "Service"
if errorlevel 1 goto :Echec

call :Publier "MelTime.Server\MelTime.Tray\MelTime.Tray.csproj" "Tray"
if errorlevel 1 goto :Echec

call :Publier "MelTime.Server\MelTime.UI\MelTime.UI.csproj" "UI"
if errorlevel 1 goto :Echec

call :Publier "MelTimeClientWindows\MelTimeClientWindows.csproj" "ClientWindows"
if errorlevel 1 goto :Echec

echo.
echo ============================================
echo  Compilation terminee avec succes.
echo  Sortie : %OUT%
echo ============================================
exit /b 0

:Echec
echo.
echo ============================================
echo  ECHEC DE LA COMPILATION.
echo ============================================
exit /b 1

:Publier
set "PROJET=%~1"
set "NOM=%~2"
echo --- Publication de %NOM% ---
dotnet publish "%ROOT%%PROJET%" -c %CONFIG% -r %RID% --self-contained false -p:PlatformTarget=x64 -o "%OUT%\%NOM%" -v minimal
if errorlevel 1 (
    echo.
    echo ERREUR : publication de %NOM% impossible.
    exit /b 1
)
echo     OK
echo.
exit /b 0
