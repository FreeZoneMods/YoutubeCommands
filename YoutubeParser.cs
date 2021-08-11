using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace YoutubeCommands
{
    class YoutubeParser
    {
        protected GoogleAuth _googleAuth;
        protected YouTubeService _youTubeService;

        protected string _liveChatId;
        protected string _nextChatToken;
        protected int _lastLiveChatRequestTickCount;
        protected int _lastLiveChatPollingPeriod;
        protected LiveChatMessageListResponse _lastLiveChatResponse;

        public YoutubeParser(GoogleAuth auth)
        {
            _googleAuth = auth;
            _youTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _googleAuth.GetCreds(),
                ApplicationName = "YouTubeLiveChatCommandsParser"
            });
            Log("Parser successfully created");
        }

        private void Log(string l) { Console.WriteLine("YoutubeParser: {0:s}", l); }

        public string GetLiveChatId()
        {
            var listBroadcastsRequest = _youTubeService.LiveBroadcasts.List("snippet");
            listBroadcastsRequest.BroadcastStatus = LiveBroadcastsResource.ListRequest.BroadcastStatusEnum.Active;

            var listBroadcastsResponse = listBroadcastsRequest.Execute();
            Log("Found " + Convert.ToString(listBroadcastsResponse.Items.Count) + " broadcasts");

            if (listBroadcastsResponse.Items.Count == 1)
            {
                return listBroadcastsResponse.Items[0].Snippet.LiveChatId;
            }
            else if (listBroadcastsResponse.Items.Count == 0)
            {
                return "! No active translations found";
            }
            else
            {
                return "! Multiple active translations found";
            }
        }

        private LiveChatMessageListResponse GetNextChatPortion(string part, int maxResults = -1)
        {
            int curTick = Environment.TickCount & Int32.MaxValue;
            if (curTick <= _lastLiveChatRequestTickCount)
            {
                //overflow, sleep full polling interval
                Thread.Sleep(_lastLiveChatPollingPeriod);
            }
            else
            {
                int dt = curTick - _lastLiveChatRequestTickCount;
                if (dt <= _lastLiveChatPollingPeriod)
                {
                    Thread.Sleep(_lastLiveChatPollingPeriod-dt);
                }
            }

            var request = _youTubeService.LiveChatMessages.List(_liveChatId, part);
            if (maxResults > 0)
            {
                request.MaxResults = maxResults;
            }

            if (_nextChatToken.Length > 0)
            {
                request.PageToken = _nextChatToken;
            }

            var response = request.Execute();
            _nextChatToken = response.NextPageToken;
            _lastLiveChatRequestTickCount = Environment.TickCount & Int32.MaxValue;
            _lastLiveChatPollingPeriod = (int)response.PollingIntervalMillis;

            if (maxResults > 0)
            {
                Log("Chat portion response, " + Convert.ToString(response.Items.Count) + "/" + Convert.ToString(maxResults) + " items, polling interval " + Convert.ToString(response.PollingIntervalMillis));
            }
            else
            {
                Log("Chat portion response, " + Convert.ToString(response.Items.Count) + "items, polling interval " + Convert.ToString(response.PollingIntervalMillis));
            }

            return response;
        }

        public bool InitLiveChatParser(string liveChatId)
        {
            Log("Init live chat parser");
            _liveChatId = liveChatId;
            _nextChatToken = "";
            _lastLiveChatRequestTickCount = Environment.TickCount & Int32.MaxValue;
            _lastLiveChatPollingPeriod = 0;

            LiveChatMessageListResponse response;
            do
            {
                response = GetNextChatPortion("id");
                Log("Skip " + Convert.ToString(response.Items.Count) + " message(s)");

            }
            while (response.Items.Count > 0);

            return true;
        }

        public int GetNewLiveChatMessages()
        {
            if (_liveChatId.Length == 0 || _nextChatToken.Length == 0)
            {
                Log("Please call InitLiveChatParser before GetNewLiveChatMessages");
                return 0;
            }
            _lastLiveChatResponse = GetNextChatPortion("snippet, authorDetails");

            return _lastLiveChatResponse.Items.Count;
        }

        public string GetChatMessageText(int i)
        {
            if (i < _lastLiveChatResponse.Items.Count)
            {
                return _lastLiveChatResponse.Items[i].Snippet.DisplayMessage;
            }
            else
            {
                return "";
            }
        }

        public string GetChatMessageSenderName(int i)
        {
            if (i < _lastLiveChatResponse.Items.Count)
            {
                return _lastLiveChatResponse.Items[i].AuthorDetails.DisplayName;
            }
            else
            {
                return "";
            }
        }

        public string GetChatMessageSenderChannelUrl(int i)
        {
            if (i < _lastLiveChatResponse.Items.Count)
            {
                return _lastLiveChatResponse.Items[i].AuthorDetails.ChannelUrl;
            }
            else
            {
                return "";
            }
        }
    }
}
