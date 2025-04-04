// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Models;
using System.Text.Json;

namespace FirstPartyAgent.Core.Helpers
{
    public static class PluginConfigLoader
    {
        private static readonly string ConfigFilePath = "Plugins/PluginConfig.json";
        public static List<PluginDetails> Plugins { get; private set; } = new List<PluginDetails>();
        private static bool pluginsLoaded = false;

        static PluginConfigLoader()
        {
            if (pluginsLoaded)
                return;
            LoadPlugins();
        }

        private static void LoadPlugins()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFilePath);
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    Plugins = JsonSerializer.Deserialize<List<PluginDetails>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<PluginDetails>();
                    pluginsLoaded = true;
                }
                else
                {
                    Console.WriteLine($"Config file '{filePath}' not found.");
                    throw new Exception($"Config file '{filePath}' not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading plugin configuration: {ex.Message}");
                throw new Exception($"Error loading plugin configuration: {ex.Message}");
            }
        }
    }

}

