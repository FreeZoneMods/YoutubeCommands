using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatLoggers;

namespace ChatInteractiveCommands
{
    abstract class BaseLiveChatCommander
    {
        protected BaseLogger _logger;
        protected ProgramConfig _cfg;
        protected BaseLiveChatParser _parser;
        protected CommandsProcessing _processor;
        protected ResponseBuilder _responceBuilder;

        public abstract bool Initialize();

        public BaseLiveChatCommander(ProgramConfig cfg, BaseLogger logger)
        {
            _logger = logger;
            _cfg = cfg;
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
            Log("Chat command from '" + m.senderName + "' processed, status = '" + r.status + "'");

            string reply = _responceBuilder.BuildResponse(m.senderName, r.status);
            if (r.allow_response && (reply.Length > 0) && (reply.Length <= 150) && _cfg.IsChatRepliesEnabled())
            {
                _parser.SendLiveChatMessage(reply);
            }
        }


        public void PerformIteration()
        {
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

                    string cmd = ExtractChatCommandFromChatMessage(m);
                    if (cmd.Length > 0)
                    {
                        Log("Extracted chat command from user '"+m.senderName+"' (" + m.senderId+"), body '"+ cmd + "'");

                        if (!found_commands)
                        {
                            _processor.PrepareNewProcessingIteration();
                            found_commands = true;
                        }

                        _processor.AddCommandToCurrentIteration(cmd, m);
                    }
                }
                if (found_commands)
                {
                    Action<CommandsProcessing.CommandParseResult> cb = OnCommandProcessResult;
                    _processor.StartIteration(cb);
                }
            }
        }

        protected void Log(string msg, LogSeverity severity)
        {
            if (_logger != null)
            {
                _logger.Log(msg, severity);
            }
        }

        protected void Log(string msg)
        {
            Log(msg, LogSeverity.LogSeverityNormal);
        }
    }
}
