using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Logging;
using FirstPartyAgent.Core.Configuration;
using Agent.Core.Configuration;
using FirstPartyAgent.Core.Models;
using Microsoft.Bot.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Services;
public class AlertHandlerService
{
    private readonly IStorageService _storageService;
    private readonly ICosmosDBService _cosmosDbService;
    private readonly AzureAlertingClient _azureAlertingClient;

    private string icmAlertConfigsContainerName = "icmalertconfigs";
    private readonly Dictionary<string, ICMAlertConfig> _icmAlertConfigs
        = new Dictionary<string, ICMAlertConfig>(StringComparer.OrdinalIgnoreCase);

    private readonly string icmAgentConfigCosmosDbContainer = "IcmAlertConfigs";
    private const string icmAgentAlertDetailsCosmosDbContainer = "IcmAlertDetails";
    private readonly ILogger<AlertHandlerService> _logger;
    private Task? _initializationTask;


    public AlertHandlerService(StorageAccountSettings storageAccountSettings, IHostEnvironment hostEnvironment, IStorageService storageService, ICosmosDBService cosmosDBService, AzureAlertingClient azureAlertingClient, ILogger<AlertHandlerService> logger)
    {
        _storageService = storageService;
        _cosmosDbService = cosmosDBService;
        _azureAlertingClient = azureAlertingClient;
        if (!string.IsNullOrWhiteSpace(storageAccountSettings?.IcmAlertConfigsContainerName))
        {
            icmAlertConfigsContainerName = storageAccountSettings.IcmAlertConfigsContainerName;
        }

        _logger = logger;

        _initializationTask = InitializeAsync(hostEnvironment);
    }

    private async Task InitializeAsync(IHostEnvironment hostEnvironment)
    {
        if (!hostEnvironment.IsDevelopment() && _cosmosDbService != null && _cosmosDbService.IsEnabled)
        {
            await LoadICMAlertConfigsFromCosmosDbAsync();
        }
        else if (!hostEnvironment.IsDevelopment() && _storageService != null && _storageService.IsEnabled)
        {
            await LoadICMAlertConfigsFromStorageAsync();
        }
        else
        {
            LoadICMAlertConfigsFromLocal();
        }
    }

    public async Task<Dictionary<string, ICMAlertConfig>> GetICMAlertConfigsAsync(bool reload = false)
    {
        await IsReady();
        if (reload)
        {
            await ReloadAsync();
        }

        return _icmAlertConfigs;
    }

    public async Task<AlertDetailsBase?> GetAzureAlertingDetailsById(
            string azureAlertingId)
    {
        _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details. azureAlertingId: {azureAlertingId}");
        await IsReady();

        // First check in local folder
        if (!_storageService.IsEnabled && !_cosmosDbService.IsEnabled)
        {
            try
            {
                _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details from local folder. azureAlertingId: {azureAlertingId}");
                //Read from local folder called AlertDetails
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AlertDetails", $"{azureAlertingId}.json");
                if (File.Exists(filePath))
                {
                    var fileContent = File.ReadAllText(filePath);
                    var alertDetails = JsonConvert.DeserializeObject<AlertDetailsBase>(fileContent);
                    return alertDetails;
                }
                else
                {
                    _logger.LogError($"Alert details file not found in local folder: {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading alert details from local folder: {ex.Message}");
            }
        }


        // If not found in local folder, check in storage
        if (_storageService.IsEnabled)
        {
            try
            {
                _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details from Storage. azureAlertingId: {azureAlertingId}");
                var fileContent = await _storageService.ReadFileFromStorage("alertdetails", $"{azureAlertingId}.json");
                var alertDetails = JsonConvert.DeserializeObject<AlertDetailsBase>(fileContent);
                return alertDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading alert details from storage: {ex.Message} for azureAlertingId: {azureAlertingId}. Will attempt to read from Azure Alerting.");
            }
        }

        if (_cosmosDbService.IsEnabled)
        {
            _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details from CosmosDB. azureAlertingId: {azureAlertingId}");
            try
            {
                var alertDetails = _cosmosDbService.GetQueryableContainer<AlertDetailsBase>(_cosmosDbService.IcmAgentDatabaseName, icmAgentAlertDetailsCosmosDbContainer)
                    .Where(a => a.Id.ToString() == azureAlertingId)
                    .ToList();
                if (alertDetails != null && alertDetails.Count > 0)
                {
                    return alertDetails.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading alert details from CosmosDB: {ex.Message}");
            }
        }

        //Finally check in Azure Alerting
        if (_azureAlertingClient.IsEnabled())
        {
            try
            {
                _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details from Azure Alerting. azureAlertingId: {azureAlertingId}");
                var alertDetails = await _azureAlertingClient.GetAlertDetails(azureAlertingId);
                return alertDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching alert details from Azure Alerting: {ex.Message}");
            }
        }
        return null;
    }

    public async Task<ICMAlertConfig?> GetICMAlertConfigAsync(string alertingId)
    {
        await IsReady();
        ICMAlertConfig? config;
        if (_cosmosDbService != null && _cosmosDbService.IsEnabled)
        {
            var queryable = _cosmosDbService.GetQueryableContainer<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, icmAgentConfigCosmosDbContainer);

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

    public async Task<string> SaveICMAlertConfig(string alertingId, string customConfig)
    {
        await IsReady();
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
        if (config == null)
        {
            throw new ArgumentException("Invalid custom configuration provided.");
        }
        _icmAlertConfigs[alertingId] = config;
        return string.Empty;
    }

    public async Task ReloadAsync()
    {
        await IsReady();
        if (_cosmosDbService != null && _cosmosDbService.IsEnabled)
        {
            await LoadICMAlertConfigsFromCosmosDbAsync();
        }
        else if (_storageService != null && _storageService.IsEnabled)
        {
            await LoadICMAlertConfigsFromStorageAsync();
        }
        else
        {
            LoadICMAlertConfigsFromLocal();
        }

    }

    private async Task LoadICMAlertConfigsFromCosmosDbAsync()
    {
        if (_cosmosDbService != null && _cosmosDbService.IsEnabled)
        {
            var configs = await _cosmosDbService.GetQueryableContainer<ICMAlertConfig>(_cosmosDbService.IcmAgentDatabaseName, icmAgentConfigCosmosDbContainer).ToListAsync();
            foreach (var config in configs)
            {
                _icmAlertConfigs[config.AlertingId] = config;
            }
        }
    }

    private async Task LoadICMAlertConfigsFromStorageAsync()
    {
        var blobNames = await _storageService.ListFilesInContainer(icmAlertConfigsContainerName);
        foreach (var blobName in blobNames)
        {
            var jsonContent = _storageService.ReadFileFromStorage(icmAlertConfigsContainerName, blobName).Result;
            var config = JsonConvert.DeserializeObject<ICMAlertConfig>(jsonContent);
            if (config == null)
            {
                _logger.LogWarning($"Failed to deserialize ICMAlertConfig from blob: {blobName}");
                continue;
            }
            _icmAlertConfigs[config.AlertingId] = config;
        }
    }

    private void LoadICMAlertConfigsFromLocal()
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
            if (config == null)
            {
                _logger.LogWarning($"Failed to deserialize ICMAlertConfig from file: {jsonFile}");
                continue;
            }
            _icmAlertConfigs[config.AlertingId] = config;
        }
    }
    private async Task IsReady()
    {

        if (_initializationTask != null && !_initializationTask.IsCompleted)
        {
            await _initializationTask;
            _initializationTask = null;
        }
    }

}
