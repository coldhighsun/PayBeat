; Inno Setup 7 script for PayBeat.
; Packages the self-contained win-x64 publish output into a per-user installer.
; Build the publish output first:
;   dotnet publish src/PayBeat.App/PayBeat.App.csproj -c Release -r win-x64 --self-contained -o publish-selfcontained/
; Then compile with:
;   ISCC.exe installer\PayBeat.iss /DAppVersion=1.2.3

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "PayBeat"
#define AppPublisher "coldhighsun"
#define AppUrl "https://github.com/coldhighsun/PayBeat"
#define AppExeName "PayBeat.exe"
#define PublishDir "..\publish-selfcontained"

[Setup]
AppId={{305F5C59-E76E-43F4-BB60-9C97994A151C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputBaseFilename=PayBeat-{#AppVersion}-setup-win-x64
OutputDir=..\installer-output
SetupIconFile=..\src\PayBeat.App\Resources\Icons\PayBeat.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\{#AppName}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  while CheckForMutexes('PayBeat_SingleInstance') do
  begin
    if MsgBox('{#AppName} is currently running. Please close it before continuing setup.', mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  while CheckForMutexes('PayBeat_SingleInstance') do
  begin
    if MsgBox('{#AppName} is currently running. Please close it before continuing uninstall.', mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
  end;
end;
