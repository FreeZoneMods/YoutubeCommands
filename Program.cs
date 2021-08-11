using System;
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

namespace YoutubeCommands
{
    class Program
    {
        static void Main(string[] args)
        {
            Stream s = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read);
            GoogleAuth a = new GoogleAuth(s, new[] { YouTubeService.Scope.YoutubeReadonly } );
            YoutubeParser parser = new YoutubeParser(a);
            string liveChatId = parser.GetLiveChatId();
            if ((liveChatId.Length == 0) || (liveChatId[0] == '!'))
            {
                Console.WriteLine("Can't get live chat id, reason:");
                Console.WriteLine(liveChatId);
            }
            else
            {
                Console.WriteLine("Got live chat id:");
                Console.WriteLine(liveChatId);
                parser.InitLiveChatParser(liveChatId);

                do
                {
                    Thread.Sleep(10000);
                    int cnt = parser.GetNewLiveChatMessages();
                    if (cnt > 0)
                    {
                        Console.WriteLine("Arrived {0:d} new chat message(s)", cnt);
                        for (int i = 0; i < cnt; ++i)
                        {
                            Console.WriteLine(parser.GetChatMessageSenderChannelUrl(i));
                            Console.WriteLine(parser.GetChatMessageSenderName(i) + ':' + parser.GetChatMessageText(i));
                        }
                    }
                } while (true);

            }

            Console.WriteLine("<Press any key to exit>");
            Console.ReadLine();
        }
    }
}
