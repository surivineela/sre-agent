using Agent.Core.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services.TokenService
{
    public abstract class ManagedIdentityTokenServiceBase
    {
        private bool tokenAcquiredAtleastOnce;
        private ValueTask<AccessToken> acquireTokenTask;

        protected abstract bool ManagedIdentityEnabled { get; set; }

        /// <summary>
        /// Gets AAD issued auth token.
        /// </summary>
        public string AuthorizationToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets AAD Resource.
        /// </summary>
        protected abstract string Resource { get; set; }

        /// <summary>
        ///  user-assigned managed identity client ID
        /// </summary>
        protected abstract string ClientId { get; set; }

        /// <summary>
        /// user-assigned managed identity resource ID
        /// </summary>
        protected abstract string? ResourceId { get; set; }

        /// <summary>
        /// Gets or sets token service name used for logging to Kusto.
        /// </summary>
        protected abstract string TokenServiceName { get; set; }

        protected abstract IAuthenticationService authenticationService { get; set; }

        protected abstract TokenCredential? TokenCredential { get; set; }
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
                    if (TokenCredential != null)
                    {
                        acquireTokenTask = AcquireToken(TokenRequestContext, new System.Threading.CancellationToken());
                        AccessToken token = await acquireTokenTask;
                        AuthorizationToken = GetAuthTokenFromValueTask(token);
                        tokenAcquiredAtleastOnce = true;
                        logger.LogInternalInformation($"[{TokenServiceName}]Token Acquisition Status Client id {ClientId} Resource id {Resource} : Success");
                    }
                }
                catch (Exception ex)
                {
                    exceptionType = ex.GetType().ToString();
                    exceptionDetails = ex.ToString();
                    logger.LogInternalError($"[{TokenServiceName}]Token Acquisition Status Client id {ClientId} Resource id {Resource} : Failed, Reason {exceptionDetails}");
                }

                await Task.Delay(TokenServiceConstants.TokenRefreshIntervalInMs);
            }
        }

        private void SetTokenCredentials()
        {
            TokenCredential = authenticationService.GetIcmApiCredential();
            TokenRequestContext = new TokenRequestContext(scopes: new string[] { Resource });
        }

        /// <summary>
        /// Virtual method to acquire token. Can be overridden by derived classes to use different token acquisition methods.
        /// </summary>
        /// <param name="requestContext">The token request context</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A ValueTask that represents the asynchronous token acquisition operation</returns>
        protected virtual ValueTask<AccessToken> AcquireToken(TokenRequestContext requestContext, System.Threading.CancellationToken cancellationToken)
        {
            if (TokenCredential == null)
            {
                throw new InvalidOperationException($"[{TokenServiceName}] TokenCredential is not initialized");
            }
            return TokenCredential.GetTokenAsync(requestContext, cancellationToken);
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
                var authResult = await AcquireToken(TokenRequestContext, new System.Threading.CancellationToken());
                return GetAuthTokenFromValueTask(authResult);
            }

            return AuthorizationToken;
        }

        private string GetAuthTokenFromValueTask(AccessToken accessToken)
        {
            if (string.IsNullOrEmpty(accessToken.Token))
            {
                throw new InvalidOperationException($"[{TokenServiceName}]Failed to acquire token from Managed Identity for Resource: {Resource}, ClientId: {ClientId}");
            }
            return accessToken.Token;
        }
    }
}
