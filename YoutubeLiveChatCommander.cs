using System.IO;
using Google.Apis.YouTube.v3;

using ChatLoggers;

namespace ChatInteractiveCommands
{
    class YoutubeLiveChatCommander : BaseLiveChatCommander
    {
        private GoogleAuth _auth;
        
        public YoutubeLiveChatCommander(ProgramConfig cfg, BaseLogger logger, IScoresBank scores) : base(cfg, logger, scores, ProgramConfig.ChatService.CHAT_SERVICE_YOUTUBE)
        {
        }

        public override bool Initialize()
        {
            Stream s = new FileStream(_cfg.GetGoogleOAuth2JsonPath(), FileMode.Open, FileAccess.Read);
            _auth = new GoogleAuth(s, new[] { YouTubeService.Scope.Youtube });
            _parser = new YoutubeParser(_auth, _logger);
            _parser.SetLiveChatLogFileName(_cfg.ChatRepliesLogFileName());
            return _parser.Init();
        }
    }
}
