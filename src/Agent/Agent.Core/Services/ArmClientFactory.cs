// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Azure.Core;
using Azure.ResourceManager;

namespace Agent.Core.Services;

public class ArmClientFactory : IArmClientFactory
{
    private readonly IAuthenticationService _authService;

    private Lazy<ArmClient> _crawlerClient;

    public ArmClientFactory(IAuthenticationService authService)
    {
        _authService = authService;

        _crawlerClient = new Lazy<ArmClient>(() => ConstructArmClient(_authService.GetCrawlerCredential()));
    }

    public async Task<ArmClient> GetArmOperationClient()
    {
        var cred = await _authService.GetArmOperationCredential();

        return ConstructArmClient(cred);
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

