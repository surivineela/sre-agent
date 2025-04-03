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

namespace FirstPartyAgent.Core.Services
{
    public interface IKernelService
    {
        Kernel GetKernelForAgentMode(string agentMode);
        List<string> ListAgentModes();
    }

    public class KernelService: IKernelService
    {
        private readonly Dictionary<string, Kernel> _kernels;
        private static HttpClient _httpClient;

        public KernelService(IServiceProvider sp)
        {
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

        public Kernel GetKernelForAgentMode(string agentMode)
        {
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
            kernelBuilder.AddAzureOpenAIChatCompletion(
               deploymentName: azureSettings.OpenAI.LLMDeploymentName,
               endpoint: azureSettings.OpenAI.Endpoint,
               apiKey: azureSettings.OpenAI.ApiKey);


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

