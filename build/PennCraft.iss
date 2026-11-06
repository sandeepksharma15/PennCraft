#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef PackageSource
  #error PackageSource is required
#endif
#ifndef PackageOutput
  #error PackageOutput is required
#endif
#ifndef FileVersion
  #define FileVersion "0.1.0.0"
#endif
[Setup]
AppId={{C904B0DD-C743-4B66-8235-8A0AC163770B}
AppName=PennCraft
AppVersion={#AppVersion}
AppPublisher=PennCraft contributors
AppPublisherURL=https://github.com/sandeepksharma15/PennCraft
AppSupportURL=https://github.com/sandeepksharma15/PennCraft/issues
AppUpdatesURL=https://github.com/sandeepksharma15/PennCraft/releases
DefaultDirName={localappdata}\Programs\PennCraft
DefaultGroupName=PennCraft
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
AppMutex=Local\PennCraft.App
CloseApplications=no
RestartApplications=no
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
SetupIconFile=..\src\PennCraft.Desktop\Assets\PennCraft.ico
UninstallDisplayIcon={app}\PennCraft.exe
OutputDir={#PackageOutput}
OutputBaseFilename=PennCraft-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion={#FileVersion}
VersionInfoDescription=PennCraft beta setup
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "{#PackageSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\PennCraft"; Filename: "{app}\PennCraft.exe"
Name: "{autodesktop}\PennCraft"; Filename: "{app}\PennCraft.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\PennCraft.exe"; Description: "Launch PennCraft"; Flags: nowait postinstall skipifsilent
; No UninstallDelete section: settings and managed desktop files belong to the user.
