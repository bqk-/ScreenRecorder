using IdentityModel.OidcClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ScreenRecorder
{
    public class Identity
    {
        private OidcClient _oidcClient;
        public async Task<ClaimsPrincipal> SignIn()
        {
            // create a redirect URI using an available port on the loopback address.
            // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
            var browser = new Browser(12345);
            string redirectUri = string.Format($"http://localhost:{browser.Port}/");

            var options = new OidcClientOptions
            {
                Authority = System.Configuration.ConfigurationManager.AppSettings["SSO"],
                ClientId = System.Configuration.ConfigurationManager.AppSettings["SSO.Client"],
                ClientSecret = System.Configuration.ConfigurationManager.AppSettings["SSO.Secret"],
                RedirectUri = redirectUri,
                Scope = "openid profile",
                FilterClaims = false,
                Browser = browser
            };

            _oidcClient = new OidcClient(options);
            var result = await _oidcClient.LoginAsync(new LoginRequest());

            if (result.IsError)
            {
                Console.WriteLine("\n\nError:\n{0}", result.Error);
                return null;
            }

            return result.User;
        }
    }
}
