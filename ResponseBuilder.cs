﻿using System;

using IniParser;
using IniParser.Model;

namespace ChatInteractiveCommands
{
    class ResponseBuilder
    {
        private IniData _localization;
        private Random _rnd;
        public ResponseBuilder(ProgramConfig cfg)
        {
            _localization = new IniData();
            _rnd = new Random();

            var parser = new FileIniDataParser();
            _localization = parser.ReadFile(cfg.GetLocalizationConfigPath());
        }

        public string BuildResponse(string nickName, string status, int initial_scores, int remained_scores)
        {
            string res = "";
            if (_localization[status] != null)
            {
                if (!Convert.ToBoolean(_localization[status]["is_silent"]))
                {

                    int templates_cnt = 0;
                    if (int.TryParse(_localization[status]["templates_count"], out templates_cnt) && templates_cnt > 0)
                    {
                        int template_id = _rnd.Next(templates_cnt);
                        string template = _localization[status]["template_" + Convert.ToString(template_id)];
                        if (template != null && template.Length > 0)
                        {
                            res = string.Format(template, nickName, initial_scores, remained_scores);
                        }
                    }
                }
            }

            return res;
        }
    }
}
