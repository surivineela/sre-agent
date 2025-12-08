// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services
{
    public interface IPublishedToolsService
    {
        /// <summary>
        /// Gets all published tools with their configurations including custom descriptions.
        /// </summary>
        Task<IReadOnlyList<PublishedTool>> GetPublishedToolsAsync();

        /// <summary>
        /// Gets the names of all published tools as a HashSet for efficient lookup.
        /// </summary>
        Task<HashSet<string>> GetPublishedToolNamesAsync();

        /// <summary>
        /// Checks if a tool is in the published tools list.
        /// </summary>
        Task<bool> IsToolPublishedAsync(string toolName);

        /// <summary>
        /// Reloads the configuration from disk.
        /// </summary>
        Task ReloadConfigurationAsync();
    }

    public class PublishedToolsService : IPublishedToolsService
    {
        private readonly ILogger<PublishedToolsService> _logger;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly bool _isFirstPartyTenant;
        private PublishedToolsConfiguration? _configuration;
        private List<PublishedTool> _publishedTools;
        private readonly object _lockObject = new object();

        public PublishedToolsService(
            ILogger<PublishedToolsService> logger,
            IHostEnvironment hostEnvironment,
            IFirstPartyTenantProvider firstPartyTenantProvider)
        {
            _logger = logger;
            _hostEnvironment = hostEnvironment;
            _isFirstPartyTenant = firstPartyTenantProvider.IsFirstPartyTenant();
            _publishedTools = new List<PublishedTool>();
            _ = Task.Run(LoadConfigurationAsync);
        }

        public async Task<IReadOnlyList<PublishedTool>> GetPublishedToolsAsync()
        {
            await EnsureConfigurationLoadedAsync();
            lock (_lockObject)
            {
                return _publishedTools.ToList().AsReadOnly();
            }
        }

        public async Task<HashSet<string>> GetPublishedToolNamesAsync()
        {
            await EnsureConfigurationLoadedAsync();
            lock (_lockObject)
            {
                return new HashSet<string>(
                    _publishedTools.Select(t => t.GetEffectiveToolName()),
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        public async Task<bool> IsToolPublishedAsync(string toolName)
        {
            await EnsureConfigurationLoadedAsync();
            lock (_lockObject)
            {
                return _publishedTools.Any(t =>
                    string.Equals(t.GetEffectiveToolName(), toolName, StringComparison.OrdinalIgnoreCase));
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
                        _publishedTools.Clear();
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
                        _publishedTools.Clear();
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
                        _publishedTools.Clear();
                    }
                    return;
                }

                var publishedTools = new List<PublishedTool>();
                foreach (var tool in configuration.Tools)
                {
                    if (tool.FirstPartyOnly && !_isFirstPartyTenant)
                    {
                        _logger.LogInternalInformation(
                            "LoadConfigurationAsync: Skipping first-party-only tool {ToolName} for non-first-party tenant",
                            tool.Name);
                        continue;
                    }

                    publishedTools.Add(tool);
                    _logger.LogInternalInformation("LoadConfigurationAsync: Added published tool: {ToolName} (effective: {EffectiveToolName})",
                        tool.Name, tool.GetEffectiveToolName());
                }

                lock (_lockObject)
                {
                    _configuration = configuration;
                    _publishedTools = publishedTools;
                }

                _logger.LogInternalInformation("LoadConfigurationAsync: Successfully loaded {ToolCount} published tools",
                    publishedTools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "LoadConfigurationAsync: Error loading published tools configuration. Using empty configuration.");
                lock (_lockObject)
                {
                    _configuration = new PublishedToolsConfiguration();
                    _publishedTools.Clear();
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
