// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models;
using Agent.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services
{
    public interface IPublishedToolsService
    {
        Task<HashSet<string>> GetPublishedToolNamesAsync();
        Task<bool> IsToolPublishedAsync(string toolName);
        Task ReloadConfigurationAsync();
    }

    public class PublishedToolsService : IPublishedToolsService
    {
        private readonly ILogger<PublishedToolsService> _logger;
        private readonly IHostEnvironment _hostEnvironment;
        private PublishedToolsConfiguration? _configuration;
        private HashSet<string> _publishedToolNames;
        private readonly object _lockObject = new object();

        public PublishedToolsService(ILogger<PublishedToolsService> logger, IHostEnvironment hostEnvironment)
        {
            _logger = logger;
            _hostEnvironment = hostEnvironment;
            _publishedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _ = Task.Run(LoadConfigurationAsync);
        }

        public async Task<HashSet<string>> GetPublishedToolNamesAsync()
        {
            await EnsureConfigurationLoadedAsync();
            lock (_lockObject)
            {
                return new HashSet<string>(_publishedToolNames, StringComparer.OrdinalIgnoreCase);
            }
        }

        public async Task<bool> IsToolPublishedAsync(string toolName)
        {
            await EnsureConfigurationLoadedAsync();
            lock (_lockObject)
            {
                return _publishedToolNames.Contains(toolName);
            }
        }

        public async Task ReloadConfigurationAsync()
        {
            await LoadConfigurationAsync();
        }

        private async Task EnsureConfigurationLoadedAsync()
        {
            if (_configuration == null)
            {
                await LoadConfigurationAsync();
            }
        }

        private async Task LoadConfigurationAsync()
        {
            try
            {
                var configPath = GetConfigurationPath();
                _logger.LogInternalInformation("LoadConfigurationAsync: Loading published tools configuration from {ConfigPath}", configPath);

                if (!File.Exists(configPath))
                {
                    _logger.LogInternalWarning("LoadConfigurationAsync: Published tools configuration file not found at {ConfigPath}. Using empty configuration.", configPath);
                    lock (_lockObject)
                    {
                        _configuration = new PublishedToolsConfiguration();
                        _publishedToolNames.Clear();
                    }
                    return;
                }

                var json = await File.ReadAllTextAsync(configPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogInternalWarning("LoadConfigurationAsync: Published tools configuration file is empty. Using empty configuration.");
                    lock (_lockObject)
                    {
                        _configuration = new PublishedToolsConfiguration();
                        _publishedToolNames.Clear();
                    }
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var configuration = JsonSerializer.Deserialize<PublishedToolsConfiguration>(json, options);
                if (configuration == null)
                {
                    _logger.LogInternalError("LoadConfigurationAsync: Failed to deserialize published tools configuration. Using empty configuration.");
                    lock (_lockObject)
                    {
                        _configuration = new PublishedToolsConfiguration();
                        _publishedToolNames.Clear();
                    }
                    return;
                }

                var publishedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var tool in configuration.Tools)
                {
                    var effectiveToolName = tool.GetEffectiveToolName();
                    publishedToolNames.Add(effectiveToolName);
                    _logger.LogInternalInformation("LoadConfigurationAsync: Added published tool: {ToolName} (effective: {EffectiveToolName})", tool.Name, effectiveToolName);
                }

                lock (_lockObject)
                {
                    _configuration = configuration;
                    _publishedToolNames = publishedToolNames;
                }

                _logger.LogInternalInformation("LoadConfigurationAsync: Successfully loaded {ToolCount} published tools", publishedToolNames.Count);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "LoadConfigurationAsync: Error loading published tools configuration. Using empty configuration.");
                lock (_lockObject)
                {
                    _configuration = new PublishedToolsConfiguration();
                    _publishedToolNames.Clear();
                }
            }
        }

        private string GetConfigurationPath()
        {
            // Look for PublishedTools.json in the web application directory
            var webAppPath = Path.Combine(_hostEnvironment.ContentRootPath, "PublishedTools.json");
            if (File.Exists(webAppPath))
            {
                return webAppPath;
            }

            // Fallback to look in the src/Agent/Agent.Web directory
            var currentDir = Directory.GetCurrentDirectory();
            var fallbackPath = Path.Combine(currentDir, "src", "Agent", "Agent.Web", "PublishedTools.json");
            return fallbackPath;
        }
    }
}
