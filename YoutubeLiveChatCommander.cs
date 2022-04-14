using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using Google.Apis.YouTube.v3;

using ChatLoggers;

namespace ChatInteractiveCommands
{
    class YoutubeLiveChatCommander : BaseLiveChatCommander
    {
        private GoogleAuth _auth;
        
        public YoutubeLiveChatCommander(ProgramConfig cfg, BaseLogger logger) : base(cfg, logger)
        {
        }

        public override bool Initialize()
        {
            Stream s = new FileStream(_cfg.GetGoogleOAuth2JsonPath(), FileMode.Open, FileAccess.Read);
            _auth = new GoogleAuth(s, new[] { YouTubeService.Scope.Youtube });
            _parser = new YoutubeParser(_auth, _logger);
            _parser.Init();
            return true;
        }
    }
}
