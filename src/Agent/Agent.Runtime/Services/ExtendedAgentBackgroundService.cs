using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class ExtendedAgentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly ILogger<ExtendedAgentBackgroundService> _logger;

    public ExtendedAgentBackgroundService(
        IServiceProvider serviceProvider,
        IToolFactory<AgentContext> toolFactory,
        ILogger<ExtendedAgentBackgroundService> logger
        )
    {
        _serviceProvider = serviceProvider;
        _toolFactory = toolFactory;
        _logger = logger;

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();

        // load agents stored in Cosmos
        var extendedAgentService = scope.ServiceProvider.GetRequiredService<IExtendedAgentService>();
        await extendedAgentService.RefreshAgentAndToolsRegisterationsAsync();
    }
}
