using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class CustomAgentFilesBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly ILogger<CustomAgentFilesBackgroundService> _logger;

    public CustomAgentFilesBackgroundService(
        IServiceProvider serviceProvider,
        IToolFactory<AgentContext> toolFactory,
        ILogger<CustomAgentFilesBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _toolFactory = toolFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInternalInformation("Starting custom agent files download...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var githubSettings = scope.ServiceProvider.GetRequiredService<GitHubSettings>();
            var customAgentFileService = scope.ServiceProvider.GetRequiredService<CustomAgentFileService>();

            CustomAgentFiles? customAgentFiles = null;

            if (!string.IsNullOrEmpty(githubSettings.CustomAgentsRepoPath))
            {
                var githubFileService = scope.ServiceProvider.GetRequiredService<IGithubFileService>();
                var localFolderPath = Path.Combine(
                    Path.GetTempPath(), Constants.ExtendedAgentsRepoPath, Guid.NewGuid().ToString());

                try
                {
                    customAgentFiles = await githubFileService.DownloadYamlFilesInRepoPath(
                        githubSettings.CustomAgentsRepoPath,
                        localFolderPath);

                    var agentFactory = scope.ServiceProvider.GetRequiredService<IAgentFactory<AgentContext>>();
                    agentFactory.LoadExtendedAgentsFromFolder(localFolderPath, true);

                    _toolFactory.FindAndRegisterCustomTools(customAgentFiles);

                    _logger.LogInternalInformation("Successfully downloaded custom agent files");
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to download custom agent files from GitHub");
                    customAgentFileService.SetError(ex);
                    return;
                }
            }

            customAgentFileService.SetFiles(customAgentFiles);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Critical error in custom agent files background service");
        }
    }
}
