; Inno Setup 6.3 or newer script for Piece of Cake.
; Expects the published build (a folder of files) in ..\publish\ with PieceOfCake.exe (see build.bat).
; Compile with: installer\build-installer.bat, or open this file in Inno Setup and press Ctrl+F9.
; Save this file as UTF-8 with BOM, otherwise Polish characters are garbled.

#define MyAppName "Piece of Cake"
#define MyAppExeName "PieceOfCake.exe"
#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

[Setup]
AppId={{C6CF1DC8-C25C-4C34-9F53-51EF0989B28C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Installs for the current user without admin rights by default; the user can switch to "all users" in a dialog.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\installer-output
OutputBaseFilename=PieceOfCake-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\Assets\app.ico
; Same name as the mutex used by the app, so Setup asks the user to close a running instance first.
AppMutex=Local\PieceOfCake.SingleInstance
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
polish.AutostartTask=Uruchamiaj {#MyAppName} z systemem
polish.StartupGroup=Autostart:
polish.RemoveSettingsPrompt=Czy usunąć także ustawienia i sparowanie z mostkiem Hue (%1)?%n%nWybierz „Nie”, jeśli planujesz zainstalować program ponownie.
english.AutostartTask=Start {#MyAppName} with Windows
english.StartupGroup=Startup:
english.RemoveSettingsPrompt=Also delete settings and the Hue bridge pairing (%1)?%n%nChoose "No" if you plan to reinstall the program.

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:StartupGroup}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Same value name and format as the in-app "start with Windows" switch, so both stay consistent.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PieceOfCake"; ValueData: """{app}\{#MyAppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "PieceOfCake"; Flags: deletevalue; Tasks: not autostart
; Autostart entry left by the old name of the app (HuePanel).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "HuePanel"; Flags: deletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  { Remove the autostart entry even if it was created later by the app itself. }
  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PieceOfCake');

  SettingsDir := ExpandConstant('{userappdata}\PieceOfCake');
  if (not UninstallSilent) and DirExists(SettingsDir) then
  begin
    if MsgBox(FmtMessage(CustomMessage('RemoveSettingsPrompt'), [SettingsDir]),
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(SettingsDir, True, True, True);
  end;
end;
