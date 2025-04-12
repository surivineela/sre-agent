// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Models.Attributes;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Reflection;

namespace FirstPartyAgent.Core.Helpers
{
    public static class AgentFinder
    {
        private static IStorageService _storageService;
        private static ICosmosDBService? _cosmosDbService;

        private static string icmAlertConfigsContainerName = "icmalertconfigs";
        private static Dictionary<string, ICMAlertConfig> _icmAlertConfigs
            = new Dictionary<string, ICMAlertConfig>(StringComparer.OrdinalIgnoreCase);

        private static string hotsiteAgentConfigCosmosDb = "HotsiteAgent";
        private static string hotsiteAgentConfigCosmosDbContainer = "IcmAlertConfigs";

        private static IServiceProvider _serviceProvider;
        private static IConfiguration _config;
        private static string _tempCustomConfig;
        private static Dictionary<string, List<string>> AgentPluginsConfig = new Dictionary<string, List<string>>()
        {
            { "None", new List<string>(){ "KustoPlugin", "TimePlugin", "HttpRequestPlugin" } },
            { "Sev2", new List<string>(){ "KustoPlugin", "IcmPlugin", "GenevaActionsPlugin", "ICMChartPlugin", "WebAppPlugin", "AzureAlertingPlugin", "TimePlugin", "HttpRequestPlugin" } },
            { "ICMAgent", new List<string>(){ "KustoPlugin", "IcmPlugin", "RedisGenevaActionsPlugin", "ICMChartPlugin", "AzureAlertingPlugin" } },
            { "MFP", new List<string>(){ "IcmPlugin", "GenevaActionsPlugin", "KustoPlugin", "TeamsPlugin" } },
            { "GithubIssueTagger", new List<string>() { "GitHubIssuePlugin", "AzureSearchPlugin" } },
            { "ICMSummarizer", new List<string>(){ "IcmPlugin" } }
        };

        public static Dictionary<string, List<string>> AgentDataParsingConfig = new Dictionary<string, List<string>>()
        {
            { "Hotsite", new List<string>(){ "IncidentId" } },
            { "Sev2", new List<string>(){ "IncidentId" } },
            { "ICMAgent", new List<string>(){ "IncidentId" } },
            { "MFP", new List<string>(){ "IncidentId" } },
            { "GithubIssueTagger", new List<string>(){ "IssueId", "CommentId" } }
        };

        public static List<string> ListAgentModes()
        {
            return Enum.GetNames(typeof(AgentMode)).ToList();
        }

        public static List<string> GetAgentPlugins(string agentMode)
        {
            if (AgentPluginsConfig.TryGetValue(agentMode, out var plugins))
            {
                return plugins;
            }
            return new List<string>();
        }

        public static async Task<Dictionary<string, ICMAlertConfig>> GetICMAlertConfigsAsync(bool reload = false)
        {
            if (reload)
            {
                await ReloadAsync();
            }

            return _icmAlertConfigs;
        }

        public static async Task<ICMAlertConfig> GetICMAlertConfigAsync(string alertingId)
        {
            ICMAlertConfig config;
            if (_cosmosDbService != null && _cosmosDbService.IsEnabled)
            {
                var queryable = _cosmosDbService.GetQueryableContainer<ICMAlertConfig>(hotsiteAgentConfigCosmosDb, hotsiteAgentConfigCosmosDbContainer);

                var result = await queryable.Where(x => x.AlertingId == alertingId).ToListAsync();
                config = result.FirstOrDefault();

                if (config != null)
                {
                    return config;
                }
            }


            if (_icmAlertConfigs.TryGetValue(alertingId, out config))
            {
                return config;
            }
            return null;
        }

        public static async Task<string> SaveICMAlertConfig(string alertingId, string customConfig)
        {
            if (!_storageService.IsEnabled)
            {
                var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ICMAlertConfigs");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                var filePath = Path.Combine(folderPath, $"{alertingId}.json");
                File.WriteAllText(filePath, customConfig);
            }
            // If using storage service, upload the file
            if (_storageService.IsEnabled)
            {
                await _storageService.WriteContentToStorage(icmAlertConfigsContainerName, $"{alertingId}.json", customConfig);
            }
            // Update the in-memory dictionary
            var config = JsonConvert.DeserializeObject<ICMAlertConfig>(customConfig);
            _icmAlertConfigs[alertingId] = config;
            return string.Empty;
        }

