using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChatLoggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;

namespace ChatInteractiveCommands
{

    public sealed class TrovoCreds
    {
        [JsonProperty("client_id")]
        public string ClientId { get; set; }
        [JsonProperty("auth_uri")]
        public string LoginPageUri { get; set; }

        public static TrovoCreds Load(Stream json)
        {
            StreamReader reader = new StreamReader(json);
            JsonSerializer s = new JsonSerializer();
            return (TrovoCreds) s.Deserialize(reader, typeof(TrovoCreds));
        }
    }

    class TrovoAuthenticator : OAuth2Authenticator
    {
        const string trovo_login_page_uri = "https://open.trovo.live/page/login.html";
        const string trovo_redirect_param = "redirect_uri";
        const string trovo_token_param = "access_token";
        
        public TrovoAuthenticator(TrovoCreds creds, string scopes) : base(trovo_login_page_uri, trovo_redirect_param, trovo_token_param)
        {
            if (creds.LoginPageUri.Length > 0 && creds.LoginPageUri != trovo_login_page_uri)
            {
                login_url = creds.LoginPageUri;
            }

            AddLoginParameter("client_id", creds.ClientId);
            AddLoginParameter("response_type", "token");
            AddLoginParameter("scope", scopes);
            AddLoginParameter("state", "TrovoStateStuff");
        }
    }

    class TrovoChatTokenData
    {
        [JsonProperty("token")]
        public string ChatToken { get; set; }
    }

    class TrovoChatToken
    {
        WebToken _trovo_token;
        string _clientId;


        public TrovoChatToken(WebToken trovo_token, string clientId)
        {
            _trovo_token = trovo_token;
            _clientId = clientId;
        }

        public TrovoChatTokenData GetChatServiceToken()
        {
            string url = "https://open-api.trovo.live/openplatform/chat/token";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "application/json";
            request.Headers.Add("Client-ID: " + _clientId);
            request.Headers.Add("Authorization: OAuth " + _trovo_token.GetToken());

            return JsonRequestor.ExecuteRequest<TrovoChatTokenData>(request);
        }

        public string GetClientId()
        {
            return _clientId;
        }

        public string GetOAuthToken()
        {
            return _trovo_token.GetToken();
        }
    }


    class TrovoChatServiceDataMessageInfo 
    {
        [JsonProperty("type")]
        public string MessageTypeId { get; set; }

        [JsonProperty("content")]
        public string MessageContent { get; set; }

        [JsonProperty("nick_name")]
        public string NickName { get; set; }


        [JsonProperty("sender_id")]
        public Int64 SenderId { get; set; }

        [JsonProperty("uid")]
        public Int64 UserId { get; set; }
    };

