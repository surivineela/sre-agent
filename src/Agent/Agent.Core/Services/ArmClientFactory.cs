// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Azure.Core;
using Azure.ResourceManager;

namespace Agent.Core.Services;

public class ArmClientFactory : IArmClientFactory
{
    private readonly IAuthenticationService _authService;

    private Lazy<ArmClient> _armClient;
    private Lazy<ArmClient> _crawlerClient;

    public ArmClientFactory(IAuthenticationService authService)
    {
        _authService = authService;

        _armClient = new Lazy<ArmClient>(() => ConstructArmClient(_authService.GetArmOperationCredential()));
        _crawlerClient = new Lazy<ArmClient>(() => ConstructArmClient(_authService.GetCrawlerCredential()));
    }

    public ArmClient GetArmClient()
    {
        return _armClient.Value;
    }

    public ArmClient GetCrawlerArmClient()
    {
        return _crawlerClient.Value;
    }

    private ArmClient ConstructArmClient(TokenCredential cred)
    {
        var options = new ArmClientOptions
        {
            Diagnostics =
            {
#if DEBUG
                // log request and response content
                IsLoggingContentEnabled = true,
                // don't redact any headers for debugging
                LoggedHeaderNames = {"*"},
                LoggedQueryParameters = {"*"}, 

#else
                IsLoggingContentEnabled = false,
#endif
                IsLoggingEnabled = true,
            },
        };

        return new ArmClient(cred, default, options);
    }
}