        public static void Initialize(IServiceProvider serviceProvider, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _config = config;

            _cosmosDbService = _serviceProvider.GetService<ICosmosDBService>();
            _storageService = _serviceProvider.GetService<IStorageService>();

            if(_cosmosDbService != null && _cosmosDbService.IsEnabled)
            {
                LoadICMAlertConfigsFromCosmosDb();
            }
            else if (_storageService != null && _storageService.IsEnabled)
            {
                LoadICMAlertConfigsFromStorage();
            }
            else
            {
                LoadICMAlertConfigsFromLocal();
            }
        }

        public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _config = config;

            _cosmosDbService = _serviceProvider.GetService<ICosmosDBService>();
            _storageService = _serviceProvider.GetService<IStorageService>();

            await ReloadAsync();
        }

        public static async Task ReloadAsync()
        {
            if (_cosmosDbService != null && _cosmosDbService.IsEnabled)
            {
                await LoadICMAlertConfigsFromCosmosDbAsync();
            }
            else if (_storageService != null && _storageService.IsEnabled)
            {
                LoadICMAlertConfigsFromStorage();
            }
            else
            {
                LoadICMAlertConfigsFromLocal();
            }
            
        }


        // better to be an async method
        private static void LoadICMAlertConfigsFromCosmosDb()
        {
            if (_cosmosDbService != null && _cosmosDbService.IsEnabled)
            {
                var configs = _cosmosDbService.GetQueryableContainer<ICMAlertConfig>(hotsiteAgentConfigCosmosDb, hotsiteAgentConfigCosmosDbContainer).ToList();
                foreach (var config in configs)
                {
                    _icmAlertConfigs[config.AlertingId] = config;
                }
            }
        }

        private static async Task LoadICMAlertConfigsFromCosmosDbAsync()
        {
            if (_cosmosDbService != null && _cosmosDbService.IsEnabled)
            {
                var configs = await _cosmosDbService.GetQueryableContainer<ICMAlertConfig>(hotsiteAgentConfigCosmosDb, hotsiteAgentConfigCosmosDbContainer).ToListAsync();
                foreach (var config in configs)
                {
                    _icmAlertConfigs[config.AlertingId] = config;
                }
            }
        }

        private static void LoadICMAlertConfigsFromStorage()
        {
            var blobNames = _storageService.ListFilesInContainer(icmAlertConfigsContainerName).GetAwaiter().GetResult();
            foreach (var blobName in blobNames)
            {
                var jsonContent = _storageService.ReadFileFromStorage(icmAlertConfigsContainerName, blobName).Result;
                var config = JsonConvert.DeserializeObject<ICMAlertConfig>(jsonContent);
                _icmAlertConfigs[config.AlertingId] = config;
            }
        }

        private static void LoadICMAlertConfigsFromLocal()
        {
            var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ICMAlertConfigs");
            if (!Directory.Exists(folderPath))
            {
                // If the folder does not exist, you might want to log or handle it differently
                // For now, we'll just return without loading anything.
                return;
            }
            // Load all .json files in the ICMAlertConfigs folder
            var jsonFiles = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var jsonFile in jsonFiles)
            {
                // Value: the entire JSON content
                var jsonContent = File.ReadAllText(jsonFile);
                var config = JsonConvert.DeserializeObject<ICMAlertConfig>(jsonContent);
                _icmAlertConfigs[config.AlertingId] = config;
            }
        }

        public static List<AgentPromptModel> GetAgentPrompts(AgentMode mode)
        {
            var results = new List<AgentPromptModel>();

            // Get all static classes in the specified namespace.
            var types = Assembly.GetExecutingAssembly().GetTypes()
                        .Where(t => t.Namespace == "FirstPartyAgent.AgentPrompts"
                                    && t.IsClass
                                    && t.IsAbstract
                                    && t.IsSealed); // static classes are both abstract and sealed

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<AgentPromptAttribute>();
                // Use equality check instead of HasFlag
                if (attr != null && attr.AgentMode == mode)
                {
                    // Retrieve the public static field "SystemMessage".
                    var field = type.GetField("SystemMessage", BindingFlags.Public | BindingFlags.Static);
                    if (field != null)
                    {
                        var systemMessage = field.GetValue(null) as string;
                        results.Add(new AgentPromptModel(type.Name, attr.Description, systemMessage));
                    }
                }
            }

            return results;
        }
    }

}

