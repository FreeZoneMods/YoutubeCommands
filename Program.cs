using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Google.Apis.Auth.OAuth2;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;

namespace YoutubeCommands
{
    class YoutubeLiveChatCommander
    {
        private ProgramConfig _cfg;
        private GoogleAuth _auth;
        private YoutubeParser _parser;
        private CommandsProcessing _processor;
        private ResponseBuilder _responceBuilder;

        public YoutubeLiveChatCommander()
        {
            _cfg = new ProgramConfig("youtubeparser.ini");
            Stream s = new FileStream(_cfg.GetGoogleOAuth2JsonPath(), FileMode.Open, FileAccess.Read);
            _auth = new GoogleAuth(s, new[] { YouTubeService.Scope.Youtube });
            _parser = new YoutubeParser(_auth);
            _processor = new CommandsProcessing(_cfg);
            _responceBuilder = new ResponseBuilder(_cfg);
        }

        private string ExtractChatCommandFromChatMessage(YoutubeParser.LiveChatMessageParams msg)
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
            var m = _parser.GetLiveChatMessage(r.id);
            Console.WriteLine("Chat command from '" + m.senderName + "' processed, status = '" + r.status + "'");

            string reply = _responceBuilder.BuildResponse(m.senderName, r.status);
            if (r.allow_response && (reply.Length > 0) && (reply.Length <= 150) && _cfg.IsChatRepliesEnabled())
            {
                _parser.AddLiveChatMessage(reply);
            }
        }

        public void Execute()
        {
            string liveChatId = _parser.GetLiveChatId();
            if ((liveChatId.Length == 0) || (liveChatId[0] == '!'))
            {
                Console.WriteLine("Can't get live chat id, reason:");
                Console.WriteLine(liveChatId);
            }
            else
            {
                Console.WriteLine("Got live chat id:");
                Console.WriteLine(liveChatId);

                Console.WriteLine("Performing live chat parser initialization");
                _parser.InitLiveChatParser(liveChatId);

                int update_period = _cfg.GetUpdateInterval();
                Console.WriteLine("Polling live chat, period {0:d}", update_period);

                int last_iter_tick = Environment.TickCount & Int32.MaxValue;
                do
                {
                    int cur_tick = Environment.TickCount & Int32.MaxValue;
                    if (last_iter_tick >= cur_tick)
                    {
                        //Overflow
                        Thread.Sleep(update_period);
                    }
                    else
                    {
                        int dt = cur_tick - last_iter_tick;
                        if (dt < update_period)
                        {
                            Thread.Sleep(update_period - dt);
                        }
                    }

                    last_iter_tick = Environment.TickCount & Int32.MaxValue;

                    int cnt = _parser.DownloadNewLiveChatMessages();
                    if (cnt > 0)
                    {
                        Console.WriteLine("Arrived {0:d} new chat message(s)", cnt);
                        bool found_commands = false;
                        for (int i = 0; i < cnt; ++i)
                        {
                            var m = _parser.GetLiveChatMessage(i);
                            string cmd = ExtractChatCommandFromChatMessage(m);
                            if (cmd.Length > 0)
                            {
                                Console.WriteLine("Extracted chat command from user '{0:s}' ({1:s}), body '{2:s}'", m.senderName, m.senderId, cmd);

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
                while (true);
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var parser = new YoutubeLiveChatCommander();
            parser.Execute();
            Console.WriteLine("<Press any key to exit>");
            Console.ReadLine();
        }
    }
}
