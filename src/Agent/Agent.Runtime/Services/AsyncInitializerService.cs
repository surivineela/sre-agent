// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Microsoft.Extensions.Hosting;

namespace Agent.Runtime.Services;

public class AsyncInitializerService : IHostedService
{
    private readonly IEnumerable<IAsyncInitializer> _initializers;

    public AsyncInitializerService(IEnumerable<IAsyncInitializer> initializers)
    {
        _initializers = initializers;
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        foreach (var init in _initializers)
        {
            await init.InitializeAsync();
        }
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

