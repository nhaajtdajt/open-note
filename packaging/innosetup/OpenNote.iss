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
; The program folder goes on the user's PATH so "opennote-ai" (the command AI agents use) works in any terminal.
ChangesEnvironment=yes
; .onote files (a note saved with "Save as file…") open in Open Note with a double-click.
ChangesAssociations=yes

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; An update replaces the whole program folder, so files dropped by a newer .NET don't linger.
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{userprograms}\Open Note"; Filename: "{app}\OpenNote.exe"

; "Start with Windows" is turned on by the app itself on its first run (and stays off once the
; user turns it off in the tray menu), so an update never overrides that choice.

[Registry]
Root: HKCU; Subkey: "Software\Classes\.onote"; ValueType: string; ValueName: ""; ValueData: "OpenNote.Note"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\OpenNote.Note"; ValueType: string; ValueName: ""; ValueData: "Open Note note"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\OpenNote.Note\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\OpenNote.exe,0"
Root: HKCU; Subkey: "Software\Classes\OpenNote.Note\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\OpenNote.exe"" ""%1"""
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsPath(ExpandConstant('{app}'))

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

// True when the folder isn't on the user's PATH yet (so an update doesn't add it twice).
function NeedsPath(Folder: String): Boolean;
var
  Path: String;
begin
  if not RegQueryStringValue(HKCU, 'Environment', 'Path', Path) then
    Result := True
  else
    Result := Pos(';' + Uppercase(Folder) + ';', ';' + Uppercase(Path) + ';') = 0;
end;

// Takes the program folder off the user's PATH again.
procedure RemoveFromPath(Folder: String);
var
  Path: String;
  P: Integer;
begin
  if not RegQueryStringValue(HKCU, 'Environment', 'Path', Path) then
    exit;
  Path := ';' + Path + ';';
  P := Pos(';' + Uppercase(Folder) + ';', Uppercase(Path));
  if P = 0 then
    exit;
  Delete(Path, P, Length(Folder) + 1);
  Path := Copy(Path, 2, Length(Path) - 2);
  RegWriteExpandStringValue(HKCU, 'Environment', 'Path', Path);
end;

// Remove the "Start with Windows" entry the app created and the PATH entry. Notes in %LOCALAPPDATA%\OpenNote are kept.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OpenNote');
    RemoveFromPath(ExpandConstant('{app}'));
  end;
end;
