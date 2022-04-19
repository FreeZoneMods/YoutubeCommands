using System.IO;
using System.Net;
using Newtonsoft.Json;

namespace ChatInteractiveCommands
{
    public class JsonRequestor
    {
        public static T ExecuteRequest<T>(WebRequest request)
        {
            WebResponse responce = request.GetResponse();
            var webStream = responce.GetResponseStream();
            var reader = new StreamReader(webStream);

            JsonSerializer s = new JsonSerializer();
            return (T)s.Deserialize(reader, typeof(T));
        }
    }
}
