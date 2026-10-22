; Open Note installer for GitHub downloads (the Microsoft Store uses the MSIX instead).
; Per-user install: no admin prompt, nothing else to download (the .NET runtime is included).
; Built by build\package.ps1:  ISCC /DAppVersion=1.0.0 /DSourceDir=<publish folder> /DOutputDir=<dir> OpenNote.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\publish\OpenNote"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\publish"
#endif

[Setup]
AppId={{8F4C2B1E-6A3D-4E7F-9B21-5C0D7A9E3F14}
AppName=Open Note
AppVersion={#AppVersion}
AppVerName=Open Note {#AppVersion}
AppPublisher=Open Note contributors
AppPublisherURL=https://github.com/nhaajtdajt/open-note
AppSupportURL=https://github.com/nhaajtdajt/open-note/issues
DefaultDirName={localappdata}\Programs\Open Note
DefaultGroupName=Open Note
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
DisableWelcomePage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\OpenNote.exe
UninstallDisplayName=Open Note
SetupIconFile=..\..\src\OpenNote\Assets\app.ico
OutputDir={#OutputDir}
OutputBaseFilename=OpenNote-Setup-{#AppVersion}-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=no

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; An update replaces the whole program folder, so files dropped by a newer .NET don't linger.
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{userprograms}\Open Note"; Filename: "{app}\OpenNote.exe"

; "Start with Windows" is turned on by the app itself on its first run (and stays off once the
; user turns it off in the tray menu), so an update never overrides that choice.

[Run]
Filename: "{app}\OpenNote.exe"; Description: "Start Open Note"; Flags: nowait postinstall

[Code]
// Ask a running copy to save and quit before its files are replaced or removed.
procedure StopRunningApp();
var
  Code: Integer;
begin
  if FileExists(ExpandConstant('{app}\OpenNote.exe')) then
    Exec(ExpandConstant('{app}\OpenNote.exe'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;

// Remove the "Start with Windows" entry the app created. Notes in %LOCALAPPDATA%\OpenNote are kept.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OpenNote');
end;
