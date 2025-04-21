// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services;

// [OPTIONAL] 
public interface IHelloWorldService
{
    Task<string> DummyHelloMessageAsync();
}

// [OPTIONAL] Implement this service only if doing heaving lifting like Network calls, DB calls, etc. and/or optionally using the HelloWorldSettings to control the behaviour of the service.
public class HelloWorldService: IHelloWorldService
{
    private readonly ILogger<HelloWorldService> _logger;
    private readonly HelloWorldSettings settings;

    // Note: The HelloWorldSettings is auto injected by the DI container via our top-level sub-agent loader code.
    public HelloWorldService(ILogger<HelloWorldService> logger, HelloWorldSettings settings)
    {
        _logger = logger;
        this.settings = settings;
    }

    public async Task<string> DummyHelloMessageAsync()
    {
        await Task.Delay(1); // Simulate asynchronous operation
        if (settings.Enabled)
        {
            return "Hello, World! from a fundamental plugin (enabled)";
        }
        else
        {
            return "Hello, World! from a fundamental plugin (disabled)";
        }
    }
}
