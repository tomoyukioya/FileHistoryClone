; Inno Setup script for FileHistoryClone
; Build:  ISCC.exe /DSourceDir="<publish output folder>" /DMyAppVersion=1.0.1 installer\FileHistoryClone.iss
; Per-user install (no admin needed); bundles the self-contained single-file build.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.1"
#endif
#ifndef SourceDir
  #define SourceDir "..\FileHistory\bin\Release\net8.0-windows\win-x64\publish"
#endif

#define MyAppName "FileHistoryClone"
#define MyAppPublisher "Tomoyuki Ohya"
#define MyAppURL "https://github.com/tomoyukioya/FileHistoryClone"
#define MyAppExeName "FileHistory.exe"

[Setup]
AppId={{B9E8B7A6-3C2D-4E1F-9A0B-6D5C4E3F2A1B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename=FileHistoryCloneSetup-{#MyAppVersion}
SetupIconFile={#SourceDir}\FileHistoryCloneMainIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

; Force Japanese-capable fonts everywhere so no wizard text renders as boxes.
; (The default Welcome/Title/Copyright fonts are Verdana/Arial, which lack CJK glyphs.)
[LangOptions]
japanese.DialogFontName=Meiryo UI
japanese.DialogFontSize=9
japanese.WelcomeFontName=Meiryo UI
japanese.TitleFontName=Meiryo UI
japanese.CopyrightFontName=Meiryo UI

[CustomMessages]
english.AutoStartDesc=Start FileHistoryClone automatically when I sign in
japanese.AutoStartDesc=サインイン時に FileHistoryClone を自動起動する

[Tasks]
; Checked by default (a backup tool should keep running across reboots).
Name: "autostart"; Description: "{cm:AutoStartDesc}"

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\FileHistoryCloneMainIcon.ico"; DestDir: "{app}"; Flags: ignoreversion
; Keep the user's settings on upgrade — only lay down the default when absent.
Source: "{#SourceDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Edit {#MyAppName} settings"; Filename: "{app}\appsettings.json"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; Auto-start at sign-in when the task is selected. Removed on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FileHistoryClone"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// Stop any running FileHistoryClone instance so its files can be replaced/removed.
// (Uses PowerShell Stop-Process instead of taskkill: taskkill depends on the WMI
//  service and hangs forever when WMI is down, e.g. in Windows Sandbox.)
procedure KillRunningApp;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
       '-NoProfile -Command Stop-Process -Name FileHistory -Force -ErrorAction SilentlyContinue',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Before an install/upgrade: close the running instance so the exe isn't locked.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  KillRunningApp;
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop the running tray app so its files can be removed.
    KillRunningApp;
    // Remove the auto-start entry the app may create at runtime ("Start with Windows"),
    // which the uninstaller would otherwise leave behind as a dead HKCU\...\Run value.
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FileHistoryClone');
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    // The uninstaller runs from inside {app}, so Windows can leave the now-empty
    // folder behind. Remove it once the uninstaller has exited (rmdir only deletes
    // the folder if it is empty, so this is safe).
    Exec(ExpandConstant('{cmd}'),
         '/C ping -n 3 127.0.0.1 >nul & rmdir "' + ExpandConstant('{app}') + '"',
         '', SW_HIDE, ewNoWait, ResultCode);
  end;
end;
