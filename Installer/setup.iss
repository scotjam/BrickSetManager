; Brick Set Manager Installer Script for Inno Setup

#define MyAppName "Brick Set Manager"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Brick Set Manager"
#define MyAppExeName "BrickSetManager.exe"

[Setup]
; App information
AppId={{8B5E9A2D-4F1C-4A3B-9D8E-2C5F6A7B8C9D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\BrickSetManager
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=BrickSetManagerSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; App icon and images
;SetupIconFile=..\BrickSetManager\icon.ico
;WizardImageFile=installer-image.bmp
;WizardSmallImageFile=installer-small.bmp

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main executable and all dependencies from publish folder
Source: "..\BrickSetManager\bin\Release\net7.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\BrickSetManager"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
