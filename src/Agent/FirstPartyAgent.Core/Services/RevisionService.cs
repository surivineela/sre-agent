// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services;

// [OPTIONAL] 
public interface IRevisionService
{
    Task<string> DummyHelloMessageAsync();
}

// [OPTIONAL] Implement this service only if doing heaving lifting like Network calls, DB calls, etc. and/or optionally using the RevisionSettings to control the behaviour of the service.
public class RevisionService: IRevisionService
{
    private readonly ILogger<RevisionService> _logger;
    private readonly RevisionSettings settings;

    // Note: The RevisionSettings is auto injected by the DI container via our top-level sub-agent loader code.
    public RevisionService(ILogger<RevisionService> logger, RevisionSettings settings)
    {
        _logger = logger;
        this.settings = settings;
    }

    public async Task<string> DummyHelloMessageAsync()
    {
        await Task.Delay(1); // Simulate asynchronous operation
        if (settings.Enabled)
        {
            return "Revision Test,Revision test2";
        }
        else
        {
            return "Revision Test,Revision test2";
        }
    }
}
