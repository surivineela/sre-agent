// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class HelloWorldPlugin : IHelloWorldPlugin
{
    private readonly ILogger<HelloWorldPlugin> _logger;
    private readonly IHelloWorldService _helloWorldService;

    public HelloWorldPlugin(ILogger<HelloWorldPlugin> logger, IHelloWorldService helloWorldService)
    {
        _logger = logger;
        _helloWorldService = helloWorldService;
    }

    public Task<string> GetHelloWorldMessageAsync()
    {
        return _helloWorldService.DummyHelloMessageAsync();
    }
}
