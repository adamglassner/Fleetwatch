; Inno Setup script for the Fleetwatch Windows agent.
; Compiled by the CI workflow (or locally with Inno Setup 6) into
; FleetwatchAgentSetup.exe.
;
; It installs the self-contained agent exe, writes the server URL and
; enrollment code into appsettings.json, and registers + starts the
; "FleetwatchAgent" Windows service.
;
; The server URL and enrollment code can be passed at install time:
;   FleetwatchAgentSetup.exe /SERVERURL="https://fw.example.com" /ENROLLTOKEN="abc123" /VERYSILENT
; or typed into the wizard.

#define AppName "Fleetwatch Agent"
#define AppVersion "0.1.0"
#define ServiceName "FleetwatchAgent"
#define ExeName "FleetwatchAgent.exe"

[Setup]
AppId={{8E9B2A41-6C3D-4E5F-9A1B-FLEETWATCH01}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Fleetwatch
DefaultDirName={autopf}\Fleetwatch Agent
DefaultGroupName=Fleetwatch
DisableProgramGroupPage=yes
OutputBaseFilename=FleetwatchAgentSetup
OutputDir=..\Output
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern

[Files]
; The published single-file exe (CI places it in publish\ before compiling).
Source: "..\publish\{#ExeName}"; DestDir: "{app}"; Flags: ignoreversion
; appsettings.json is written in [Code] after install so we can inject settings.

[Icons]
Name: "{group}\Uninstall Fleetwatch Agent"; Filename: "{uninstallexe}"

[Run]
; Register the service (idempotent-ish: delete first, ignore errors).
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\{#ExeName}"" start= auto DisplayName= ""Fleetwatch Agent"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""Keeps this computer updated and lets your family admin help remotely (Fleetwatch)."""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failure {#ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopSvc"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DelSvc"

[Code]
var
  ServerPage: TInputQueryWizardPage;

function GetParam(const Name: String): String;
var
  I: Integer;
  Prefix: String;
  S: String;
begin
  Result := '';
  Prefix := '/' + Name + '=';
  for I := 1 to ParamCount do
  begin
    S := ParamStr(I);
    if Pos(Uppercase(Prefix), Uppercase(S)) = 1 then
      Result := Copy(S, Length(Prefix) + 1, MaxInt);
  end;
end;

procedure InitializeWizard;
begin
  ServerPage := CreateInputQueryPage(wpWelcome,
    'Fleetwatch setup',
    'Where should this computer report to?',
    'Enter the address of your Fleetwatch server and the enrollment code you generated for this computer.');
  ServerPage.Add('Server address (e.g. https://fleetwatch.example.com):', False);
  ServerPage.Add('Enrollment code:', False);
  ServerPage.Values[0] := GetParam('SERVERURL');
  ServerPage.Values[1] := GetParam('ENROLLTOKEN');
end;

function JsonEscape(const S: String): String;
var
  R: String;
begin
  R := S;
  StringChangeEx(R, '\', '\\', True);
  StringChangeEx(R, '"', '\"', True);
  Result := R;
end;

procedure WriteAppSettings;
var
  AppDir, FilePath, ServerUrl, Token, Json: String;
begin
  AppDir := ExpandConstant('{app}');
  FilePath := AppDir + '\appsettings.json';

  ServerUrl := ServerPage.Values[0];
  Token := ServerPage.Values[1];

  Json :=
    '{' + #13#10 +
    '  "Fleetwatch": {' + #13#10 +
    '    "ServerUrl": "' + JsonEscape(ServerUrl) + '",' + #13#10 +
    '    "EnrollmentToken": "' + JsonEscape(Token) + '",' + #13#10 +
    '    "CheckinIntervalSeconds": 300,' + #13#10 +
    '    "DeviceName": ""' + #13#10 +
    '  },' + #13#10 +
    '  "Logging": { "LogLevel": { "Default": "Information" } }' + #13#10 +
    '}' + #13#10;

  if not SaveStringToFile(FilePath, Json, False) then
    MsgBox('Could not write settings file. The agent may not start.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    // Stop/remove any previous service before copying the new exe.
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
  if CurStep = ssPostInstall then
    WriteAppSettings;
end;
