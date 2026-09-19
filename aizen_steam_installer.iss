; ==============================================================================
; AizenRex Steam Switcher - Professional Installer Script (Inno Setup 6)
; Author: Aizenrex x Riyad
; Version: 2.2.0
;
; Paths resolve from AIZEN_SOURCE_ROOT when the release pipeline sets it, and
; otherwise from this script's own folder, so the repository never hard-codes
; a personal directory path.
; ==============================================================================

#define MyAppName "AizenRex Steam Switcher"
#define MyAppPublisher "Aizenrex x Riyad"
#define MyAppURL "https://github.com/aizenrexx/AizenRex-Steam-Switcher"
#define MyAppExeName "SteamSwitcher.exe"

#define MySourceRoot GetEnv('AIZEN_SOURCE_ROOT')
#if MySourceRoot == ""
#define MySourceRoot SourcePath
#endif

#define MyAppVersion GetEnv('AIZEN_VERSION')
#if MyAppVersion == ""
#define MyAppVersion "2.2.0"
#endif

#define MyAppIcon MySourceRoot + "\src\SteamSwitcher\Assets\app.ico"
#define MyLicense MySourceRoot + "\license.txt"
#define MySourceDir MySourceRoot + "\Distribution\Portable"
#define MyOutputDir MySourceRoot + "\Distribution\Installer"

[Setup]
; Stable application GUID so upgrades install in place and never stack up.
AppId={{7C4A1E62-9B3D-4F58-A1E7-2D8C6B904F31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=AizenRex Steam Switcher Setup - Steam profile manager
VersionInfoCopyright=Copyright (C) 2026 Aizenrex x Riyad. MIT licensed.

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile={#MyLicense}
OutputDir={#MyOutputDir}
OutputBaseFilename=AizenRex-Steam-Switcher-Setup-v{#MyAppVersion}
SetupIconFile={#MyAppIcon}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; The application manifest requests administrator rights because it renames
; folders inside Program Files, so the installer matches it.
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableWelcomePage=no
DisableProgramGroupPage=yes

; In-place upgrade: keep the folder the user chose and overwrite binaries
; regardless of timestamps, while never touching their data or backups.
UsePreviousAppDir=yes
DisableDirPage=auto
AppMutex=AizenRexSteamSwitcherMutex
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; ignoreversion lets an upgrade or a downgrade replace files without a prompt.
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyAppIcon}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Deliberately empty of data paths: settings, history and Steam config backups
; created at runtime are left on disk so an uninstall never destroys them.
Type: filesandordirs; Name: "{app}\logs"

[Messages]
WelcomeLabel2=This will install {#MyAppName} v{#MyAppVersion} on your computer.%n%n{#MyAppName} manages two Steam installations and switches between them. It needs administrator rights because it renames folders inside Program Files.%n%nYour Steam config backups and settings are kept when you upgrade.