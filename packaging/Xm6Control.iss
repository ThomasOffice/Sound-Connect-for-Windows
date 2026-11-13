; Sound Connect for Windows installer (Inno Setup 6.7+)
; Build with: pwsh packaging/build-installer.ps1

#ifndef AppVersion
  #define AppVersion "1.0.3"
#endif
#ifndef StagingDir
  #define StagingDir "..\artifacts\release\publish-1.0.3"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\release"
#endif

#define AppName "Sound Connect for Windows"
#define AppPublisher "ThomasOffice"
#define AppURL "https://github.com/ThomasOffice/Sound-Connect-for-Windows"
#define AppExeName "Xm6Control.App.exe"

[Setup]
AppId={{F1E85F60-C12E-4686-80BD-BB71A8316C79}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir={#OutputDir}
OutputBaseFilename=Xm6Control-{#AppVersion}-win-x64-setup
SetupIconFile=..\src\Xm6Control.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\app.ico"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsAppRunning(): Boolean;
var
  ResultCode: Integer;
  TempFile: string;
  Lines: TArrayOfString;
  I: Integer;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\xm6-tasklist.txt');
  DeleteFile(TempFile);
  if Exec(
    ExpandConstant('{cmd}'),
    '/c tasklist /FI "IMAGENAME eq Xm6Control.App.exe" /NH > "' + TempFile + '" 2>nul',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    if LoadStringsFromFile(TempFile, Lines) then
    begin
      for I := 0 to GetArrayLength(Lines) - 1 do
      begin
        if Pos('Xm6Control.App.exe', Lines[I]) > 0 then
        begin
          Result := True;
          Break;
        end;
      end;
    end;
    DeleteFile(TempFile);
  end;
end;

function CloseRunningApp(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{cmd}'),
    '/c taskkill /IM Xm6Control.App.exe /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  Sleep(1200);
  Result := not IsAppRunning();
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not IsAppRunning() then
    Exit;

  if WizardSilent() or
     (MsgBox(
        'Sound Connect for Windows is running. Setup must close it before continuing.' + #13#10 + #13#10 +
        'Close the application now?',
        mbConfirmation,
        MB_YESNO) = IDYES) then
  begin
    if not CloseRunningApp() then
      Result := 'The running application could not be closed. Exit it from the notification area and run Setup again.';
  end
  else
    Result := 'Exit Sound Connect for Windows from the notification area, then run Setup again.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  if not IsAppRunning() then
    Exit;

  if not (UninstallSilent() or
          (MsgBox(
             'Sound Connect for Windows is running. Uninstall must close it before continuing.' + #13#10 + #13#10 +
             'Close the application now?',
             mbConfirmation,
             MB_YESNO) = IDYES)) then
  begin
    MsgBox(
      'Exit Sound Connect for Windows from the notification area, then uninstall again.',
      mbInformation,
      MB_OK);
    Result := False;
    Exit;
  end;

  if not CloseRunningApp() then
  begin
    MsgBox('The running application could not be closed.', mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Xm6Control');
end;
