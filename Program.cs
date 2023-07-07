using System;
using System.Collections.Generic;
using System.Threading;

using ChatLoggers;

namespace ChatInteractiveCommands
{
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
            logger.SetSeverity((LogSeverity)cfg.GetLogLevel());

            var engine = new MainEngine(cfg, logger);
            ScoresBank scores = null;
            if (cfg.IsUserScoresEnabled())
            {
                scores = new ScoresBank(cfg.GetScoresPath());
            }

            if ((scores == null) || scores.Init())
            {
                int commanders_cnt = 0;

                if (cfg.IsYoutubeParserEnabled())
                {
                    engine.RegisterCommander(new YoutubeLiveChatCommander(cfg, logger, scores));
                    commanders_cnt++;
                }

                if (cfg.IsTrovoParserEnabled())
                {
                    engine.RegisterCommander(new TrovoLiveChatCommander(cfg, logger, scores));
                    commanders_cnt++;
                }

                if (commanders_cnt == 0)
                {
                    Console.WriteLine("Please enable at least one chat service in the config");
                }
                else if (!engine.Initialize())
                {
                    Console.WriteLine("Engine initialization fail!");
                }
                else
                {
                    engine.Execute();
                }
            }
            else
            {
                Console.WriteLine("Cannot initialize scores");
            }

            Console.WriteLine("<Press any key to exit>");
            Console.ReadLine();
        }
    }
}
