using System.IO;
using ChatLoggers;

namespace ChatInteractiveCommands
{
    class DonationAlertsCommander : BaseLiveChatCommander
    {
        public DonationAlertsCommander(ProgramConfig cfg, BaseLogger logger) : base(cfg, logger, null, ProgramConfig.ChatService.CHAT_SERVICE_DONATIONALERTS)
        {
        }

        public override bool Initialize()
        {
            Stream s = new FileStream(_cfg.GetDonationAlertsOAuth2JsonPath(), FileMode.Open, FileAccess.Read);
            DonationAlertsCreds creds = DonationAlertsCreds.Load(s);

            _parser = new DonationAlertsParser(creds, _logger);
            _parser.SetLiveChatLogFileName(_cfg.ChatRepliesLogFileName());
            return _parser.Init();
        }

        public override bool IsCommandProcessorNeedToCheckScores()
        {
            // scores bank is not used, but we need to check donation amount in command processor
            return true;
        }
    }
}
