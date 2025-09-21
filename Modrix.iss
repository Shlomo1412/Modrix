[Setup]
AppId={{279B89A5-9D88-49DE-9FF5-6658E51D78D3}
AppName=Modrix
AppVersion=1.0.0
AppVerName=Modrix 1.0.0
AppPublisher=Modrix Development Team
AppPublisherURL=https://github.com/Shlomo1412/Modrix
AppSupportURL=https://github.com/Shlomo1412/Modrix/issues
AppUpdatesURL=https://github.com/Shlomo1412/Modrix/releases
DefaultDirName={autopf}\Modrix
DefaultGroupName=Modrix
LicenseFile=LICENSE.txt
InfoBeforeFile=README.md
OutputDir=installer
OutputBaseFilename=ModrixSetup
SetupIconFile=Resources\ModrixIcon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
Source: "exe\Modrix.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "exe\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\Modrix"; Filename: "{app}\Modrix.exe"
Name: "{group}\{cm:UninstallProgram,Modrix}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Modrix"; Filename: "{app}\Modrix.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Modrix"; Filename: "{app}\Modrix.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\Modrix.exe"; Description: "{cm:LaunchProgram,Modrix}"; Flags: nowait postinstall skipifsilent