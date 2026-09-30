; Inno Setup script for Razer Battery Tray
#define MyAppName "Razer Battery Tray"
#define MyAppVersion "1.0.0"
#define MyAppExe "RazerBatteryTray.exe"

[Setup]
AppId={{79F53E56-5A7E-4A1C-B077-FA8DD0169B5A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Personal
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=installer-output
OutputBaseFilename=RazerBatteryTray-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExe}

[Tasks]
Name: "startup"; Description: "Start {#MyAppName} when I sign in to Windows"; GroupDescription: "Options:"

[Files]
Source: "publish\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure StopApp;
var
  ResultCode: Integer;
begin
  { The app has no window, so close it by name before installing or uninstalling. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopApp;
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopApp;
end;
