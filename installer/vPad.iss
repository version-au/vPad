; vPad setup (Inno Setup 6), like vTerm's. Packages dist\vPad.exe - build it first with build.cmd (which also runs this).
;
; Make the setup:  "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\vPad.iss
; It lands in dist\setup\vPad-setup.exe - the same name in every release, so a web page can link to the latest (the version is read from dist\vPad.exe).
;
; Installs for the current user only (no admin): %LOCALAPPDATA%\Programs\vPad. Settings and the log stay in %APPDATA%\vPad,
; so an existing setup carries straight over. Everything the options change is under HKCU and is undone by the uninstaller.

#define SourceExe "..\dist\vPad.exe"
#if !FileExists(SourceExe)
  #error dist\vPad.exe is missing - run build.cmd first
#endif
; "0.1.0.0" -> "0.1.0"
#define AppVer RemoveFileExt(GetVersionNumbersString(SourceExe))

[Setup]
AppId={{03F65620-3C49-4AC0-A0CF-4B60DF14E4F0}
AppName=vPad
AppVersion={#AppVer}
AppVerName=vPad {#AppVer}
AppPublisher=V
VersionInfoVersion={#AppVer}
DefaultDirName={autopf}\vPad
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
; every running vPad holds this (Program.cs): setup and uninstall ask you to close it first
AppMutex=vPad.Running
SetupIconFile=..\vpad.ico
UninstallDisplayIcon={app}\vPad.exe
UninstallDisplayName=vPad
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=..\dist\setup
OutputBaseFilename=vPad-setup
; a newer setup over an older one: keeps the options picked last time
UsePreviousTasks=yes
ChangesAssociations=yes

[Messages]
SetupAppRunningError=vPad is open. Close its windows, then click OK to carry on.
UninstallAppRunningError=vPad is open. Close its windows, then click OK to carry on.

[Tasks]
Name: "editwith"; Description: "Add ""Edit with vPad"" to the right-click menu of files"
Name: "desktopicon"; Description: "A shortcut on the desktop"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\vPad"; Filename: "{app}\vPad.exe"
Name: "{autodesktop}\vPad"; Filename: "{app}\vPad.exe"; Tasks: desktopicon

[Registry]
; "Edit with vPad" on every file
Root: HKCU; Subkey: "Software\Classes\*\shell\vpad.edit"; ValueType: string; ValueName: ""; ValueData: "Edit with vPad"; Flags: uninsdeletekey; Tasks: editwith
Root: HKCU; Subkey: "Software\Classes\*\shell\vpad.edit"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\vPad.exe"",0"; Tasks: editwith
Root: HKCU; Subkey: "Software\Classes\*\shell\vpad.edit\command"; ValueType: string; ValueName: ""; ValueData: """{app}\vPad.exe"" ""%1"""; Tasks: editwith
; in "Open with" (and Settings > Default apps) for text files
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "vPad"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\vPad.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".txt"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".md"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".log"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".json"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".yml"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".yaml"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".ini"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".conf"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".cfg"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".toml"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".env"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".xml"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".csv"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".ps1"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".sh"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".py"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".cmd"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\vPad.exe\SupportedTypes"; ValueType: string; ValueName: ".bat"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\.txt\OpenWithList\vPad.exe"; ValueType: string; ValueName: ""; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithList\vPad.exe"; ValueType: string; ValueName: ""; ValueData: ""; Flags: uninsdeletekey

[Run]
Filename: "{app}\vPad.exe"; Description: "Start vPad now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; left by the updater next to the exe
Type: files; Name: "{app}\vPad.new.exe"
Type: files; Name: "{app}\vPad.old.exe"
Type: files; Name: "{app}\vPad.failed.exe"
Type: files; Name: "{app}\vpad-write-test.tmp"
Type: dirifempty; Name: "{app}"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent() and DirExists(ExpandConstant('{userappdata}\vPad')) then
  begin
    if MsgBox('Also delete your vPad settings (options, the files open last time) and log?' + #13#10 + #13#10 +
              ExpandConstant('{userappdata}\vPad') + #13#10 + #13#10 +
              'Keep them (No) if you might install vPad again - it picks up where you left off.',
              mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{userappdata}\vPad'), True, True, True);
  end;
end;
