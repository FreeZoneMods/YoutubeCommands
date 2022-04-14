using System;

namespace ChatLoggers
{
    enum LogSeverity
    { 
        LogSeverityDebug,
        LogSeverityNormal,
        LogSeverityWarning,
        LogSeverityError,
    }


    abstract class BaseLogger
    {
        LogSeverity _targetSeverity;

        public void SetSeverity(LogSeverity severity)
        {
            _targetSeverity = severity;
        }


        public void Log(string s, LogSeverity severity)
        {
            if (severity >= _targetSeverity)
            {
                WriteLine(s);
            }
        }


        protected abstract void WriteLine(string s);
    }

    class ConsoleLogger : BaseLogger 
    {
        protected override void WriteLine(string s)
        {
            Console.WriteLine(s);
        }
    }
}
