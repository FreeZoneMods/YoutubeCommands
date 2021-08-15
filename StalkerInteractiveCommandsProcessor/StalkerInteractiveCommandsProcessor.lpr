program StalkerDonateInteractiveAdapter;

uses IniFiles, dateutils, strutils, sysutils, windows;

type TCmdProcessor = function(ini:TIniFile; sect_name:string; cfg:TIniFile):string;

tagKEYBDINPUT_Custom = packed record
  wVk: WORD;
  wScan: WORD;
  dwFlags: DWORD;
  time: DWORD;
  dwExtraInfo: DWORD;
end;

TInput_Custom = packed record
  Itype:DWORD;
  ki:tagKEYBDINPUT_Custom;
  DUMMY_DWORD1:DWORD;
  DUMMY_DWORD2:DWORD;
end;
pTInput_Custom = ^TInput_Custom;

const
  MAIN_SECTION:string = 'main';
  LAST_CMD_TIME_KEY:string='last_cmd_time';
  TIMEOUT_KEY:string='timeout';
  COST_KEY:string = 'cost';
  USER_NICK_KEY = 'user_nick';
  DEFAULT_NICK:string = 'stalker';

function SendInput_Custom(cInputs:UINT; pInputs:pTInput_Custom; cbsize:cardinal):UINT; stdcall; external user32 name 'SendInput';

procedure KeyPressImitation(virtual_key_code:word);
var
  Inp: TInput_Custom;
  sc:word;

const
  KEYEVENTF_SCANCODE:word = $8;
  MAPVK_VK_TO_VSC:word = $0;
  INPUT_KEYBOARD:cardinal=$1;
begin
    sc:=MapVirtualKey(virtual_key_code, MAPVK_VK_TO_VSC);
    // press
    Inp.Itype := INPUT_KEYBOARD;
    Inp.ki.wScan := sc;
    Inp.ki.dwFlags := KEYEVENTF_SCANCODE;
    SendInput_Custom(1, @Inp, SizeOf(Inp));

    // release
    Inp.Itype := INPUT_KEYBOARD;
    Inp.ki.wScan := sc;
    Inp.ki.dwFlags := KEYEVENTF_SCANCODE + KEYEVENTF_KEYUP;
    SendInput_Custom(1, @Inp, SizeOf(Inp));
end;

procedure CreateDonateFile(nickname:string; amount:integer; cfg:TIniFile);
var
  f:textfile;
  donate_file_path:string;
  sleep_period, virtual_key_code:integer;

begin
  donate_file_path:=cfg.ReadString(MAIN_SECTION, 'donate_file_path', 'Donate_Last.txt');

  assignfile(f, donate_file_path);
  rewrite(f);
  writeln(f, nickname+' '+inttostr(amount)+' RUB');
  closefile(f);

  sleep_period:=cfg.ReadInteger(MAIN_SECTION, 'sleep_period', 0);
  if sleep_period > 0 then begin
    Sleep(sleep_period);
  end;

  virtual_key_code:=cfg.ReadInteger(MAIN_SECTION, 'press_key_code', VK_V);
  KeyPressImitation(virtual_key_code);
end;


function CheckTimeout(cfg:TIniFile; sect_name:string):boolean;
var
  last_command_run_time:string;
  cmd_timeout:integer;
begin
  result:=true;
  cmd_timeout:=cfg.ReadInteger(sect_name, TIMEOUT_KEY, 60);
  last_command_run_time:=cfg.ReadString(sect_name, LAST_CMD_TIME_KEY, '');

  if length(last_command_run_time) > 0 then begin
    try
      result:=(SecondsBetween(Now(), StrToDateTime(last_command_run_time)) > cmd_timeout);
    except
      result:=true;
    end;
  end;
end;
procedure UpdateLastLime(cfg:TIniFile; sect_name:string);
begin
  cfg.WriteString(sect_name, LAST_CMD_TIME_KEY, DateTimeToStr(Now()));
  cfg.UpdateFile();
end;

function DefaultCommandProcessor(ini_in:TIniFile; sect_name_in:string; cfg:TIniFile):string;
var
  nick:string;
  cost:integer;

  cmd, cmd_params_sect:string;
begin
  cmd:=ini_in.ReadString(sect_name_in, 'command' , '');
  nick:=ini_in.ReadString(sect_name_in, USER_NICK_KEY, DEFAULT_NICK);

  cmd:=Trim(cmd);
  cmd_params_sect:=cmd+'_command';

  cost:=cfg.ReadInteger(cmd_params_sect, COST_KEY, 0);
  if cost = 0 then begin
    result:='generic_fail';
  end else if CheckTimeout(cfg, cmd_params_sect) then begin
    CreateDonateFile(nick, cost, cfg);
    UpdateLastLime(cfg, cmd_params_sect);
    result:='success';
  end else begin
    result:='command_unavailable';
  end;
end;

function GetCommandProcessor(ini_in:TIniFile; sect_name_in:string; cfg:TIniFile):TCmdProcessor;
var
  cmd:string;
begin
  result:=nil;

  cmd:=ini_in.ReadString(sect_name_in, 'command' , '');
  cmd:=Trim(cmd);
  if length(cmd) = 0 then exit;


  if cfg.SectionExists(cmd+'_command') then begin
    result:=@DefaultCommandProcessor;
  end;
end;

procedure ParseCommands(infile:string; outfile:string; config:string);
var
  ini_cfg:TIniFile;
  ini_in:TIniFile;
  ini_out:TIniFile;

  can_run_cmd_now:boolean;
  i, cmd_count:integer;
  sect_name:string;
  cmd_status:string;
  allow_reply:boolean;
  cmdproc:TCmdProcessor;
begin
  ini_cfg:=TIniFile.Create(config);
  ini_in:=TIniFile.Create(infile);
  ini_out:=TIniFile.Create(outfile);
  try
    can_run_cmd_now:=CheckTimeout(ini_cfg, MAIN_SECTION);
    cmd_count:=ini_in.ReadInteger(MAIN_SECTION, 'items_count', 0);
    for i:=0 to cmd_count-1 do begin
      cmd_status:='generic_fail';
      allow_reply:=true;

      sect_name:='item_'+inttostr(i);
      if ini_in.SectionExists(sect_name) then begin
        cmdproc:=GetCommandProcessor(ini_in, sect_name, ini_cfg);
        if cmdproc = nil then begin
          cmd_status := 'unknown_command';
        end else if can_run_cmd_now then begin
          cmd_status:=cmdproc(ini_in, sect_name, ini_cfg);
          if cmd_status = 'success' then begin
            can_run_cmd_now:=false;
            UpdateLastLime(ini_cfg, MAIN_SECTION);
          end;
        end else begin
          cmd_status := 'command_unavailable';
        end;
      end;

      ini_out.WriteString(sect_name, 'status', cmd_status);
      ini_out.WriteString(sect_name, 'allow_response', booltostr(allow_reply, true));
    end;

    ini_out.UpdateFile();
  finally
    ini_cfg.Free;
    ini_in.Free;
    ini_out.Free;
  end;
end;

begin
  if ParamCount = 2 then begin
    ParseCommands(ParamStr(1), ParamStr(2), 'StalkerInteractiveCommandsProcessor.ini');
  end else begin
    ParseCommands('in.ini', 'out.ini', 'StalkerInteractiveCommandsProcessor.ini');
  end;
end.

