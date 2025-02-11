using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OperationalAgentCore.Models;
using Model = OperationalAgentCore.Models;

namespace OperationalAgentCore;

public static class SemanticKernelHelper
{
    public static void ConfigService(IServiceCollection serviceCollection)
    {
        serviceCollection.AddHostedService<RemediationWorker>();

        serviceCollection.AddScoped<AppIdentityUpdatePlugin>();
        serviceCollection.AddScoped<ApprovalPlugin>();
        serviceCollection.AddScoped<ChartPlugin>();
        serviceCollection.AddScoped<CodeAnalyzerPlugin>();
        serviceCollection.AddScoped<CurrentStatePlugin>();
        serviceCollection.AddScoped<Models.GitHubClient>();
        serviceCollection.AddScoped<GithubIssuePlugin>();
        serviceCollection.AddScoped<MemoryAnalysisPlugin>();
        serviceCollection.AddScoped<PeriodicRemediationPlugin>();
        serviceCollection.AddScoped<SubscriptionPlugin>();
        serviceCollection.AddScoped<TlsPlugin>();
        
        serviceCollection.AddSingleton<CodeAnalyzerService>();
        serviceCollection.AddSingleton<ITaskClient, TaskClient>();
        serviceCollection.AddSingleton<TeamsConnector>();
        serviceCollection.AddScoped<IcmPlugin>();
        serviceCollection.AddScoped<ICMApprovalPlugin>();


        // Configure Semantic Kernel
        serviceCollection.AddScoped<Kernel>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();

            var azureSettings = config.GetSection("Azure").Get<AzureSettings>();

            if (azureSettings == null)
            {
                throw new NullReferenceException("Azure settings are required.");
            }

            var kernelBuilder = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: azureSettings.OpenAI.DeploymentName,
                    endpoint: azureSettings.OpenAI.Endpoint,
                    apiKey: azureSettings.OpenAI.ApiKey);

            string agentModeStr = config.GetValue("AgentMode", string.Empty);
            var agentMode = Enum.TryParse<AgentMode>(agentModeStr, out var mode) ? mode : AgentMode.SREAgent;

            // common/generic plugins
            var curlPlugin = sp.GetRequiredService<TlsPlugin>();
            kernelBuilder.Plugins.AddFromObject(curlPlugin, "CurlPlugin");

            // kernelBuilder.Plugins.AddFromType<MonitorPlugin>("MonitorPlugin");

            if (agentMode == AgentMode.SREAgent)
            {
                // Register skills for SRE Agent
                kernelBuilder.Plugins.AddFromType<ApprovalPlugin>("ApprovalPlugin");
                kernelBuilder.Plugins.AddFromType<DiagnosePlugin>("DiagnosePlugin");
                kernelBuilder.Plugins.AddFromType<MetricsPlugin>("MetricsPlugin");
                kernelBuilder.Plugins.AddFromType<RemediationPlugin>("RemediationPlugin");
                kernelBuilder.Plugins.AddFromType<AppConfigurationChecksPlugin>("SqlConnectionPlugin");
                kernelBuilder.Plugins.AddFromType<TimePlugin>("TimePlugin");

                var createGithubWorkItemPlugin = sp.GetRequiredService<GithubIssuePlugin>();
                kernelBuilder.Plugins.AddFromObject(createGithubWorkItemPlugin, "CreateGithubWorkItemPlugin");

                var subscriptionPlugin = sp.GetRequiredService<SubscriptionPlugin>();
                kernelBuilder.Plugins.AddFromObject(subscriptionPlugin, "SubscriptionPlugin");

                var TestEmbeddingPlugin = sp.GetRequiredService<TestEmbeddingPlugin>();
                kernelBuilder.Plugins.AddFromObject(TestEmbeddingPlugin, "TestEmbeddingPlugin");

                var repoPlugin = sp.GetRequiredService<CodeAnalyzerPlugin>();
                kernelBuilder.Plugins.AddFromObject(repoPlugin, "CodeAnalyzerPlugin");

                var memAnalysisPlugin = sp.GetRequiredService<MemoryAnalysisPlugin>();
                kernelBuilder.Plugins.AddFromObject(memAnalysisPlugin, "MemoryAnalysisPlugin");

                var appIdentityUpdatePlugin = sp.GetRequiredService<AppIdentityUpdatePlugin>();
                kernelBuilder.Plugins.AddFromObject(appIdentityUpdatePlugin, "AppIdentityUpdatePlugin");

                var chartPlugin = sp.GetRequiredService<ChartPlugin>();
                kernelBuilder.Plugins.AddFromObject(chartPlugin, "ChartPlugin");

                var periodicRemPlugin = sp.GetRequiredService<PeriodicRemediationPlugin>();
                kernelBuilder.Plugins.AddFromObject(periodicRemPlugin, "PeriodicRemediationPlugin");
            }
            else if(agentMode == AgentMode.ICM)
            {
                var icmPlugin = sp.GetRequiredService<IcmPlugin>();
                kernelBuilder.Plugins.AddFromObject(icmPlugin, "IcmPlugin");
                kernelBuilder.Plugins.AddFromType<ICMApprovalPlugin>("ICMApprovalPlugin");
            }
            
            return kernelBuilder.Build();
        });
    }
}

