; StoryMaker's installer (Inno Setup 6). Built by tools\build-package.ps1, which passes:
;   AppVersion  e.g. 1.0.0 (from StoryMaker.csproj)
;   SourceDir   the published program (self-contained, .NET included)
; Result: dist\StoryMaker-<version>-Setup.exe
;
; It installs for the current user only (no admin rights): %LOCALAPPDATA%\Programs\StoryMaker, a Start
; menu shortcut, and an entry under Settings -> Apps to uninstall it. Settings and which sessions are
; told stay in %APPDATA%\LunarJournal and are kept on uninstall and update.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\app"
#endif

[Setup]
AppId={{6E2B9C4A-3F1D-4B7E-9A55-1C8D2E7F4A31}
AppName=StoryMaker
AppVersion={#AppVersion}
AppVerName=StoryMaker {#AppVersion}
AppPublisher=Lundqwist
AppPublisherURL=https://lundqwist.se/projects/lunar-journal
AppSupportURL=https://github.com/Lundqwist/StoryMaker
AppUpdatesURL=https://github.com/Lundqwist/StoryMaker/releases
AppCopyright=© 2026 Bjarne Lundqwist
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\StoryMaker
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
LicenseFile=..\LICENSE
SetupIconFile=..\StoryMaker.ico
UninstallDisplayIcon={app}\StoryMaker.exe
UninstallDisplayName=StoryMaker
WizardStyle=modern
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
OutputDir=..\dist
OutputBaseFilename=StoryMaker-{#AppVersion}-Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\StoryMaker"; Filename: "{app}\StoryMaker.exe"; Comment: "Turn your Lunar Journal into your characters' stories"
Name: "{autodesktop}\StoryMaker"; Filename: "{app}\StoryMaker.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\StoryMaker.exe"; Description: "{cm:LaunchProgram,StoryMaker}"; Flags: nowait postinstall skipifsilent
