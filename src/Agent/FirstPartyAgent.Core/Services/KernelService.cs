// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Identity;

namespace FirstPartyAgent.Core.Services
{
    public interface IKernelService
    {
        Kernel GetKernelForAgentMode(string agentMode, bool createNew = false);
        List<string> ListAgentModes();
        List<PluginToolInfo> GetAvailablePluginToolInfo(string agentMode);
    }

    public class PluginToolInfo
    {
        public string PluginName { get; set; }
        public string ToolName { get; set; }
        public string Description { get; set; }
        public List<ToolParameter> ToolParameters { get; set; }
    }

    public class ToolParameter
    {
        public string ParameterName { get; set; }
        public string Description { get; set; }
    }

    public class KernelService: IKernelService
    {
        private readonly Dictionary<string, Kernel> _kernels;
        private static HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;

        public KernelService(IServiceProvider sp)
        {
            _serviceProvider = sp;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(300)
            };
            //Enumerate all agent modes from the enum and create a dictionary of agents
            _kernels = new Dictionary<string, Kernel>();
            foreach (AgentMode agentMode in Enum.GetValues(typeof(AgentMode)))
            {
                var kernel = CreateAndConfigureKernel(agentMode, sp);
                if (kernel != null)
                {
                    _kernels.Add(agentMode.ToString(), kernel);
                }
            }
        }

        public Kernel GetKernelForAgentMode(string agentMode, bool createNew = false)
        {
            if (createNew)
            {
                AgentMode agentModeEnum = Enum.Parse<AgentMode>(agentMode, true);
                return CreateAndConfigureKernel(agentModeEnum, _serviceProvider);
            }
            if (_kernels.TryGetValue(agentMode, out var agent))
            {
                return agent;
            }
            throw new ArgumentException($"Agent mode {agentMode} not found");
        }

        public List<string> ListAgentModes()
        {
            return _kernels.Keys.ToList();
        }

        public List<PluginToolInfo> GetAvailablePluginToolInfo(string agentMode)
        {
            var result = new List<PluginToolInfo>();
            var _kernel = GetKernelForAgentMode(agentMode);
            foreach (var plugin in _kernel.Plugins)
            {
                var pluginName = plugin.Name;
                foreach (var tool in plugin.GetFunctionsMetadata())
                {
                    var pluginToolInfo = new PluginToolInfo()
                    {
                        PluginName = pluginName,
                        Description = tool.Description,
                        ToolName = tool.Name,
                        ToolParameters = tool.Parameters.Select(x => new ToolParameter()
                        {
                            ParameterName = x.Name,
                            Description = x.Description
                        }).ToList()
                    };
                    result.Add(pluginToolInfo);
                }
            }
            return result;
        }

        private static Kernel CreateAndConfigureKernel(AgentMode agentMode, IServiceProvider sp)
        {
            var agentPluginList = AgentFinder.GetAgentPlugins(agentMode.ToString());
            var config = sp.GetRequiredService<IConfiguration>();
            var azureSettings = sp.GetRequiredService<IOptions<AzureSettings>>().Value;

            if (azureSettings == null)
            {
                throw new NullReferenceException("Azure settings are required.");
            }

            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton<HttpClient>(_httpClient);
            var openAISettings = azureSettings.OpenAI;

            var _federationSettings = azureSettings.Federation;
            if (!string.IsNullOrWhiteSpace(_federationSettings?.ClientId))
            {
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: openAISettings.LLMDeploymentName,
                    endpoint: openAISettings.Endpoint,
                    new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions()
                    {
                        ClientId = _federationSettings.ClientId,
                        TenantId = _federationSettings.TenantId,
                        AuthorityHost = new Uri(_federationSettings.AuthorityHost),
                    }));
            }
            else if (!string.IsNullOrWhiteSpace(openAISettings.ApiKey))
            {
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: openAISettings.LLMDeploymentName,
                    endpoint: openAISettings.Endpoint,
                    apiKey: openAISettings.ApiKey);
            }
            else if (!string.IsNullOrWhiteSpace(openAISettings.ManagedIdentityClientId))
            {
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: openAISettings.LLMDeploymentName,
                    endpoint: openAISettings.Endpoint,
                    new ManagedIdentityCredential(openAISettings.ManagedIdentityClientId));
            }
            else
            {
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: openAISettings.LLMDeploymentName,
                    endpoint: openAISettings.Endpoint,
                    new DefaultAzureCredential());
            }

            kernelBuilder.Services.AddLogging(builder =>
            {
                // Use configuration for logging levels
                builder.AddConfiguration(config.GetSection("Logging"));
                builder.AddConsole();
            });

            var allPlugins = PluginConfigLoader.Plugins;
            var agentPlugins = allPlugins.Where(p => agentPluginList.Contains(p.PluginName)).ToList();
            foreach (var plugin in agentPlugins)
            {
                var pluginType = Type.GetType(plugin.ServiceIdentifier);
                if (pluginType != null)
                {
                    kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService(pluginType), plugin.PluginName);
                }
            }

            var kernel = kernelBuilder.Build();
            kernel.Data["agentMode"] = agentMode.ToString();
            return kernel;
        }
    }
}

