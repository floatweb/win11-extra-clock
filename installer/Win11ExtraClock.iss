; === Inno Setup script ===
#define MyAppName      "Win11 Extra Clock"
#define MyAppPublisher "Float Web"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\out\publish"
#endif

#ifndef OutDir
  #define OutDir "dist"
#endif

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir={#OutDir}
OutputBaseFilename=Win11ExtraClock-x64-{#MyAppVersion}-Setup
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2
SolidCompression=yes
SetupLogging=yes

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Win11 Extra Clock.exe"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\Win11 Extra Clock.exe"; Tasks: autostart

[Tasks]
Name: "autostart"; Description: "Start {#MyAppName} with Windows"; GroupDescription: "Options:"; Flags: checkedonce

[Run]
Filename: "{app}\Win11 Extra Clock.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
