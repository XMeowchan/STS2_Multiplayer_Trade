#ifndef AppName
  #define AppName "STS2 Multiplayer Trade"
#endif

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef AppPublisher
  #define AppPublisher "XMeow"
#endif

#ifndef ModId
  #define ModId "Sts2MultiplayerTrade"
#endif

#ifndef PayloadDir
  #define PayloadDir "..\dist\installer\payload"
#endif

[Setup]
AppId={{0B268A32-B4BC-4C18-B020-BB7A7D0A8494}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
CreateAppDir=no
Uninstallable=no
OutputDir=..\dist\installer\output
OutputBaseFilename={#ModId}-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PayloadDir}\{#ModId}\*"; DestDir: "{code:GetTargetModDir}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: filesandordirs; Name: "{code:GetTargetModDir}\_update_runtime"
Type: files; Name: "{code:GetTargetModDir}\mod_manifest.json"

[Code]
var
  GameDirPage: TInputDirWizardPage;
  ResolvedGameDir: string;

function NormalizeDir(const Value: string): string;
begin
  Result := RemoveBackslashUnlessRoot(Trim(Value));
end;

function PathJoin(const BasePath, ChildPath: string): string;
begin
  Result := AddBackslash(BasePath) + ChildPath;
end;

function IsNumericKey(const Value: string): Boolean;
var
  Index: Integer;
begin
  Result := Value <> '';
  if not Result then
    Exit;

  for Index := 1 to Length(Value) do
  begin
    if (Value[Index] < '0') or (Value[Index] > '9') then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function ExtractVdfValue(const Line: string): string;
var
  Remaining: string;
  Key: string;
  Value: string;
  QuotePos: Integer;
begin
  Result := '';
  Remaining := Trim(Line);
  if Remaining = '' then
    Exit;

  if Remaining[1] <> '"' then
    Exit;

  Delete(Remaining, 1, 1);
  QuotePos := Pos('"', Remaining);
  if QuotePos = 0 then
    Exit;

  Key := Copy(Remaining, 1, QuotePos - 1);
  Delete(Remaining, 1, QuotePos);
  Remaining := Trim(Remaining);
  if Remaining = '' then
    Exit;

  if Remaining[1] <> '"' then
    Exit;

  Delete(Remaining, 1, 1);
  QuotePos := Pos('"', Remaining);
  if QuotePos = 0 then
    Exit;

  Value := Copy(Remaining, 1, QuotePos - 1);
  if (CompareText(Key, 'path') = 0) or IsNumericKey(Key) then
  begin
    Result := Value;
    StringChangeEx(Result, '\\', '\', True);
  end;
end;

function IsSts2GameDir(const Candidate: string): Boolean;
var
  Normalized: string;
begin
  Normalized := NormalizeDir(Candidate);
  if Normalized = '' then
  begin
    Result := False;
    Exit;
  end;

  Result := DirExists(Normalized);
  if Result then
    Result := FileExists(PathJoin(Normalized, 'SlayTheSpire2.exe'));
end;

function FindSts2InLibraryRoot(const LibraryRoot: string): string;
var
  CommonDir: string;
  Candidate: string;
begin
  Result := '';
  if LibraryRoot = '' then
    Exit;

  CommonDir := PathJoin(NormalizeDir(LibraryRoot), 'steamapps\common');
  if not DirExists(CommonDir) then
    Exit;

  Candidate := PathJoin(CommonDir, 'Slay the Spire 2');
  if IsSts2GameDir(Candidate) then
    Result := Candidate;
end;

function FindSteamRootFromRegistry(): string;
var
  Candidate: string;
begin
  Result := '';
  if RegQueryStringValue(HKCU, 'Software\Valve\Steam', 'InstallPath', Candidate) and DirExists(Candidate) then
    Result := NormalizeDir(Candidate);
end;

function FindDefaultGameDir(): string;
var
  SteamRoot: string;
begin
  Result := '';
  SteamRoot := FindSteamRootFromRegistry();
  if SteamRoot <> '' then
    Result := FindSts2InLibraryRoot(SteamRoot);
end;

function GetTargetModDir(Value: string): string;
begin
  Result := PathJoin(PathJoin(ResolvedGameDir, 'mods'), '{#ModId}');
end;

procedure InitializeWizard;
begin
  ResolvedGameDir := FindDefaultGameDir();
  GameDirPage := CreateInputDirPage(
    wpWelcome,
    '选择 Slay the Spire 2 安装目录',
    '安装器会把 Mod 复制到游戏目录下的 mods 文件夹。',
    '请确认目录下存在 SlayTheSpire2.exe。',
    False,
    ''
  );
  GameDirPage.Add('');
  if ResolvedGameDir <> '' then
    GameDirPage.Values[0] := ResolvedGameDir;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = GameDirPage.ID then
  begin
    ResolvedGameDir := NormalizeDir(GameDirPage.Values[0]);
    if not IsSts2GameDir(ResolvedGameDir) then
    begin
      MsgBox('请选择正确的 Slay the Spire 2 安装目录。', mbError, MB_OK);
      Result := False;
    end;
  end;
end;