    internal class CustomArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(List<T>));
        }

        public override object ReadJson(
          JsonReader reader,
          Type objectType,
          object existingValue,
          JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
                return token.ToObject<List<T>>();
            return new List<T> { token.ToObject<T>() };
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    class TrovoChatServiceData
    {
        [JsonProperty("gap")]
        public int HeartbeatPeriod { get; set; }

        [JsonConverter(typeof(CustomArrayConverter<TrovoChatServiceDataMessageInfo>))]
        public IEnumerable<TrovoChatServiceDataMessageInfo> chats { get; set; }

    }

    class TrovoChatServiceMessage
    {
        [JsonProperty("type")]
        public string MessageType { get; set; }

        [JsonProperty("nonce")]
        public string MessageNonce { get; set; }

        [JsonProperty("error")]
        public string ErrorString { get; set; }

        [JsonProperty("data")]
        public TrovoChatServiceData Data { get; set; }
    }

    class TrovoChatService
    {
        string _nonce = "InterChat";
        int _timeout = 5000;
        ClientWebSocket _socket = null;
        TrovoChatToken _token;
        BaseLogger _log;
        Thread _cycle_thread = null;
        bool _old_messages_skipped = false;

        private Mutex _messages_queue_mutex;
        Queue<LiveChatMessageParams> _messages_queue;

        public TrovoChatService(string nonce, TrovoChatToken token, BaseLogger logger)
        {
            _nonce = nonce;
            _token = token;
            _log = logger;
            _messages_queue = new Queue<LiveChatMessageParams>();
            _messages_queue_mutex = new Mutex(); 
        }

        public void Log(string s)
        {
            Log(s, LogSeverity.LogSeverityDebug);
        }

        public void Log(string s, LogSeverity sev)
        {
            if (_log != null)
            {
                _log.Log("TrovoService: "+s, sev);
            }
        }


        private bool Send(string s)
        {
            bool res = false;
            if (_socket == null || (_socket.State != WebSocketState.Open))
            {
                return res;
            }

            var source = new CancellationTokenSource();
            source.CancelAfter(5000);

            try
            {
                ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(s));
                _socket.SendAsync(bytesToSend, WebSocketMessageType.Text, true, source.Token).Wait();
                res = true;
            }
            catch (Exception)
            { }

            return res;
        }

        private string Receive(int time_to_wait)
        {
            if (_socket == null ||  (_socket.State != WebSocketState.Open))
            {
                return "";
            }

            string result = "";

            byte[] data = new byte[4096];
            ArraySegment<byte> buffer = new ArraySegment<byte>(data);

            WebSocketReceiveResult receiveResult = null;
            do
            {
                bool force_break = false;

                try
                {
                    if (time_to_wait >= 0)
                    {
                        var source = new CancellationTokenSource();
                        source.CancelAfter(time_to_wait);
                        receiveResult = _socket.ReceiveAsync(buffer, source.Token).Result;
                    }
                    else
                    {
                        receiveResult = _socket.ReceiveAsync(buffer, CancellationToken.None).Result;
                    }
                }
                catch (AggregateException)
                {
                    result = "";
                    force_break = true;
                }

                
                if (!force_break && receiveResult!=null && receiveResult.Count > 0)
                {
                    string m = Encoding.UTF8.GetString(data, 0, receiveResult.Count);
                    result += m;
                }
                else
                {
                    break;
                }
            } while (!receiveResult.EndOfMessage);

            return result;
        }


        public bool Connect()
        {
            Log("Connecting to Trovo chat service", LogSeverity.LogSeverityNormal);

            string url = "wss://open-chat.trovo.live/chat";

            var source = new CancellationTokenSource();
            source.CancelAfter(_timeout);

            _socket = new ClientWebSocket();

            try
            {
                _socket.ConnectAsync(new Uri(url), source.Token).Wait();
            }
            catch
            {
                return false;
            }

            var chat_token_data = _token.GetChatServiceToken();
            if (chat_token_data.ChatToken == null || chat_token_data.ChatToken.Length == 0)
            {
                Log("Can't get Trovo chat service token", LogSeverity.LogSeverityError);
                return false;
            }

            string authJson = "{\"type\": \"AUTH\", \"nonce\": \"" + _nonce + "\", \"data\": { \"token\": \"" + chat_token_data.ChatToken + "\"} }";

            Log("Send AUTH message");
            if (Send(authJson))
            {
                string reply = Receive(_timeout);
                TrovoChatServiceMessage msg = DeserializeTrovoChatServiceMessage(reply);

                if (msg.MessageType != "RESPONSE")
                {
                    Log("Invalid responce type: " + msg.MessageType, LogSeverity.LogSeverityError);
                    return false;
                }

                if (msg.MessageNonce != _nonce)
                {
                    Log("Invalid nonce: " + msg.MessageNonce, LogSeverity.LogSeverityError);
                    return false;
                }

                if (msg.ErrorString != null)
                {
                    Log("Received error: " + msg.ErrorString, LogSeverity.LogSeverityError);
                    return false;
                }

                _old_messages_skipped = false;
                return true;
                Log("Connection estabilished");
            }

            return false;
        }

        public TrovoChatServiceMessage DeserializeTrovoChatServiceMessage(string s)
        {
            JsonSerializer ser = new JsonSerializer();
            TextReader tr = new StringReader(s);
            return (TrovoChatServiceMessage)ser.Deserialize(tr, typeof(TrovoChatServiceMessage));
        }

        private void Cycle()
        {
            int defaultHeartbeatPeriod = 5;
            int heartbeatPeriod = defaultHeartbeatPeriod;
            string pingJson = "{\"type\": \"PING\", \"nonce\": \"" + _nonce + "\" }";

            while (true)
            {
                if (_socket == null || (_socket.State != WebSocketState.Open))
                {
                    Connect();
                    Thread.Sleep(heartbeatPeriod * 1000);
                    heartbeatPeriod = defaultHeartbeatPeriod;
                }
                else
                {
                    try
                    {
                        bool pongReceived = true;
                        int chats_before_pong = 0;

                        do
                        {
                            if (_socket.State != WebSocketState.Open)
                            {
                                Log("Connection with Trovo chat lost, attempting to reconnect", LogSeverity.LogSeverityNormal);
                                _socket = null;
                                break;
                            }


                            if (pongReceived)
                            {
                                Log("Send heartbeat " + pingJson);
                                Send(pingJson);
                                pongReceived = false;
                                chats_before_pong = 0;
                            }

                            string reply = Receive(_timeout);
                            if (reply.Length > 0)
                            {
                                TrovoChatServiceMessage m = DeserializeTrovoChatServiceMessage(reply);
                                if (m.MessageType == null)
                                {
                                    Log("Received TrovoChatServiceMessage message without message type", LogSeverity.LogSeverityWarning);
                                }
                                else if (m.MessageType == "PONG")
                                {
                                    Log("Received PONG");
                                    pongReceived = true;
                                    if (chats_before_pong == 0)
                                    {
                                        _old_messages_skipped = true;
                                    }

                                    Thread.Sleep(heartbeatPeriod * 1000);
                                }
                                else if (m.MessageType == "CHAT")
                                {
                                    chats_before_pong++;
                                    if (_old_messages_skipped)
                                    {
                                        Log("Received CHAT: " + reply);

                                        Log("Chats in message:" + m.Data.chats.Count().ToString());
                                        foreach (var chat in m.Data.chats)
                                        {
                                            Log(chat.NickName + ": " + chat.MessageContent);


                                            LiveChatMessageParams chatmessage = new LiveChatMessageParams();
                                            chatmessage.valid = true;
                                            chatmessage.text = chat.MessageContent;
                                            chatmessage.senderId = "tr-"+chat.SenderId.ToString();
                                            chatmessage.senderName = chat.NickName;
                                            chatmessage.senderUrl = chat.UserId.ToString();

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
                                        
                                    }
                                    else
                                    {
                                        Log("Received CHAT skipped");
                                    }
                                }
                                else
                                {
                                    Log("Received unsupported message, type "+ m.MessageType, LogSeverity.LogSeverityWarning);
                                }
                            }
                        } while (true);
                    }
                    catch
                    {
                        _socket = null;
                    }
                }
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

        public int MessagesCount()
        { 
            _messages_queue_mutex.WaitOne();
            try
            {
                return _messages_queue.Count;
            }
            finally
            {
                _messages_queue_mutex.ReleaseMutex();
            }
        }

        public LiveChatMessageParams GetNextMessage()
        {
            _messages_queue_mutex.WaitOne();
            try
            {
                if (_messages_queue.Count() == 0)
                {
                    return null;
                }
                else
                {
                    return _messages_queue.Dequeue();
                }
            }
            finally
            {
                _messages_queue_mutex.ReleaseMutex();
            }
        }

        public void SendChat(string text)
        {
            string url = "https://open-api.trovo.live/openplatform/chat/send";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Accept = "application/json";
            request.Headers.Add("Client-ID: " + _token.GetClientId());
            request.Headers.Add("Authorization: OAuth " + _token.GetOAuthToken());

            try
            {
                JsonRequestor.ExecuteRequest<TrovoChatTokenData>(request);
            }
            catch (Exception)
            {
                Log("Failed to send chat message", LogSeverity.LogSeverityError);
            }
        }
    }

    class TrovoParser: BaseLiveChatParser
    {
        WebToken _trovo_token;        
        TrovoChatService _chat_service;
        TrovoCreds _creds;
        List<LiveChatMessageParams> _lastLiveChatMessages;


        public TrovoParser(TrovoCreds creds, BaseLogger logger) : base("TrovoParser", logger)
        {
            _creds = creds;
            _lastLiveChatMessages = new List<LiveChatMessageParams>();
        }

        public override bool Init()
        {
            Log("Performing login to Trovo, please enter your credentials to browser's window", LogSeverity.LogSeverityNormal);

            string scopes = "chat_send_self+send_to_my_channel+manage_messages+chat_connect";

            TrovoAuthenticator auth = new TrovoAuthenticator(_creds, scopes);
            _trovo_token = new WebToken(auth);

            string token_value = _trovo_token.GetToken();
            if (token_value.Length == 0)
            {
                Log("Can't get Trovo auth token", LogSeverity.LogSeverityError);
                return false;
            }

            Log("Connecting to chat service using Trovo token=" + token_value);

            TrovoChatToken chat_service_token = new TrovoChatToken(_trovo_token, _creds.ClientId);           
            _chat_service = new TrovoChatService("InterChatNonce", chat_service_token, _logger);

            if (!_chat_service.RunCycleThead())
            {
                Log("Can't start message polling thread", LogSeverity.LogSeverityError);
                return false;
            }

            return true;
        }

        public override int UpdateLiveChatMessageBuffer()
        {
            int cnt = _chat_service.MessagesCount();
            _lastLiveChatMessages.Clear();
            for (int i = 0; i < cnt; i++)
            {
                _lastLiveChatMessages.Add(_chat_service.GetNextMessage());
            }
            
            return cnt;
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


        public override void SendLiveChatMessage(string text)
        {
            _chat_service.SendChat(text);
        }
    }
}
