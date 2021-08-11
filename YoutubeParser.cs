﻿using System;
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

            Log("Performing OAuth2 login and starting YouTube service");
            _youTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _googleAuth.GetCreds(),
                ApplicationName = "YouTubeLiveChatCommandsParser"
            });
            Log("Parser successfully created");
        }

        private void Log(string l)
        {
            // Console.WriteLine("YoutubeParser: {0:s}", l);
        }

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
                    Thread.Sleep(_lastLiveChatPollingPeriod - dt);
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
                Log("Chat portion response, " + Convert.ToString(response.Items.Count) + " items, polling interval " + Convert.ToString(response.PollingIntervalMillis));
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

        public int DownloadNewLiveChatMessages()
        {
            if (_liveChatId.Length == 0 || _nextChatToken.Length == 0)
            {
                Log("Please call InitLiveChatParser before GetNewLiveChatMessages");
                return 0;
            }
            _lastLiveChatResponse = GetNextChatPortion("snippet, authorDetails");

            return _lastLiveChatResponse.Items.Count;
        }

        public class LiveChatMessageParams
        {
            public bool valid = false;
            public string text = "";
            public string senderName = "";
            public string senderUrl = "";
            public string senderId = "";
        }

        public LiveChatMessageParams GetLiveChatMessage(int i)
        {
            var res = new LiveChatMessageParams();
            if ((i < _lastLiveChatResponse.Items.Count) && (_lastLiveChatResponse.Items[i].Snippet.Type == "textMessageEvent"))
            {
                res.valid = true;
                res.text = _lastLiveChatResponse.Items[i].Snippet.TextMessageDetails.MessageText;
                res.senderName = _lastLiveChatResponse.Items[i].AuthorDetails.DisplayName;
                res.senderId = "yt-" + _lastLiveChatResponse.Items[i].AuthorDetails.ChannelId.Replace('=', '*').Replace(';', '&');
                res.senderUrl = _lastLiveChatResponse.Items[i].AuthorDetails.ChannelUrl;
            }
            return res;
        }

        public void AddLiveChatMessage(string text)
        {
            if (_liveChatId.Length == 0 || _nextChatToken.Length == 0)
            {
                Log("Please call InitLiveChatParser before AddLiveChatMessage");
                return;
            }

            var m = new LiveChatMessage();
            m.Snippet = new LiveChatMessageSnippet();
            m.Snippet.TextMessageDetails = new LiveChatTextMessageDetails();

            m.Snippet.LiveChatId = _liveChatId;
            m.Snippet.Type = "textMessageEvent";
            m.Snippet.TextMessageDetails.MessageText = text;

            var request = _youTubeService.LiveChatMessages.Insert(m, "snippet");
            request.Execute();
        }
    }
}
