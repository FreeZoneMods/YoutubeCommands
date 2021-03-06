using IniParser;
using IniParser.Model;

namespace ChatInteractiveCommands
{
    class ProgramConfig
    {
        protected IniData _data;
        public ProgramConfig(string path)
        {
            var parser = new FileIniDataParser();
            _data = parser.ReadFile(path);
        }

        private string GetData(string section, string key)
        {
            return _data[section][key];
        }

        private int GetIntDef(string section, string key, int def)
        {
            string d = GetData(section, key);
            int n;
            if (!int.TryParse(d, out n))
            {
                n = def;
            }
            return n;
        }

        private bool GetBoolDef(string section, string key, bool def)
        {
            string d = GetData(section, key);
            bool n;
            if (!bool.TryParse(d, out n))
            {
                n = def;
            }
            return n;
        }

        private string GetStringDef(string section, string key, string def)
        {
            string d = GetData(section, key);
            if (d == null || d.Length == 0)
            {
                d = def;
            }
            return d;
        }

        private string GetTrovoSettingsSectionName()
        {
            return "trovo_settings";
        }

        private string GetYoutubeSettingsSectionName()
        {
            return "youtube_settings";
        }

        private string GetProgramSettingsSectionName()
        {
            return "main";
        }

        ////////////////////////////////////////////////////////////////////////////////////////////
        public bool IsYoutubeParserEnabled()
        {
            return GetBoolDef(GetYoutubeSettingsSectionName(), "use_youtube_parser", true);
        }

        public bool IsTrovoParserEnabled()
        {
            return GetBoolDef(GetTrovoSettingsSectionName(), "use_trovo_parser", true);
        }

        public int GetUpdateInterval()
        {
            return GetIntDef(GetProgramSettingsSectionName(), "update_interval", 10000);
        }

        public string GetGoogleOAuth2JsonPath()
        {
            return GetStringDef(GetYoutubeSettingsSectionName(), "youtube_client_secrets_path", "youtube_client_secrets.json");
        }

        public string GetTrovoOAuth2JsonPath()
        {
            return GetStringDef(GetTrovoSettingsSectionName(), "trovo_client_secrets_path", "trovo_client_secrets_path.json");
        }

        public string GetChatCommandPrefix()
        {
            return GetStringDef(GetProgramSettingsSectionName(), "chat_command_prefix", "!");
        }

        public string GetProcessorInputPath()
        {
            return GetStringDef(GetProgramSettingsSectionName(), "processor_input", "processor_input.ini");
        }

        public string GetProcessorOutputPath()
        {
            return GetStringDef(GetProgramSettingsSectionName(), "processor_output", "processor_output.ini");
        }

        public string GetProcessorExecutablePath()
        {
            return GetStringDef(GetProgramSettingsSectionName(), "processor_executable", "processor.exe");
        }

        public int GetProcessorTimeout()
        {
            return GetIntDef(GetProgramSettingsSectionName(), "processor_timeout", -1);
        }

        public bool IsChatRepliesEnabled()
        {
            return GetBoolDef(GetProgramSettingsSectionName(), "chat_replies_enabled", false);
        }

        public bool AllowGenericFailsReply()
        {
            return GetBoolDef(GetProgramSettingsSectionName(), "allow_generic_fails_reply", false);
        }

        public string GetLocalizationConfigPath()
        {
            return GetStringDef(GetProgramSettingsSectionName(), "locale_config", "localization.ini");
        }

        public int GetLogLevel()
        {
            return GetIntDef(GetProgramSettingsSectionName(), "log_severity", 1);
        }
    }
}
