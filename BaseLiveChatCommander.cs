using ChatLoggers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatInteractiveCommands
{
    abstract class BaseLiveChatCommander
    {
        protected BaseLogger _logger;
        protected ProgramConfig _cfg;
        protected ProgramConfig.ChatService _chat_service_type;
        protected BaseLiveChatParser _parser;
        protected CommandsProcessing _processor;
        protected ResponseBuilder _responceBuilder;
        protected IScoresBank _userscores;

        public abstract bool Initialize();

        public BaseLiveChatCommander(ProgramConfig cfg, BaseLogger logger, IScoresBank scores, ProgramConfig.ChatService chat_service_type)
        {
            _logger = logger;
            _cfg = cfg;
            _chat_service_type = chat_service_type;
            _userscores = scores;
            _processor = new CommandsProcessing(_cfg);
            _responceBuilder = new ResponseBuilder(_cfg);
        }

        protected string ExtractChatCommandFromChatMessage(LiveChatMessageParams msg)
        {
            string res = "";

            if (msg.valid)
            {
                string prefix = _cfg.GetChatCommandPrefix();
                if ((msg.text.Length > prefix.Length) && (msg.text.Substring(0, prefix.Length) == prefix))
                {
                    res = msg.text.Substring(prefix.Length);
                }
            }

            return res;
        }

        private void OnCommandProcessResult(CommandsProcessing.CommandParseResult r)
        {
            var m = _parser.GetLiveChatMessageFromBuffer(r.id);
            Log("Chat command from '" + m.senderName + "' processed, status = '" + r.status + "', used scores " + Convert.ToString(r.used_scores));

            var ur = SUserRecord.GetEmpty();
            if (_userscores != null)
            {
                ur = _userscores.GetUserRecord(m.senderId);
                if (r.used_scores != 0 && ur.idstring.Length > 0)
                {
                    ur.scores -= r.used_scores;
                    _userscores.UpdateUser(m.senderId, m.senderName, ur.scores, false);
                }
            }

            string reply = _responceBuilder.BuildResponse(m.senderName, r.status, ur.scores);
            if (r.allow_response && (reply.Length > 0) && _cfg.IsChatRepliesEnabled())
            {
                _parser.AddLiveChatMessageToSendBuffer(reply);
            }
        }


        public void PerformIteration()
        {
            if (_parser == null)
            {
                Log("Commander iteration failed - parser is null! Maybe you forgot to call Initialize?", LogSeverity.LogSeverityError);
                return;
            }

            _parser.ClearSendLiveChatBuffer();

            int cnt = _parser.UpdateLiveChatMessageBuffer();
            if (cnt > 0)
            {
               Log("Arrived "+ cnt.ToString() + " new chat message(s)");
                bool found_commands = false;
                for (int i = 0; i < cnt; ++i)
                {
                    var m = _parser.GetLiveChatMessageFromBuffer(i);
                    if (!m.valid)
                    {
                        continue;
                    }

                    var ud = ExtractUserData(m);

                    string cmd = ExtractChatCommandFromChatMessage(m);
                    if (cmd.Length > 0)
                    {
                        Log("Extracted chat command from user '" + m.senderName + "' (" + m.senderId + "), body '" + cmd + "'");

                        if (!found_commands)
                        {
                            _processor.PrepareNewProcessingIteration();
                            found_commands = true;
                        }

                        _processor.AddCommandToCurrentIteration(cmd, _userscores != null, ud.scores, m, i);
                    }
                    else
                    {
                        ProcessRegularChatMessage(m, ud);
                    }
                }
                if (found_commands)
                {
                    Action<CommandsProcessing.CommandParseResult> cb = OnCommandProcessResult;
                    _processor.StartIteration(cb);
                }
                _parser.FlushSendLiveChatBuffer();
            }
        }

        protected SUserRecord ExtractUserData(LiveChatMessageParams m)
        {
            SUserRecord result = SUserRecord.GetEmpty();
            if (_userscores != null)
            {
                result = _userscores.GetUserRecord(m.senderId);
                if (result.idstring.Length == 0)
                {
                    Log("Registering new user " + m.senderName + " (" + m.senderId + ")");
                    _userscores.RegisterUser(m.senderId, m.senderName, _cfg.GetChatRegistrationBonus(_chat_service_type));
                    result = _userscores.GetUserRecord(m.senderId);
                }
            }            
            return result;
        }

        protected int GetAwardValueForChatMessage(LiveChatMessageParams m)
        {
            var min_len = _cfg.GetMinimalChatMessageLenForAward(_chat_service_type);
            if ((min_len > 0) && (min_len > m.text.Length))
            {
                return 0;
            }

            var min_syms = _cfg.GetMinimalSymbolsCountForAward(_chat_service_type);
            if (min_syms > 0)
            {
                Dictionary<char, bool> symbs = new Dictionary<char, bool>();

                for (int i = 0; i < m.text.Length; i++)
                {
                    if (!symbs.ContainsKey(m.text[i]))
                    {
                        symbs.Add(m.text[i], true);
                        if (symbs.Count >= min_syms) break;
                    }            
                }

                if (symbs.Count < min_syms)
                {
                    return 0;
                }
            }

            return _cfg.GetChatMessageAward(_chat_service_type);
        }

        protected void ProcessRegularChatMessage(LiveChatMessageParams m, SUserRecord ur)
        {
            if (_userscores != null)
            {
                var dailyBonus = _cfg.GetChatDailyBonus(_chat_service_type);
                if (dailyBonus != 0)
                {
                    TimeSpan timeDiff = DateTime.Now - ur.last_bonus_time;
                    if (timeDiff.TotalSeconds > _cfg.GetChatDailyPeriod(_chat_service_type))
                    {
                        Log("Daily bonus for user " + m.senderName + " (" + m.senderId + ")");
                        ur.scores += dailyBonus;
                        _userscores.UpdateUser(m.senderId, m.senderName, ur.scores, true);
                    }
                }

                var message_award = GetAwardValueForChatMessage(m);
                if (message_award != 0)
                {
                    ur.scores += message_award;
                    _userscores.UpdateUser(m.senderId, m.senderName, ur.scores, false);
                }
            }
        }

        protected void Log(string msg, LogSeverity severity)
        {
            if (_logger != null)
            {
                _logger.Log(_parser.GetName() + "Commander: " + msg, severity);
            }
        }

        protected void Log(string msg)
        {
            Log(msg, LogSeverity.LogSeverityNormal);
        }
    }
}
