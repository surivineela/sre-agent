using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.Repositories;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.Services;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Agent.Evals;

public static class TestHelpers
{
    public static HostApplicationBuilder BuildTestApp(out string? outLLMDeploymentName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.LoadAppSettings(isDevelopment: true);
        builder.RegisterAppSettingsNoValidation<AppSettings>();

        var llmDeploymentName = builder.Configuration["AppSettings:Core:Azure:OpenAI:LLMDeploymentName"];

        if (string.IsNullOrEmpty(llmDeploymentName))
        {
            Console.WriteLine("Eval pipeline doesn't use appsettings. Using OpenAI API key and model from TestRunParameters.");

            string? apiKey = builder.Configuration["OpenAIKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is missing. Pass it as a TestRunParameter.");
            }

            llmDeploymentName = builder.Configuration["OpenAIModel"];
            if (string.IsNullOrEmpty(llmDeploymentName))
            {
                throw new InvalidOperationException("OpenAI API model is missing. Pass it as a TestRunParameter.");
            }

            string? aiEndpoint = builder.Configuration["OpenAIEndpoint"];
            if (string.IsNullOrEmpty(aiEndpoint))
            {
                throw new InvalidOperationException("OpenAI API endpoint is missing. Pass it as a TestRunParameter.");
            }

            builder.Services.AddSingleton(new AzureOpenAIClient(new Uri(aiEndpoint), new System.ClientModel.ApiKeyCredential(apiKey)));
        }
        else
        {
            Console.WriteLine("Eval pipeline is using appsettings. Please make sure you have proper values in appsettings.json.");
            builder.Services.ConfigureAzureOpenAIClient();
        }

        builder.Services.AddLogging(builder =>
        {
            builder.AddConsole();
        });

        builder.Services.AddChatClient(sp => sp.GetRequiredService<AzureOpenAIClient>().AsChatClient(llmDeploymentName));
        outLLMDeploymentName = llmDeploymentName;

        builder.Services.AddSingleton<IThreadOrchestrationManager, InMemoryThreadOrchestrationManager>().
                        AddSingleton<IThreadRepository, InmemoryThreadRepository>().
                        AddSingleton<ThreadService>().
                        AddSingleton<SinkService>().
                        // NOTE: use mock for teams plugin as we don't rely on teams for Agent Eval.
                        AddSingleton(sp => new Mock<IPostToTeamsPlugin>().Object).
                        AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>();
        // These plugins don't have any dependencies on appsettings.json
        builder.Services.AddSingleton<ITimePlugin, TimePlugin>()
                        .AddSingleton<IRecordActionsPlugin, RecordActionsPlugin>();

        return builder;
    }
}
