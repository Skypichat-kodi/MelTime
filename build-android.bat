@echo off
chcp 65001 >nul
setlocal

REM ============================================================
REM  MelTime - compilation du client ANDROID (.apk)
REM
REM  Produit : Apk\MelTimeClientAndroid.apk
REM
REM  Prerequis :
REM    - JAVA_HOME    -> dossier du JDK 17
REM    - ANDROID_HOME -> dossier du SDK Android (API 36)
REM    - workload maui-android installe pour le SDK .NET 10
REM
REM  Usage : build-android.bat [--auto]
REM          --auto : pas de pause, pas d'ouverture du dossier
REM ============================================================

for /F "delims=" %%A in ('echo prompt $E^| cmd') do set "ESC=%%A"
set "VERT=%ESC%[92m"
set "ROUGE=%ESC%[91m"
set "JAUNE=%ESC%[93m"
set "CYAN=%ESC%[96m"
set "RAZ=%ESC%[0m"

set "ROOT=%~dp0"
set "PROJET=%ROOT%MelTimeClientAndroid\MelTimeClientAndroid.csproj"
set "SORTIE=%ROOT%Apk"

REM Indispensable : global.json n'est lu que depuis le dossier du projet.
REM Celui du projet Android epingle le SDK .NET 10, alors que celui de la
REM racine epingle le SDK 8 pour le serveur et le client Windows. Se placer
REM ici evite que dotnet prenne le SDK 8, qui ne connait pas net10.0-android.
REM Le point final est necessaire : ROOT se termine par un backslash, lequel
REM echapperait le guillemet fermant et fausserait le chemin.
cd /d "%ROOT%MelTimeClientAndroid"

echo %CYAN%============================================%RAZ%
echo %CYAN%  MelTime - build ANDROID%RAZ%
echo %CYAN%============================================%RAZ%
echo.

if not defined JAVA_HOME (
    echo   %ROUGE%[ERREUR]%RAZ% JAVA_HOME n'est pas defini.
    echo   Definissez-le une fois pour toutes :
    echo     setx JAVA_HOME "C:\jdk"
    goto :Echec
)

if not defined ANDROID_HOME (
    echo   %ROUGE%[ERREUR]%RAZ% ANDROID_HOME n'est pas defini.
    echo   Definissez-le une fois pour toutes :
    echo     setx ANDROID_HOME "C:\android-sdk"
    goto :Echec
)

echo %JAUNE%  [INFO]%RAZ% Java    : %JAVA_HOME%
echo %JAUNE%  [INFO]%RAZ% Android : %ANDROID_HOME%
echo %JAUNE%  [INFO]%RAZ% Dossier : %CD%

if exist "%ROOT%signature.props" (
    echo %JAUNE%  [INFO]%RAZ% Signature : cle de production
) else (
    echo %JAUNE%  [INFO]%RAZ% Signature : cle de debogage ^(installation USB seulement^)
)
echo.

if not exist "%PROJET%" (
    echo   %ROUGE%[ERREUR]%RAZ% Projet introuvable : %PROJET%
    goto :Echec
)

echo %JAUNE%  [INFO]%RAZ% Compilation en cours...
echo.

dotnet publish "%PROJET%" -c Release -f net10.0-android --nologo -v minimal -p:AndroidPackageFormats=apk
if errorlevel 1 (
    echo   %ROUGE%[ERREUR]%RAZ% La compilation a echoue.
    goto :Echec
)

set "APK="
for /R "%ROOT%MelTimeClientAndroid\bin\Release" %%F in (*-Signed.apk) do set "APK=%%F"
if not defined APK (
    echo   %ROUGE%[ERREUR]%RAZ% Aucun APK signe n'a ete produit.
    goto :Echec
)

if not exist "%SORTIE%" mkdir "%SORTIE%"
if exist "%SORTIE%\MelTimeClientAndroid.apk" del /q "%SORTIE%\MelTimeClientAndroid.apk"
copy /y "%APK%" "%SORTIE%\MelTimeClientAndroid.apk" >nul

for %%F in ("%SORTIE%\MelTimeClientAndroid.apk") do set "TAILLE=%%~zF"

echo   %VERT%[OK]%RAZ%   MelTimeClientAndroid.apk  (%TAILLE% octets)
echo.
echo %VERT%============================================%RAZ%
echo %VERT%  BUILD ANDROID TERMINE%RAZ%
echo %VERT%============================================%RAZ%
echo.
echo   APK dans : %SORTIE%
echo.
echo   Installation sur un telephone (debogage USB active) :
echo     "%ANDROID_HOME%\platform-tools\adb.exe" install -r "%SORTIE%\MelTimeClientAndroid.apk"
goto :Fin

:Echec
echo.
echo %ROUGE%============================================%RAZ%
echo %ROUGE%  ECHEC DU BUILD ANDROID%RAZ%
echo %ROUGE%============================================%RAZ%
if /I not "%~1"=="--auto" pause
exit /b 1

:Fin
if /I not "%~1"=="--auto" start "" "%SORTIE%"
if /I not "%~1"=="--auto" pause
exit /b 0
