@echo off
setlocal

rem ============================================================
rem  MelTime - compilation et publication
rem  Cible : Windows x64 / .NET 8 (framework-dependent)
rem  Prerequis machine : .NET 8 Desktop Runtime installe
rem
rem  L'arborescence produite reflete exactement l'installation :
rem    publish\Serveur\ProgramData   -> C:\ProgramData
rem    publish\Serveur\ProgramFiles  -> C:\Program Files
rem    publish\ClientWindows\ProgramFiles -> C:\Program Files
rem  Les installeurs Inno Setup n'ont plus qu'a recopier des dossiers.
rem ============================================================

set "ROOT=%~dp0"
set "CONFIG=Release"
set "RID=win-x64"
set "OUT=%ROOT%publish"

set "DOSSIER_SERVICE=%OUT%\Serveur\ProgramData\MelTime"
set "DOSSIER_SERVEUR=%OUT%\Serveur\ProgramFiles\MelTime"
set "DOSSIER_CLIENT=%OUT%\ClientWindows\ProgramFiles\MelTimeClient"

echo ============================================
echo  MelTime - compilation
echo ============================================
echo.

tasklist /FI "IMAGENAME eq MelTime.Service.exe" 2>NUL | find /I "MelTime.Service.exe" >NUL
if not errorlevel 1 (
    echo ATTENTION : MelTime.Service.exe est en cours d'execution.
    echo La publication echouera tant que le service tournera.
    echo Fermez-le ^(Ctrl+C dans sa fenetre^) puis relancez ce script.
    echo.
    exit /b 1
)

if exist "%OUT%" (
    echo Nettoyage du dossier publish...
    rmdir /s /q "%OUT%"
)
echo.

call :Publier "MelTime.Server\MelTime.Service\MelTime.Service.csproj" "%DOSSIER_SERVICE%" "Service"
if errorlevel 1 goto :Echec

call :Publier "MelTime.Server\MelTime.Tray\MelTime.Tray.csproj" "%DOSSIER_SERVEUR%" "Tray"
if errorlevel 1 goto :Echec

call :Publier "MelTime.Server\MelTime.UI\MelTime.UI.csproj" "%DOSSIER_SERVEUR%" "UI"
if errorlevel 1 goto :Echec

call :Publier "MelTimeClientWindows\MelTimeClientWindows.csproj" "%DOSSIER_CLIENT%" "ClientWindows"
if errorlevel 1 goto :Echec

echo.
echo ============================================
echo  Compilation terminee avec succes.
echo.
echo  Paquet serveur     : %OUT%\Serveur
echo  Paquet client      : %OUT%\ClientWindows
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
set "DESTINATION=%~2"
set "NOM=%~3"
echo --- Publication de %NOM% ---
dotnet publish "%ROOT%%PROJET%" -c %CONFIG% -r %RID% --self-contained false -p:PlatformTarget=x64 -o "%DESTINATION%" -v minimal
if errorlevel 1 (
    echo.
    echo ERREUR : publication de %NOM% impossible.
    exit /b 1
)
echo     OK
echo.
exit /b 0
