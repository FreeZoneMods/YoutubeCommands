using ChatLoggers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using static ChatInteractiveCommands.ProgramConfig;

namespace ChatInteractiveCommands
{
    public sealed class DonationAlertsCreds
    {
        [JsonProperty("client_id")]
        public string ClientId { get; set; }
        [JsonProperty("client_secret")]
        public string ClientSecret { get; set; }

        public static DonationAlertsCreds Load(Stream json)
        {
            StreamReader reader = new StreamReader(json);
            JsonSerializer s = new JsonSerializer();
            return (DonationAlertsCreds)s.Deserialize(reader, typeof(DonationAlertsCreds));
        }
    }

    public sealed class DonationAlertsSingleDonationInfo
    {
        [JsonProperty("id")]
        public int DonationId { get; set; }

        [JsonProperty("name")]
        public string  EventType { get; set; }

        [JsonProperty("username")]
        public string DonaterName { get; set; }

        [JsonProperty("message_type")]
        public string MessageType { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("amount")]
        public float Amount { get; set; }

        [JsonProperty("amount_in_user_currency")]
        public float AmountInUserCurrency { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("is_shown")]
        public int IsShown { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("shown_at")]
        public string ShownAt { get; set; }
    }

    public sealed class DonationAlertsDonationResponse
    {
        [JsonProperty("data")]
        public List<DonationAlertsSingleDonationInfo> Donations { get; set; }

        public static DonationAlertsDonationResponse Load(Stream json)
        {
            StreamReader reader = new StreamReader(json);
            JsonSerializer s = new JsonSerializer();
            return (DonationAlertsDonationResponse)s.Deserialize(reader, typeof(DonationAlertsDonationResponse));
        }
    }

    class DonationAlertsAuthenticator : OAuth2Authenticator
    {
        const string donationalerts_login_page_uri = "https://www.donationalerts.com/oauth/authorize";
        const string donationalerts_redirect_param = "redirect_uri";
        const string donationalerts_token_param = "access_token";

        public DonationAlertsAuthenticator(DonationAlertsCreds creds, string scopes) : base(donationalerts_login_page_uri, donationalerts_redirect_param, donationalerts_token_param)
        {
            AddLoginParameter("response_type", "token");
            AddLoginParameter("client_id", creds.ClientId);
            AddLoginParameter("scope", scopes);
        }
    }

    class DonationAlertsParser : BaseLiveChatParser
    {
        DonationAlertsCreds _creds;
        WebToken _auth_token;
        Thread _cycle_thread = null;
        Mutex _messages_queue_mutex;
        Queue<LiveChatMessageParams> _messages_queue;
        List<LiveChatMessageParams> _lastLiveChatMessages;

        public DonationAlertsParser(DonationAlertsCreds creds, BaseLogger logger) : base("DonationAlertsParser", logger)
        {
            _creds = creds;
            _messages_queue = new Queue<LiveChatMessageParams>();
            _messages_queue_mutex = new Mutex();
            _lastLiveChatMessages = new List<LiveChatMessageParams>();
        }

        private string ApiRequest(string uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Headers.Add("Authorization: Bearer " + _auth_token.GetToken());

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                return "";
            }
        }

        private DonationAlertsDonationResponse RequestLastDonations()
        {
            try
            {
                string donates = ApiRequest("https://www.donationalerts.com/api/v1/alerts/donations");
                if (donates.Length > 0)
                {

                    using (Stream donates_stream = new MemoryStream(Encoding.UTF8.GetBytes(donates)))
                    {
                        DonationAlertsDonationResponse response = DonationAlertsDonationResponse.Load(donates_stream);
                        return response;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch 
            {
                return null;
            }
        }

        private void OnNewDonationFound(DonationAlertsSingleDonationInfo info)
        {
            var name = info.DonaterName;
            if ((name == null) || (name.Length == 0))
            {
                name = "Anonymous";
            }

            Log(name + " (" + info.AmountInUserCurrency.ToString() + "): " + info.Message);

            LiveChatMessageParams chatmessage = new LiveChatMessageParams();
            chatmessage.valid = true;
            chatmessage.text = info.Message;
            chatmessage.senderId = "da-"+ name;
            chatmessage.senderName = name;
            chatmessage.senderUrl = name;
            chatmessage.award = (int)(info.AmountInUserCurrency);
            chatmessage.with_donation = true;

            _messages_queue_mutex.WaitOne();
            try
            {
                _messages_queue.Enqueue(chatmessage);
            }
            finally
            {
                _messages_queue_mutex.ReleaseMutex();
            }
        }

        private void Cycle()
        {
            // Determine latest donation id
            int last_donation_id = 0;
            while (true)
            {
                DonationAlertsDonationResponse r = RequestLastDonations();
                if (r != null && r.Donations.Count>0)
                {
                    last_donation_id = r.Donations[0].DonationId;
                    break;
                }
            }

            // Poll & check for new donations
            while (true)
            {                
                DonationAlertsDonationResponse r = RequestLastDonations();
                if (r != null && r.Donations.Count > 0)
                {
                    int newIdx = r.Donations.Count - 1;

                    for (int i = 0; i < r.Donations.Count; ++i)
                    {
                        if (r.Donations[i].DonationId == last_donation_id)
                        {
                            newIdx = i-1;
                            break;
                        }
                    }

                    if (newIdx >= 0)
                    {
                        for (int i = 0; i <= newIdx; ++i)
                        {
                            OnNewDonationFound(r.Donations[i]);
                        }
                        last_donation_id = r.Donations[newIdx].DonationId;
                    }
                }

                Thread.Sleep(5000);
            }

        }


        public bool RunCycleThead()
        {
            if (_cycle_thread != null && !_cycle_thread.IsAlive)
            {
                _cycle_thread = null;
            }


            if (_cycle_thread == null)
            {
                _cycle_thread = new Thread(new ThreadStart(this.Cycle));
                _cycle_thread.Start();
                return true;
            }
            return false;
        }

        public override bool Init()
        {
            Log("Performing login to DonationAlerts, please enter your credentials to browser's window", LogSeverity.LogSeverityNormal);

            string scopes = "oauth-donation-index";
            DonationAlertsAuthenticator auth = new DonationAlertsAuthenticator(_creds, scopes);
            
            _auth_token = new WebToken(auth);

            string token_value = _auth_token.GetToken();
            if (token_value.Length == 0)
            {
                Log("Can't get DonationAlerts auth token", LogSeverity.LogSeverityError);
                return false;
            }

            return RunCycleThead();
        }

        public override LiveChatMessageParams GetLiveChatMessageFromBuffer(int i)
        {
            if (_lastLiveChatMessages.Count <= i)
            {
                return null;
            }
            else
            {
                return _lastLiveChatMessages[i];
            }
        }

        public override int UpdateLiveChatMessageBuffer()
        {
            _lastLiveChatMessages.Clear();

            _lastLiveChatMessages.Clear();
            _messages_queue_mutex.WaitOne();
            try
            {
                while (_messages_queue.Count > 0) 
                {
                    _lastLiveChatMessages.Add(_messages_queue.Dequeue());
                }

                return _lastLiveChatMessages.Count;
            }
            finally
            {
                _messages_queue_mutex.ReleaseMutex();
            }
        }

        public override void SendLiveChatMessage(string text)
        { }
    }
}
