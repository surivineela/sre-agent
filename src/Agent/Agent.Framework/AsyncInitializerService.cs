// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Framework;

public class AsyncInitializerService : IHostedService
{
    private readonly IEnumerable<IAsyncInitializer> _initializers;

    private bool _initialized = false;

    public AsyncInitializerService(IEnumerable<IAsyncInitializer> initializers)
    {
        _initializers = initializers;
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(_initializers.Select(i => i.InitializeAsync()));
        _initialized = true;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public bool IsInitialized() => _initialized;
}

public static class AsyncInitializerServiceExtensions
{
    public static IServiceCollection ConfigureFrameworkAsyncInitializers<T>(this IServiceCollection services) where T : class
    {
        return services
            .AddSingleton<IAsyncInitializer>(sp => sp.GetRequiredService<IToolFactory<T>>())
            .AddSingleton<IAsyncInitializer>(sp => sp.GetRequiredService<IAgentFactory<T>>())
            .AddSingleton<IAsyncInitializer>(sp => sp.GetRequiredService<ISkillRegistry>())
            .AddSingleton<AsyncInitializerService>()
            .AddHostedService(sp => sp.GetRequiredService<AsyncInitializerService>());
    }
}
