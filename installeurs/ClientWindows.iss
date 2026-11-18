; ============================================================================
;  MelTime Client Windows - script d'installation Inno Setup
;
;  Produit distinct du serveur : il peut etre installe sur n'importe quel
;  poste, sans aucun des programmes du serveur.
;
;  Compilation : ISCC.exe ClientWindows.iss
;  (ou build-installeurs.bat a la racine du projet)
;
;  Le contenu provient de publish\ClientWindows, produit par build.bat.
; ============================================================================

#define NomApp  "MelTime Client"
#define Editeur "MelTime"

; ---------------------------------------------------------------------------
; Version lue directement dans l'executable produit par build.bat.
; Une seule source de verite : GitVersion, via GitVersion.MsBuild.
; ---------------------------------------------------------------------------
#define FichierReference "..\publish\ClientWindows\ProgramFiles\MelTimeClient\MelTimeClientWindows.exe"

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
; Identifiant distinct de celui du serveur : les deux produits
; s'installent, se mettent a jour et se desinstallent independamment.
AppId={{FC694743-86AC-4B3F-A0E4-92EC01E8D4BD}
AppName={#NomApp}
AppVersion={#Version}
AppVerName={#NomApp} {#Version}
VersionInfoVersion={#VersionFichier}
AppPublisher={#Editeur}
DefaultDirName={commonpf}\MelTimeClient
DefaultGroupName=MelTime Client
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=sortie
OutputBaseFilename=MelTimeClient-{#Version}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#NomApp}
UninstallDisplayIcon={app}\MelTimeClientWindows.exe

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
Source: "..\publish\ClientWindows\ProgramFiles\*"; DestDir: "{commonpf}"; \
    Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{commondesktop}\MelTime"; Filename: "{app}\MelTimeClientWindows.exe"; \
    WorkingDir: "{app}"; Comment: "Saisir ma feuille de temps"
Name: "{commonprograms}\MelTime Client\MelTime"; Filename: "{app}\MelTimeClientWindows.exe"; \
    WorkingDir: "{app}"
Name: "{commonprograms}\MelTime Client\Desinstaller {#NomApp}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\MelTimeClientWindows.exe"; Description: "Lancer MelTime"; \
    Flags: postinstall nowait skipifsilent

[Code]
// Le client peut etre ouvert : sans cet arret, ses fichiers seraient
// verrouilles et la mise a jour echouerait.
procedure ArreterMelTime();
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM MelTimeClientWindows.exe /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(400);
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
