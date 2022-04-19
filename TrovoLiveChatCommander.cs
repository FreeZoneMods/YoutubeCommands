﻿using System.IO;
using Google.Apis.YouTube.v3;

using ChatLoggers;

namespace ChatInteractiveCommands
{
    class TrovoLiveChatCommander : BaseLiveChatCommander
    {      

        public TrovoLiveChatCommander(ProgramConfig cfg, BaseLogger logger) : base(cfg, logger)
        {            
        }

        public override bool Initialize()
        {
            Stream s = new FileStream(_cfg.GetTrovoOAuth2JsonPath(), FileMode.Open, FileAccess.Read);
            TrovoCreds creds = TrovoCreds.Load(s);

            _parser = new TrovoParser(creds, _logger);
            return _parser.Init();
        }
    }
}
