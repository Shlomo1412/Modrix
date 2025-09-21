[Setup]
AppName=Modrix
AppVersion=1.0.0
DefaultDirName={pf}\Modrix
OutputDir=Output
OutputBaseFilename=ModrixSetup
Compression=lzma
SolidCompression=yes

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\Modrix"; Filename: "{app}\Modrix.exe"
