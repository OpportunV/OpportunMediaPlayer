#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\OMP.Ui\bin\Release\net10.0\win-x64\publish"
#endif

#define MyAppName "Opportun Media Player"
#define MyAppExeName "OMP.exe"
#define MyAppPublisher "OpportunV"
#define MyAppURL "https://github.com/OpportunV/OpportunMediaPlayer"

[Setup]
; Do not change this GUID between releases - it is how Windows recognizes an
; upgrade (same install location, same Add/Remove Programs entry) instead of
; a parallel install.
AppId={{46022E31-6125-4FA3-AE9B-E60EBA05B4A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\OMP
DefaultGroupName=OMP
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename=OMP-Setup-{#MyAppVersion}
OutputDir=..\..\artifacts
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplicationsFilter={#MyAppExeName}
DisableProgramGroupPage=yes
SetupIconFile=..\..\OMP.Ui\Assets\app-icon.ico
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

; Inno Setup's built-in .isl files only translate its own standard wizard text
; (Next/Back/Ready/etc.) - anything we wrote ourselves (task descriptions, the
; launch checkbox) needs its own translation here, referenced via {cm:...}.
[CustomMessages]
english.TaskDesktopIcon=Create a desktop shortcut
english.TaskFileAssoc=Associate common video and audio files with OMP
english.GroupShortcuts=Additional shortcuts:
english.GroupFileAssoc=File associations:
english.LaunchProgram=Launch OMP
russian.TaskDesktopIcon=Создать ярлык на рабочем столе
russian.TaskFileAssoc=Связать обычные видео- и аудиофайлы с OMP
russian.GroupShortcuts=Дополнительные ярлыки:
russian.GroupFileAssoc=Сопоставление файлов:
russian.LaunchProgram=Запустить OMP

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktopIcon}"; GroupDescription: "{cm:GroupShortcuts}"
Name: "fileassoc"; Description: "{cm:TaskFileAssoc}"; GroupDescription: "{cm:GroupFileAssoc}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\OMP"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\OMP"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCR; Subkey: "OMP.MediaFile"; ValueType: string; ValueName: ""; ValueData: "OMP Media File"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: "OMP.MediaFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKCR; Subkey: "OMP.MediaFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKCR; Subkey: ".mp4"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".mkv"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".avi"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".webm"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".mov"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".flv"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".wmv"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".mp3"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".flac"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".wav"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".ogg"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".m4a"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".aac"; ValueType: string; ValueName: ""; ValueData: "OMP.MediaFile"; Flags: uninsdeletevalue; Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent

[Code]
procedure SHChangeNotify(wEventId: Longint; uFlags: Longint; dwItem1: Longint; dwItem2: Longint);
  external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Refreshes Explorer's file-type icons after registering associations, so
    the new icon shows up without requiring a logoff/restart. }
  if CurStep = ssPostInstall then
    SHChangeNotify($08000000, $0000, 0, 0);
end;
