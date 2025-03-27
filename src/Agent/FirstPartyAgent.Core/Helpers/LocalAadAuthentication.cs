using Azure.Core;
using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Helpers
{
    public static class LocalAadAuthenticator
    {
        private static DefaultAzureCredential _credential;

        public static void Initialize()
        {
            var options = new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = true,
                ExcludeWorkloadIdentityCredential = false,
                ExcludeSharedTokenCacheCredential = false,
                ExcludeVisualStudioCredential = false,
                ExcludeVisualStudioCodeCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeInteractiveBrowserCredential = false,
                ExcludeAzurePowerShellCredential = false
            };

            _credential = new DefaultAzureCredential(options);
        }

        /// <summary>
        /// Using client AAD app "Microsoft Azure CLI" 04b07795-8ddb-461a-bbee-02f9e1bf7b46 to acquire token
        /// </summary>
        public static async Task<string> AcquireTokenAsync(params string[] scopes)
        {
            var tokenRequestContext = new TokenRequestContext(scopes);
            AccessToken token = await _credential.GetTokenAsync(tokenRequestContext);

            return "Bearer " + token.Token;
        }
    }
}
