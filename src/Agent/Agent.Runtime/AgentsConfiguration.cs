using System.ComponentModel;
using Agent.Core.Configuration;
using Agent.Runtime.SubAgents;
using Agent.Core;
using Agent.Core.Models;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
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
                .AddSingleton((Func<IServiceProvider, IChatClient>)(sp =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();

                    return new ChatClientBuilder(client.AsChatClient(openAISettings.LLMDeploymentName)).Build();
                }));
        }
    }

    public class MetaAgentPlugin
    {
        ILogger<MetaAgentPlugin> _logger;
        ArchitectureAgent _badArchitectureAgent;
        GenericAgent _genericAgent;
        LogsAndMetricsAgent _logsAndMetricsAgent;
        DiagnosticAgent _diagnosticAgent;
        MCPMetaAgent _mcpMetaAgent;

        GraphDBQueryAgent _graphDBQueryAgent;
        IHttpContextAccessor _httpContextAccessor;
        private ChatHistory _currentChatHistory;
        private const string LastRespondingAgentKey = "LastRespondingAgent";

        public MetaAgentPlugin(
            IChatClient chatClient,
            ArchitectureAgent badArchitectureAgent,
            GenericAgent genericAgent,
            LogsAndMetricsAgent logsAndMetricsAgent,
            DiagnosticAgent diagnosticAgent,
            GraphDBQueryAgent graphDBQueryAgent,
            MCPMetaAgent mcpMetaAgent,
            IHttpContextAccessor httpContextAccessor,
            ILogger<MetaAgentPlugin> logger)
        {
            _logger = logger;
            _badArchitectureAgent = badArchitectureAgent;
            _genericAgent = genericAgent;
            _logsAndMetricsAgent = logsAndMetricsAgent;
            _diagnosticAgent = diagnosticAgent;
            _graphDBQueryAgent = graphDBQueryAgent;
            _mcpMetaAgent = mcpMetaAgent;
            _httpContextAccessor = httpContextAccessor;
            _currentChatHistory = new ChatHistory();
        }

        public void UpdateChatHistory(ChatHistory history)
        {
            _currentChatHistory = history;
        }

        [KernelFunction("architecture_agent")]
        [Description("This agent will answer questions relating to the architecture of a service.")]
        public async Task<string> LaunchBadArchitectureAgentAsync(
             [Description("The question to ask the agent, please include the brief summary to the chat history but be accurate for information like 'id', 'name', 'type', 'url', 'timestamp', etc that directly helps to query from external system.")]
             string question)
        {
            _logger.LogInformation("Invoking architecture agent");
            string answer = await _badArchitectureAgent.Ask(question, _currentChatHistory);
            _logger.LogInformation($"Architecture agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                _httpContextAccessor.HttpContext.Items["LastRespondingAgent"] = "Architecture";
            }

            return answer;
        }

        [KernelFunction("mcp_meta_agent")]
        [Description(
            @"MCP stands for 'Model Context Protocol' which is is an open protocol that enables seamless
integration between LLM applications and external data sources and tools.This agent will delegate to agents
which have the functionality of customer MCP servers. If you ever find can't find a tool to call, first check
with this meta agent to see if it can find the tool."
        )]
        public async Task<string> LaunchMcpServerAgentAsync(
             [Description("The question to ask the agent")]
             string question)
        {
            _logger.LogInformation("Invoking mcp agent");
            string answer = await _mcpMetaAgent.Ask(question, _currentChatHistory);
            _logger.LogInformation($"Mcp agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                _httpContextAccessor.HttpContext.Items["LastRespondingAgent"] = "Mcp";
            }

            return answer;
        }

        [KernelFunction("generic_agent")]
        [Description("If you can't find a better agent, try this agent")]
        public async Task<string> LaunchGenericAgentAsync(
             [Description("The question to ask the agent, please include the brief summary to the chat history but be accurate for information like 'id', 'name', 'type', 'url', 'timestamp', etc that directly helps to query from external system.")]
            string question)
        {
            _logger.LogInformation("Invoking generic agent");

            // Pass the current chat history to the subagent
            string answer = await _genericAgent.Ask(question, _currentChatHistory);

            _logger.LogInformation($"Generic agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                const string AgentType = "Generic";

                _logger.LogInformation($"Setting {LastRespondingAgentKey} to '{AgentType}'");
                _httpContextAccessor.HttpContext.Items[LastRespondingAgentKey] = AgentType;
            }
            else
            {
                _logger.LogWarning("HttpContextAccessor or HttpContext is null, cannot set LastRespondingAgent");
            }

            return answer;
        }

        [KernelFunction("logs_and_metrics_agent")]
        [Description("This agent will answer questions relating to fetch and analyze logs and metrics of a service.")]
        public async Task<string> LaunchLogsAndMetricsAgentAsync(
             [Description("The question to ask the agent, please include the brief summary to the chat history but be accurate for information like 'id', 'name', 'type', 'url', 'timestamp', etc that directly helps to query from external system.")]
            string question)
        {
            _logger.LogInformation("Invoking LogsAndMetrics agent");

            // Pass the current chat history to the subagent
            string answer = await _logsAndMetricsAgent.Ask(question, _currentChatHistory);

            _logger.LogInformation($"LogsAndMetrics agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                const string AgentType = "LogsAndMetrics";

                _logger.LogInformation($"Setting {LastRespondingAgentKey} to '{AgentType}'");
                _httpContextAccessor.HttpContext.Items[LastRespondingAgentKey] = AgentType;
            }
            else
            {
                _logger.LogWarning("HttpContextAccessor or HttpContext is null, cannot set LastRespondingAgent");
            }

            return answer;
        }

        [KernelFunction("diagnose_agent")]
        [Description("This agent will answer questions relating to the diagnosis of a service.")]
        public async Task<string> LaunchDiagnosticAgentAsync(
             [Description("The question to ask the agent, please include the brief summary to the chat history but be accurate for information like 'id', 'name', 'type', 'url', 'timestamp', etc that directly helps to query from external system.")]
            string question)
        {
            _logger.LogInformation("Invoking Diagnostic agent");

            // Pass the current chat history to the subagent
            string answer = await _diagnosticAgent.Ask(question, _currentChatHistory);

            _logger.LogInformation($"Diagnostic agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                const string AgentType = "Diagnostic";

                _logger.LogInformation($"Setting {LastRespondingAgentKey} to '{AgentType}'");
                _httpContextAccessor.HttpContext.Items[LastRespondingAgentKey] = AgentType;
            }
            else
            {
                _logger.LogWarning("HttpContextAccessor or HttpContext is null, cannot set LastRespondingAgent");
            }

            return answer;
        }

        [KernelFunction("graphdbquery_agent")]
        [Description("This agent will answer questions relating to the graph database.")]
        public async Task<string> LaunchGraphDBQueryAgentAsync(
             [Description("The question to ask the agent, please include the brief summary to the chat history but be accurate for information like 'id', 'name', 'type', 'url', 'timestamp', etc that directly helps to query from external system.")]
            string question)
        {
            _logger.LogInformation("Invoking GraphDBQuery agent");

            // Pass the current chat history to the subagent
            string answer = await _graphDBQueryAgent.Ask(question, _currentChatHistory);

            _logger.LogInformation($"GraphDBQuery agent responded with: {answer}");

            if (_httpContextAccessor?.HttpContext?.Items != null)
            {
                const string AgentType = "GraphDBQuery";

                _logger.LogInformation($"Setting {LastRespondingAgentKey} to '{AgentType}'");
                _httpContextAccessor.HttpContext.Items[LastRespondingAgentKey] = AgentType;
            }
            else
            {
                _logger.LogWarning("HttpContextAccessor or HttpContext is null, cannot set LastRespondingAgent");
            }

            return answer;
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

    public Task<LongRunningOperationStatus> StartApprovalFlow(string approvalId)
    {
        var guid = Guid.NewGuid();
        var status = GlobalStatic.ApprovalStatus.GetOrAdd(
            new ApprovalDescriptor(approvalId, "new-approval-flow"),
            new ApprovalStatus(guid.ToString(), DateTime.Now, null, null, null, "new"));

        return Task.FromResult(new LongRunningOperationStatus(guid.ToString(), status.ToString()));
    }
}
