﻿using ChatLoggers;

namespace ChatInteractiveCommands
{
    public class LiveChatMessageParams
    {
        public bool valid = false;
        public string text = "";
        public string senderName = "";
        public string senderUrl = "";
        public string senderId = "";
    }

    abstract class BaseLiveChatParser
    {
        protected BaseLogger _logger = null;
        string _parserName = "BaseLiveChatParser";

        public BaseLiveChatParser(string name, BaseLogger l)
        {
            _parserName = name;
            _logger = l;
        }

        public string GetName()
        {
            return _parserName;
        }

        public abstract bool Init();
        public abstract int UpdateLiveChatMessageBuffer();
        public abstract LiveChatMessageParams GetLiveChatMessageFromBuffer(int i);


        public abstract void SendLiveChatMessage(string text);

        protected void Log(string msg, LogSeverity severity)
        {
            if (_logger != null)
            {
                _logger.Log(_parserName + ": " + msg, severity);
            }
        }

        protected void Log(string msg)
        {
            Log(msg, LogSeverity.LogSeverityDebug);
        }
    }
}
