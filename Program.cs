﻿using System;
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

using ChatLoggers;

namespace ChatInteractiveCommands
{
    /*class YoutubeLiveChatCommander1
    {
        private ProgramConfig _cfg;
        private GoogleAuth _auth;
        private YoutubeParser _parser;
        private CommandsProcessing _processor;
        private ResponseBuilder _responceBuilder;

        public YoutubeLiveChatCommander1(string inipath)
        {
            _cfg = new ProgramConfig(inipath);
            Stream s = new FileStream(_cfg.GetGoogleOAuth2JsonPath(), FileMode.Open, FileAccess.Read);
            _auth = new GoogleAuth(s, new[] { YouTubeService.Scope.Youtube });
            _parser = new YoutubeParser(_auth);
            _processor = new CommandsProcessing(_cfg);
            _responceBuilder = new ResponseBuilder(_cfg);
        }

        private string ExtractChatCommandFromChatMessage(LiveChatMessageParams msg)
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
            Console.WriteLine("Chat command from '" + m.senderName + "' processed, status = '" + r.status + "'");

            string reply = _responceBuilder.BuildResponse(m.senderName, r.status);
            if (r.allow_response && (reply.Length > 0) && (reply.Length <= 150) && _cfg.IsChatRepliesEnabled())
            {
                _parser.SendLiveChatMessage(reply);
            }
        }

        public void Execute()
        {
            Console.WriteLine("Performing live chat parser initialization");
            if (!_parser.Init())
            {
                Console.WriteLine("Can't initialize parser!");
            }
            else
            {
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

                    int cnt = _parser.UpdateLiveChatMessageBuffer();
                    if (cnt > 0)
                    {
                        Console.WriteLine("Arrived {0:d} new chat message(s)", cnt);
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
    }*/

    class MainEngine
    {
        private List<BaseLiveChatCommander> _commanders;
        private ProgramConfig _cfg;
        private BaseLogger _logger;

        public MainEngine(ProgramConfig cfg, BaseLogger logger)
        {
            _commanders = new List<BaseLiveChatCommander>();
            _cfg = cfg;
            _logger = logger;
        }

        public void RegisterCommander(BaseLiveChatCommander commander)
        {
            _commanders.Add(commander);
        }

        public bool Initialize()
        {
            foreach (BaseLiveChatCommander commander in _commanders)
            {
                if (!commander.Initialize())
                {
                    return false;
                }
            }
            return true;
        }

        public void Execute()
        {
            int update_period = _cfg.GetUpdateInterval();
            _logger.Log("Polling live chat, period " + update_period.ToString(), LogSeverity.LogSeverityNormal);
            int last_iter_tick = Environment.TickCount & Int32.MaxValue;

            while (true)
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

                foreach (BaseLiveChatCommander commander in _commanders)
                {
                    commander.PerformIteration();
                }
            }
        }
    }

    class Program
    {

        static void Main(string[] args)
        {
            string ini = "interactivechatparser.ini";
            if (args.Length > 0)
            {
                ini = args[0];
            }

            var cfg = new ProgramConfig(ini);
            var logger = new ConsoleLogger();

            var engine = new MainEngine(cfg, logger);
            engine.RegisterCommander(new YoutubeLiveChatCommander(cfg, logger));


            if (!engine.Initialize())
            {
                Console.WriteLine("Initialization fail!");
            }
            else
            {
                engine.Execute();
            }

            Console.WriteLine("<Press any key to exit>");
            Console.ReadLine();
        }
    }
}
