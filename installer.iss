#define SourceDir ExtractFilePath(SourcePath)
#if Pos("InstallerAppData", SourceDir) > 0
  #define RootPath ExtractFilePath(ExtractFilePath(SourcePath))
#else
  #define RootPath ExtractFilePath(SourcePath)
#endif

[Setup]
AppId={{A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D}
AppName=Amaliassistant
AppVersion=1.0
AppPublisher=Amaliassistant
AppPublisherURL=
AppSupportURL=
DefaultDirName={userappdata}\Amaliassistant
DisableDirPage=yes
DefaultGroupName=Amaliassistant
DisableProgramGroupPage=yes
OutputDir=InstallerAppData
OutputBaseFilename=Amaliassistant_Setup
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMABlockSize=65536
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\GameOverlay.App.exe
SetupIconFile={#RootPath}Amalia.ico
AllowNoIcons=yes
WizardStyle=modern

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le bureau"; GroupDescription: "Raccourcis supplémentaires:"; Flags: unchecked
Name: "startup"; Description: "Lancer au démarrage de Windows"; GroupDescription: "Options:"; Flags: unchecked

[Files]
; Application principale - exclure les fichiers inutiles
Source: "{#RootPath}publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.deps.json,logs\*,Release\*,GameOverlay.Video.*,GameOverlay.ZQSD.*"
; Inclure GameOverlay.App.deps.json qui est nécessaire
Source: "{#RootPath}publish\GameOverlay.App.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RootPath}Amalia.ico"; DestDir: "{app}"; Flags: ignoreversion
; Prérequis - copier tous les fichiers dans le dossier temporaire
Source: "{#RootPath}Prerequisites\windowsdesktop-runtime-8.0.21-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#RootPath}Prerequisites\windowsdesktop-runtime-8.0.21-win-x86.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#RootPath}Prerequisites\windowsdesktop-runtime-8.0.21-win-arm64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#RootPath}Prerequisites\MicrosoftEdgeWebView2RuntimeInstallerx64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#RootPath}Prerequisites\MicrosoftEdgeWebView2RuntimeInstallerx86.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#RootPath}Prerequisites\MicrosoftEdgeWebView2RuntimeInstallerARM64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Amaliassistant"; Filename: "{app}\GameOverlay.App.exe"; IconFilename: "{app}\Amalia.ico"
Name: "{autodesktop}\Amaliassistant"; Filename: "{app}\GameOverlay.App.exe"; IconFilename: "{app}\Amalia.ico"; Tasks: desktopicon
Name: "{userstartup}\Amaliassistant"; Filename: "{app}\GameOverlay.App.exe"; IconFilename: "{app}\Amalia.ico"; Tasks: startup

[Run]
Filename: "{app}\GameOverlay.App.exe"; Description: "Lancer Amaliassistant"; Flags: nowait postinstall skipifsilent

[Code]

// Fonction pour vérifier si .NET 8.0 Desktop Runtime est installé
function IsDotNetInstalled: Boolean;
var
  InstalledVer: String;
begin
  Result := False;
  
  // Vérifier pour x64
  if RegQueryStringValue(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost', 'Version', InstalledVer) then
  begin
    if Copy(InstalledVer, 1, 3) = '8.0' then
    begin
      Result := True;
      Exit;
    end;
  end;
  
  // Vérifier pour x86
  if RegQueryStringValue(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedhost', 'Version', InstalledVer) then
  begin
    if Copy(InstalledVer, 1, 3) = '8.0' then
    begin
      Result := True;
      Exit;
    end;
  end;
  
  // Vérifier pour ARM64
  if RegQueryStringValue(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\arm64\sharedhost', 'Version', InstalledVer) then
  begin
    if Copy(InstalledVer, 1, 3) = '8.0' then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

// Fonction pour vérifier si WebView2 Runtime est installé
function IsWebView2Installed: Boolean;
var
  Ver: String;
begin
  Result := False;
  
  // Vérifier dans la clé de registre standard pour WebView2
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Ver) then
  begin
    Result := True;
    Exit;
  end;
  
  // Vérifier aussi la clé alternative
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Ver) then
  begin
    Result := True;
    Exit;
  end;
end;

// Détection de l'architecture
function GetArchitecture: String;
var
  ProcessorArchitecture: String;
begin
  // Détecter l'architecture du système
  if not RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'PROCESSOR_ARCHITECTURE', ProcessorArchitecture) then
  begin
    // Essayer une autre méthode
    if IsWin64 then
      ProcessorArchitecture := 'AMD64'
    else
      ProcessorArchitecture := 'x86';
  end;
  
  if ProcessorArchitecture = 'AMD64' then
    Result := 'x64'
  else if (ProcessorArchitecture = 'ARM64') or (ProcessorArchitecture = 'arm64') then
    Result := 'arm64'
  else
    Result := 'x86';
end;

// Fonction pour installer un prérequis silencieusement
function InstallPrerequisite(FileName: String; Description: String): Boolean;
var
  StatusText: String;
  ResultCode: Integer;
  FullPath: String;
begin
  Result := False;
  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := 'Installation de ' + Description + '... (silencieux)';
  FullPath := ExpandConstant('{tmp}\' + FileName);
  
  // Installation silencieuse avec /quiet /norestart
  if Exec(FullPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // Vérifier le code de retour (0 = succès, 3010 = redémarrage requis mais ignoré)
    if (ResultCode = 0) or (ResultCode = 3010) then
    begin
      Result := True;
    end;
  end;
  
  WizardForm.StatusLabel.Caption := StatusText;
end;

procedure InitializeWizard;
begin
  // Forcer le chemin d'installation vers AppData\Roaming
  WizardForm.DirEdit.Text := ExpandConstant('{userappdata}\Amaliassistant');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Arch: String;
  DotNetFile: String;
  WebView2File: String;
begin
  // Installer les prérequis après l'extraction des fichiers mais avant l'installation de l'app
  if CurStep = ssInstall then
  begin
    Arch := GetArchitecture;
    
    // Vérifier et installer .NET 8.0 Desktop Runtime
    if not IsDotNetInstalled then
    begin
      // Déterminer le fichier .NET approprié selon l'architecture
      if Arch = 'x64' then
        DotNetFile := 'windowsdesktop-runtime-8.0.21-win-x64.exe'
      else if Arch = 'arm64' then
        DotNetFile := 'windowsdesktop-runtime-8.0.21-win-arm64.exe'
      else
        DotNetFile := 'windowsdesktop-runtime-8.0.21-win-x86.exe';
      
      // Installer .NET silencieusement
      if not InstallPrerequisite(DotNetFile, '.NET 8.0 Desktop Runtime') then
      begin
        MsgBox('L''installation de .NET 8.0 Desktop Runtime a échoué. L''application pourrait ne pas fonctionner correctement.', mbError, MB_OK);
      end;
    end;
    
    // Vérifier et installer WebView2 Runtime
    if not IsWebView2Installed then
    begin
      // Déterminer le fichier WebView2 approprié selon l'architecture
      if Arch = 'x64' then
        WebView2File := 'MicrosoftEdgeWebView2RuntimeInstallerx64.exe'
      else if Arch = 'arm64' then
        WebView2File := 'MicrosoftEdgeWebView2RuntimeInstallerARM64.exe'
      else
        WebView2File := 'MicrosoftEdgeWebView2RuntimeInstallerx86.exe';
      
      // Installer WebView2 silencieusement
      if not InstallPrerequisite(WebView2File, 'WebView2 Runtime') then
      begin
        MsgBox('L''installation de WebView2 Runtime a échoué. L''application pourrait ne pas fonctionner correctement.', mbError, MB_OK);
      end;
    end;
  end
  
  // Configuration après installation
  else if CurStep = ssPostInstall then
  begin
    // Créer le dossier de configuration s'il n'existe pas
    ForceDirectories(ExpandConstant('{userappdata}\Amaliassistant'));
    
    // Configuration du lancement au démarrage si demandé
    if WizardIsTaskSelected('startup') then
    begin
      RegWriteStringValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', 'Amaliassistant', ExpandConstant('"{app}\GameOverlay.App.exe"'));
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  RootDir: String;
begin
  Result := True;
  
  // Utiliser le chemin racine défini dans la constante
  RootDir := ExpandConstant('{#RootPath}');
  
  // Vérifier que le dossier publish existe
  if not DirExists(RootDir + 'publish') then
  begin
    MsgBox('Le dossier "publish" est introuvable dans: ' + RootDir + #13#10 + 'Veuillez d''abord compiler l''application avec "dotnet publish".', mbError, MB_OK);
    Result := False;
    Exit;
  end;
  
  // Vérifier que le dossier Prerequisites existe
  if not DirExists(RootDir + 'Prerequisites') then
  begin
    MsgBox('Le dossier "Prerequisites" est introuvable dans: ' + RootDir + #13#10 + 'Veuillez vous assurer que les fichiers de prérequis sont présents.', mbError, MB_OK);
    Result := False;
    Exit;
  end;
  
  // Vérifier que les fichiers de prérequis existent
  if not FileExists(RootDir + 'Prerequisites\windowsdesktop-runtime-8.0.21-win-x64.exe') then
  begin
    MsgBox('Fichier de prérequis manquant: windowsdesktop-runtime-8.0.21-win-x64.exe' + #13#10 + 'Dans: ' + RootDir + 'Prerequisites', mbError, MB_OK);
    Result := False;
    Exit;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Suppression du lancement au démarrage
    RegDeleteValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', 'Amaliassistant');
  end;
end;
