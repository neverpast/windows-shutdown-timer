#ifndef AppVersion
#define AppVersion "1.0.0"
#endif

#define AppName "Windows 定时关机"
#define AppExeName "WindowsShutdownTimer.exe"
#define SourceDir "..\dist\publish"

[Setup]
AppId={{C8D0F6E9-37B1-4101-9A5C-E9E47C4C4522}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=WindowsShutdownTimer
DefaultDirName={localappdata}\Programs\WindowsShutdownTimer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=WindowsShutdownTimer-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayName={#AppName}
SetupIconFile=..\src\WindowsShutdownTimer.App\Assets\AppIcon.ico
UsePreviousAppDir=yes
UsePreviousGroup=yes
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartApplications=no
AlwaysRestart=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "登录 Windows 后自动启动"; GroupDescription: "启动选项:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WindowsShutdownTimer"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExeName} /F >NUL 2>&1"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\WindowsShutdownTimer"
Type: dirifempty; Name: "{app}"
