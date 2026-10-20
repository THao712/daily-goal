#define AppName "Daily Goal"
#define AppVersion "2.5.0"
#define AppPublisher "Daily Goal"
#define AppExeName "DailyGoal.exe"

[Setup]
AppId={{58A9578C-EF51-4E72-9F42-C430523A64A1}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Daily Goal
DefaultGroupName=Daily Goal
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=DailyGoal-Setup-{#AppVersion}-x64
SetupIconFile=..\src\DailyGoal\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion=2.5.0.0
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} 安装程序
VersionInfoCompany={#AppPublisher}

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Daily Goal"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\Daily Goal"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 Daily Goal"; Flags: nowait postinstall skipifsilent
