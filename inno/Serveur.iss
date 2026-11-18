; ============================================================================
;  MelTime Serveur - script d'installation Inno Setup
;
;  Contient les trois programmes du serveur :
;    - MelTime.Service.exe  ->  C:\ProgramData\MelTime
;    - MelTime.Tray.exe     ->  C:\Program Files\MelTime
;    - MelTime.UI.exe       ->  C:\Program Files\MelTime
;
;  Compilation : ISCC.exe Serveur.iss
;  (ou build-installeurs.bat a la racine du projet)
;
;  Le contenu provient de publish\Serveur, produit par build.bat.
; ============================================================================

#define NomApp  "MelTime Serveur"
#define Editeur "MelTime"

; ---------------------------------------------------------------------------
; Version lue directement dans l'executable produit par build.bat.
; Une seule source de verite : GitVersion (via le package GitVersion.MsBuild)
; l'ecrit dans les assemblies, et Inno Setup la relit ici.
; Aucun fichier intermediaire, aucun script.
; ---------------------------------------------------------------------------
#define FichierReference "..\publish\Serveur\ProgramFiles\MelTime\MelTime.UI.exe"

#if FileExists(FichierReference)
  #define VersionFichier GetFileVersion(FichierReference)
  #define VersionProduit GetStringFileInfo(FichierReference, "ProductVersion")
  #define PositionPlus Pos("+", VersionProduit)
  #if PositionPlus > 0
    #define Version Copy(VersionProduit, 1, PositionPlus - 1)
  #else
    #define Version VersionProduit
  #endif
#else
  #error Executable introuvable. Lancez build.bat avant build-installeurs.bat.
#endif

[Setup]
; Identifiant stable : il permet aux futures versions de mettre a jour
; l'installation existante au lieu d'en creer une seconde.
AppId={{3F27B89C-370F-4A12-9DA9-7FFE3CFB3B56}
AppName={#NomApp}
AppVersion={#Version}
AppVerName={#NomApp} {#Version}
VersionInfoVersion={#VersionFichier}
AppPublisher={#Editeur}
DefaultDirName={commonpf}\MelTime
DefaultGroupName=MelTime
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\Installers
OutputBaseFilename=MelTimeServeur-{#Version}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#NomApp}
UninstallDisplayIcon={app}\MelTime.UI.exe

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
; Program Files : Tray et UI. L'arborescence MelTime\ vient du publish.
; Les fichiers .pdb (debogage) sont exclus de la distribution.
Source: "..\publish\Serveur\ProgramFiles\*"; DestDir: "{commonpf}"; \
    Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

; ProgramData : le service et ses dependances (SQLite comprise).
Source: "..\publish\Serveur\ProgramData\*"; DestDir: "{commonappdata}"; \
    Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{commondesktop}\MelTime Administration"; Filename: "{app}\MelTime.UI.exe"; \
    WorkingDir: "{app}"; Comment: "Gerer les comptes clients de MelTime"
Name: "{commonprograms}\MelTime\MelTime Administration"; Filename: "{app}\MelTime.UI.exe"; \
    WorkingDir: "{app}"
Name: "{commonprograms}\MelTime\Desinstaller {#NomApp}"; Filename: "{uninstallexe}"

[Run]
; Ouverture du port dans le pare-feu Windows. Sans cela, le service ecoute
; mais Windows refuse les connexions venant d'autres machines, et le client
; affiche « serveur injoignable » sans aucune explication.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=MelTime-Service-TCP-5080 dir=in action=allow protocol=TCP localport=5080 profile=any"; \
    Flags: runhidden; StatusMsg: "Ouverture du port 5080 dans le pare-feu..."

; runascurrentuser est indispensable : par defaut, une entree marquee postinstall
; s'execute avec l'utilisateur d'origine, donc SANS les droits d'administrateur.
; L'UI porte un manifeste requireAdministrator : sans ce drapeau, elle ne
; pourrait pas demarrer (et ne pourrait pas joindre le service sous SYSTEM).
Filename: "{app}\MelTime.UI.exe"; Description: "Lancer l'administration MelTime"; \
    Flags: postinstall nowait skipifsilent runascurrentuser

[UninstallRun]
; La regle de pare-feu doit disparaitre avec le logiciel.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=MelTime-Service-TCP-5080"; \
    Flags: runhidden; RunOnceId: "SupprimerReglePareFeu"

; Sans cela, des taches planifiees continueraient de viser des executables supprimes.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""MelTime Service"" /F"; \
    Flags: runhidden; RunOnceId: "SupprimerTacheService"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""MelTime Tray"" /F"; \
    Flags: runhidden; RunOnceId: "SupprimerTacheTray"

[UninstallDelete]
; Volontairement vide : les dossiers data\ et logs\ contiennent les comptes
; et les feuilles de temps des clients. Une desinstallation ne doit jamais
; detruire ces donnees. Elles se suppriment a la main, en connaissance de cause.

[Code]
// Le service, le Tray et l'administration peuvent etre en cours d'execution.
// Sans cet arret prealable, leurs fichiers seraient verrouilles et
// l'installation d'une nouvelle version echouerait.
procedure ArreterMelTime();
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM MelTime.UI.exe /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM MelTime.Tray.exe /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM MelTime.Service.exe /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(600);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    ArreterMelTime();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    ArreterMelTime();
end;
