﻿using System;

using IniParser;
using IniParser.Model;
using System.Diagnostics;

namespace ChatInteractiveCommands
{
    class CommandsProcessing
    {
        private string _executable;
        private string _infile;
        private string _outfile;
        private int _timeout;
        private IniData _indata;
        private bool _allow_generic_fails_reply;

        private const string MAIN_SECTION = "main";
        private const string ITEM_SECTIONS_CNT = "items_count";
        private const string ITEM_SECTION_PREFIX = "item_";

        private const string ITEM_KEY_STATUS = "status";
        private const string ITEM_KEY_ALLOW_RESPONSE = "allow_response";
        private const string AVAILABLE_SCORES = "available_scores";
        private const string USED_SCORES = "used_scores";
        private const string MESSAGE_ID = "message_id";

        private const string PROCESSING_STATUS_GENERIC_FAIL = "generic_fail";

        public CommandsProcessing(ProgramConfig cfg)
        {
            _infile = cfg.GetProcessorInputPath();
            _outfile = cfg.GetProcessorOutputPath();
            _timeout = cfg.GetProcessorTimeout();
            _executable = cfg.GetProcessorExecutablePath();
            _allow_generic_fails_reply = cfg.AllowGenericFailsReply();

            _indata = null;
        }

        public void PrepareNewProcessingIteration()
        {
            _indata = new IniData();
            _indata[MAIN_SECTION][ITEM_SECTIONS_CNT] = "0";
        }

        public void AddCommandToCurrentIteration(string cmd, bool need_use_score, int available_scores, LiveChatMessageParams m, int message_id_in_buffer)
        {
            int cmdidx = Convert.ToInt32(_indata[MAIN_SECTION][ITEM_SECTIONS_CNT]);
            _indata[MAIN_SECTION][ITEM_SECTIONS_CNT] = Convert.ToString(cmdidx + 1);
            string item_section = ITEM_SECTION_PREFIX + Convert.ToString(cmdidx);

            string[] subs = cmd.Split(' ');
            string realcommand = (subs.Length > 0) ? subs[0] : cmd;

            _indata[item_section]["command"] = realcommand;

            for (int i = 1; i < subs.Length; ++i)
            {
                _indata[item_section]["arg_" + Convert.ToString(i)] = subs[i];
            }

            _indata[item_section]["user_nick"] = m.senderName.Replace('=', '_').Replace(';', '_');
            _indata[item_section]["user_id"] = m.senderId;
            _indata[item_section]["use_scores"] = need_use_score ? "1" : "0";
            _indata[item_section]["donation"] = m.with_donation ? "1" : "0";
            _indata[item_section][AVAILABLE_SCORES] = Convert.ToString(available_scores);
            _indata[item_section][MESSAGE_ID] = Convert.ToString(message_id_in_buffer);
        }

        public class CommandParseResult
        {
            public int id;
            public string status;
            public bool allow_response;
            public int initial_scores;
            public int used_scores;
        }

        internal void StartIteration(Action<CommandParseResult> parseResultCb)
        {
            int cnt = Convert.ToInt32(_indata[MAIN_SECTION][ITEM_SECTIONS_CNT]);
            if (cnt <= 0) return;

            var out_template = new IniData();
            for (int i = 0; i < cnt; ++i)
            {
                string item_section = ITEM_SECTION_PREFIX + Convert.ToString(i);
                out_template[item_section][ITEM_KEY_STATUS] = PROCESSING_STATUS_GENERIC_FAIL;
                out_template[item_section][ITEM_KEY_ALLOW_RESPONSE] = "0";
                out_template[item_section][USED_SCORES] = "0";
                out_template[item_section][MESSAGE_ID] = _indata[item_section][MESSAGE_ID];
            }

            var parser = new FileIniDataParser();
            parser.WriteFile(_infile, _indata);
            parser.WriteFile(_outfile, out_template);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;

            startInfo.FileName = _executable;
            startInfo.Arguments = "\"" + _infile + "\" \"" + _outfile + "\"";

            Process proc = new Process();
            proc.StartInfo = startInfo;
            try
            {
                proc.Start();
                if (_timeout <= 0)
                {
                    proc.WaitForExit();
                }
                else if (!proc.WaitForExit(_timeout))
                {
                    proc.Kill();
                }
            }
            catch { }

            var out_data = parser.ReadFile(_outfile);

            for (int i = 0; i < cnt; ++i)
            {
                string item_section = ITEM_SECTION_PREFIX + Convert.ToString(i);
                string status = PROCESSING_STATUS_GENERIC_FAIL;
                bool allow_response = false;
                int initial_scores = 0;
                int used_scores = 0;
                int message_id = 0;

                if (_indata[item_section] != null)
                {
                    if (_indata[item_section][AVAILABLE_SCORES] != null) Int32.TryParse(_indata[item_section][AVAILABLE_SCORES], out initial_scores);
                }

                if (out_data[item_section] != null)
                {
                    if (out_data[item_section][ITEM_KEY_STATUS] != null) status = out_data[item_section][ITEM_KEY_STATUS];
                    if (out_data[item_section][ITEM_KEY_ALLOW_RESPONSE] != null) bool.TryParse(out_data[item_section][ITEM_KEY_ALLOW_RESPONSE], out allow_response);
                    if (out_data[item_section][USED_SCORES] != null) Int32.TryParse(out_data[item_section][USED_SCORES], out used_scores);
                    if (out_data[item_section][MESSAGE_ID] != null) Int32.TryParse(out_data[item_section][MESSAGE_ID], out message_id);
                }

                CommandParseResult cpr = new CommandParseResult();
                cpr.id = message_id;
                cpr.status = (status != null && status.Length > 0) ? status : PROCESSING_STATUS_GENERIC_FAIL;
                cpr.allow_response = (status == PROCESSING_STATUS_GENERIC_FAIL) ? _allow_generic_fails_reply : allow_response;
                cpr.initial_scores = initial_scores;
                cpr.used_scores = used_scores;
                parseResultCb(cpr);
            }
        }
    }
}
