using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Web;
using System.Diagnostics;
using System.IO;

namespace ChatInteractiveCommands
{
    abstract class WebAuthenticator
    {
        Dictionary<string, string> login_params;

        HttpListener listener;
        int listener_port;
        
        protected string listener_url;
        protected string login_url;

        public WebAuthenticator(string auth_url)
        {
            login_url = auth_url;
            login_params = new Dictionary<string, string>();
        }

        public void AddLoginParameter(string param_name, string value)
        { 
            login_params.Add(param_name, value);
        }

        protected string BuildLoginParamStr()
        {
            string result = "";
            foreach (var item in login_params)
            {
                if (result.Length > 0)
                {
                    result = result + '&';
                }
                result = result + item.Key + '=' + Uri.EscapeUriString(item.Value);
            }
            return result;
        }

        protected bool StartListener() 
        {
            const int MinPort = 49215;
            const int MaxPort = MinPort+5;

            for (int port = MinPort; port < MaxPort; port++)
            {
                listener = new HttpListener();
                listener_url = $"http://localhost:{port}/";
                listener.Prefixes.Add(listener_url);
                try
                {
                    listener.Start();
                    listener_port = port;
                    return true;
                }
                catch
                {
                    // nothing to do here -- the listener disposes itself when Start throws
                }
            }

            listener_port = 0;
            listener = null;
            listener_url = "";
            return false;
        }

        protected string WaitForTokenInResponse(string token_param_name)
        {
            if (listener != null)
            {
                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;

                    const string responseString = "<HTML><BODY onload='var u = window.location.href; var u2 = u.replace(/#/g, \"?\");  if (!(u===u2)){window.location.href=u2;}; window.setTimeout(\"window.close(); \", 2000);'>Login successfull, you can close this window now.</BODY></HTML>";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

                    HttpListenerResponse response = context.Response;
                    response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);

                    string paramval = request.QueryString.Get(token_param_name);
                    if (paramval != null)
                    {
                        return paramval;
                    }
                }
            }
            return null;
        }

        protected void StopListener()
        {
            if (listener != null)
            {
                listener.Stop();
                listener = null;
                listener_port = 0;
                listener_url = "";
            }
        }

        public abstract string PerformInitialLoginAndExtractToken();
    }

    class OAuth2Authenticator: WebAuthenticator
    {
        string redirect_param;
        string token_param;

        public OAuth2Authenticator(string auth_url, string url_redirect_param, string url_token_param) : base(auth_url)
        {
            redirect_param = url_redirect_param;
            token_param = url_token_param;
        }

        public override string PerformInitialLoginAndExtractToken() {
            if (StartListener())
            {
                string url = login_url + '?' + BuildLoginParamStr() + "&"+ redirect_param +'='+ HttpUtility.UrlEncode(listener_url);

                Process p = System.Diagnostics.Process.Start(url);

                string token = WaitForTokenInResponse(token_param);

                StopListener();

                return token;
            }

            return "";
        }
    }

    class WebToken
    {
        private readonly WebAuthenticator authenticator;
        string token;

        public WebToken(WebAuthenticator a)
        {
            token = "";
            authenticator = a;
        }

        public string GetToken()
        {
            if (token.Length == 0)
            {
                token = authenticator.PerformInitialLoginAndExtractToken();
            }
            return token;
        }
    }
}
