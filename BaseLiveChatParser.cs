using ChatLoggers;
using System;
using System.IO;

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
        int _max_send_buffer_len = 150;
        string _chat_log_file_name = "";

        public BaseLiveChatParser(string name, BaseLogger l)
        {
            _parserName = name;
            _logger = l;
            _send_buffer = "";
            _chat_log_file_name = "";
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

        public void SetLiveChatLogFileName(string name)
        {
            _chat_log_file_name = name;
        }

        public void ResetLiveChatLogFile()
        {
            if (_chat_log_file_name.Length > 0)
            {
                try
                {
                    using (StreamWriter outputFile = new StreamWriter(_chat_log_file_name, false))
                    {
                    }
                }
                catch (Exception)
                {
                    Log("Error resetting chat log file", LogSeverity.LogSeverityError);
                }
            }
        }

        public void WriteToLiveChatLogFile(string s)
        {
            if (_chat_log_file_name.Length > 0)
            {
                try
                {
                    using (StreamWriter outputFile = new StreamWriter(_chat_log_file_name, true))
                    {
                        outputFile.WriteLine(s);
                    }
                }
                catch (Exception)
                {
                    Log("Error writing to chat log file", LogSeverity.LogSeverityError);
                }
            }
        }

        public void FlushSendLiveChatBuffer()
        {
            if (_send_buffer.Length > 0)
            {
                WriteToLiveChatLogFile(_send_buffer);
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
