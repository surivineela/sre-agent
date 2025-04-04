// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Configuration;
using Agent.Core;
using Agent.Core.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Agent.Plugins.Definitions;

namespace Agent.Runtime
{
    public static class AgentsConfigurationExtensions
    {
        public static IServiceCollection ConfigureIChatCompletionService(this IServiceCollection services)
        {
            return services
                .AddSingleton((Func<IServiceProvider, IChatCompletionService>)(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();

                    return (IChatCompletionService)new AzureOpenAIChatCompletionService(
                        deploymentName: openAISettings.LLMDeploymentName,
                        endpoint: openAISettings.Endpoint,
                        apiKey: openAISettings.ApiKey
                    );
                }));
        }

        public static IServiceCollection ConfigureAzureOpenAIClient(this IServiceCollection services)
        {
            return services
                .AddSingleton((Func<IServiceProvider, AzureOpenAIClient>)(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();

                    return new AzureOpenAIClient(
                        endpoint: new Uri(openAISettings.Endpoint),
                        credential: new System.ClientModel.ApiKeyCredential(openAISettings.ApiKey)
                    );
                }));
        }

        public static IServiceCollection ConfigureIChatClient(this IServiceCollection services)
        {
            return services
                .AddSingleton<IChatClient>((Func<IServiceProvider, IChatClient>)(sp =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory= sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.AsChatClient(openAISettings.LLMDeploymentName))
                        .UseLogging(loggerFactory)
                        .Build();
                }))
                .AddKeyedSingleton<IChatClient>("function-invocation-enabled", (sp, _) =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.AsChatClient(openAISettings.LLMDeploymentName))
                        .UseLogging(loggerFactory)
                        .UseFunctionInvocation(loggerFactory, x =>
                        {
                            x.IncludeDetailedErrors = true;
                        })
                        .Build();
                });
        }
    }

}

public class ApprovalPlugin : IApprovalPlugin
{
    [KernelFunction("start_approval_process")]
    [Description("To start a new approval process for user to approve a specific remediation operation for a given resource.")]
    public ApprovalStatus StartApprovalProcess(
        [Description("The resource ID of the App Service.")]
        string resourceId,
        [Description("The name of remediation operation that to be approved.")]
        string operationName,
        [Description("The concise description of what the operation is doing to be displayed on the approval page")]
        string operationDescription)
    {
        var guid = Guid.NewGuid();
        return GlobalStatic.ApprovalStatus.GetOrAdd(
            new ApprovalDescriptor(resourceId, operationName),
            new ApprovalStatus(guid.ToString(), DateTime.Now, null, null, null, operationDescription));
    }

    [KernelFunction("get_approval_status")]
    [Description("To get the status of an approval, returns null if the approval process hasn't started.")]
    public ApprovalStatus? GetApprovalStatus(
        [Description("The resource ID of the App Service.")]
        string resourceId,
        [Description("The name of remediation operation that to be approved.")]
        string operationName)
    {
        return GlobalStatic.ApprovalStatus.TryGetValue(new ApprovalDescriptor(resourceId, operationName), out var status)
            ? status
            : null;
    }

    public Task<LongRunningOperationStatus> StartApprovalFlow(string approvalId, string description)
    {
        var guid = Guid.NewGuid();
        var status = GlobalStatic.ApprovalStatus.GetOrAdd(
            new ApprovalDescriptor(approvalId, "new-approval-flow"),
            new ApprovalStatus(guid.ToString(), DateTime.Now, null, null, null, "new"));

        return Task.FromResult(new LongRunningOperationStatus(guid.ToString(), status.ToString()));
    }
}

