using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.YouTube.v3;

namespace ChatInteractiveCommands
{
    public class GoogleAuth
    {
        private UserCredential _creds;
        private ClientSecrets _secrets;
        IEnumerable<string> _scopes;

        public GoogleAuth(Stream credstream, IEnumerable<string> scopes)
        {
            _secrets = GoogleClientSecrets.Load(credstream).Secrets;
            _scopes = scopes;
        }

        public UserCredential GetCreds()
        {
            if (_creds == null)
            {
                GetTokenTask().Wait();
            } else if (_creds.Token.IsExpired(Google.Apis.Util.SystemClock.Default)){
                RefreshTokenTask().Wait();
            }
            return _creds;
        }

        private async Task GetTokenTask()
        {
            _creds = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                _secrets,
                _scopes,
                "user",
                CancellationToken.None);
        }

        private async Task RefreshTokenTask()
        {
            await _creds.RefreshTokenAsync(CancellationToken.None);
        }



    }
}
