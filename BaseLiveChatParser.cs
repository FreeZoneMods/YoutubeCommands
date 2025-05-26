using ChatLoggers;

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
        string _send_buffer = "";
        int _max_send_buffer_len = 145;

        public BaseLiveChatParser(string name, BaseLogger l)
        {
            _parserName = name;
            _logger = l;
            _send_buffer = "";
        }

        public string GetName()
        {
            return _parserName;
        }

        public abstract bool Init();
        public abstract int UpdateLiveChatMessageBuffer();
        public abstract LiveChatMessageParams GetLiveChatMessageFromBuffer(int i);


        public void AddLiveChatMessageToSendBuffer(string text)
        {
            if (text.Length > _max_send_buffer_len)
            {
                text = text.Substring(0, _max_send_buffer_len);
            }

            string new_buffer = (_send_buffer.Length>0) ? (_send_buffer + ". " + text) : text;

            if (new_buffer.Length > _max_send_buffer_len)
            {
                FlushSendLiveChatBuffer();
                _send_buffer = text;
            }
            else
            {
                _send_buffer = new_buffer;
            }
        }

        public void ClearSendLiveChatBuffer()
        {
            _send_buffer = "";
        }

        public void FlushSendLiveChatBuffer()
        {
            if (_send_buffer.Length > 0)
            {
                SendLiveChatMessage(_send_buffer);
                _send_buffer = "";
            }
        }

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
