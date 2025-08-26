using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services.TokenService
{
    public abstract class ManagedIdentityTokenServiceBase
    {
        private bool tokenAcquiredAtleastOnce;
        private ValueTask<AccessToken> acquireTokenTask;

        protected abstract bool ManagedIdentityEnabled { get; set; }

        /// <summary>
        /// Gets AAD issued auth token.
        /// </summary>
        public string AuthorizationToken { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets AAD Resource.
        /// </summary>
        protected abstract string Resource { get; set; }

        /// <summary>
        ///  user-assigned managed identity client ID
        /// </summary>
        protected abstract string ClientId { get; set; }

        /// <summary>
        /// Gets or sets token service name used for logging to Kusto.
        /// </summary>
        protected abstract string TokenServiceName { get; set; }

        protected abstract TokenCredential TokenCredential { get; set; }
        protected abstract TokenRequestContext TokenRequestContext { get; set; }

        public static class TokenServiceConstants
        {
            public const int TokenRefreshIntervalInMs = 10 * 60 * 1000; //10 minutes
        }

        /// <summary>
        /// Acquires Security Token from MSI for the given <see cref="Resource"/>.
        /// </summary>
        public async Task StartTokenRefresh(ILogger logger)
        {
            if (!ManagedIdentityEnabled)
            {
                return;
            }
            SetTokenCredentials();
            while (true)
            {
                DateTime invocationStartTime = DateTime.UtcNow;
                string exceptionType = string.Empty;
                string exceptionDetails = string.Empty;
                string message = string.Empty;

                try
                {

                    acquireTokenTask = TokenCredential.GetTokenAsync(TokenRequestContext, new System.Threading.CancellationToken());
                    AccessToken token = await acquireTokenTask;
                    AuthorizationToken = GetAuthTokenFromValueTask(token);
                    tokenAcquiredAtleastOnce = true;
                    message = $"Token Acquisition Status Client id {ClientId} Resource id {Resource} : Success";
                }
                catch (Exception ex)
                {
                    exceptionType = ex.GetType().ToString();
                    exceptionDetails = ex.ToString();
                    message = $"Token Acquisition Status Client id {ClientId} Resource id {Resource} : Failed, Reason {exceptionDetails}";
                }
                finally
                {
                    DateTime invocationEndTime = DateTime.UtcNow;
                    long latencyInMs = Convert.ToInt64((invocationEndTime - invocationStartTime).TotalMilliseconds);
                    logger.LogInformation(
                       TokenServiceName,
                        message,
                        latencyInMs,
                        invocationStartTime.ToString("HH:mm:ss.fff"),
                        invocationEndTime.ToString("HH:mm:ss.fff"),
                        exceptionType,
                        exceptionDetails);
                }

                await Task.Delay(TokenServiceConstants.TokenRefreshIntervalInMs);
            }
        }

        private void SetTokenCredentials()
        {
            var authOptions = new DefaultAzureCredentialOptions();
            // Use Managed Identity only when in a MSI supported environment like app service
            // This will help default to VS credentials when developing locally
            if (Environment.GetEnvironmentVariable("MSI_ENDPOINT") != null)
            {
                authOptions.ManagedIdentityClientId = ClientId;
            }
            TokenCredential = new DefaultAzureCredential(authOptions); // CodeQL [SM05137] This is non-production code which is deprecated and not deployed.
            TokenRequestContext = new TokenRequestContext(scopes: new string[] { Resource });
        }

        /// <summary>
        /// Gets AAD issued auth token.
        /// </summary>
        public virtual async Task<string> GetAuthorizationTokenAsync()
        {
            if (!ManagedIdentityEnabled)
            {
                return string.Empty;
            }
            if (TokenCredential == null)
            {
                SetTokenCredentials();
            }
            if (!tokenAcquiredAtleastOnce)
            {
                var authResult = await acquireTokenTask;
                return GetAuthTokenFromValueTask(authResult);
            }

            return AuthorizationToken;
        }

        private string GetAuthTokenFromValueTask(AccessToken accessToken)
        {
            return accessToken.Token;
        }
    }
}
