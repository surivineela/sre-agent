// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;

namespace Agent.Core.Configuration
{
    public interface IPluginSettingsTypeRegistry
    {
        void Register<TSettings>(string pluginName) where TSettings : class, new();

        Type? GetSettingsType(string pluginName);

        bool TryGetSettingsType(string pluginName, out Type type);

        IEnumerable<string> GetRegisteredPluginNames();
    }

    public class PluginSettingsTypeRegistry : IPluginSettingsTypeRegistry
    {
        private readonly ConcurrentDictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase);

        public void Register<TSettings>(string pluginName) where TSettings : class, new()
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Plugin name cannot be null or empty", nameof(pluginName));

            var type = typeof(TSettings);

            if (!_registry.TryAdd(pluginName, type))
            {
                throw new InvalidOperationException($"Plugin '{pluginName}' is already registered with settings type '{_registry[pluginName].FullName}'");
            }
        }

        public Type? GetSettingsType(string pluginName)
        {
            return _registry.TryGetValue(pluginName, out var type) ? type : null;
        }

        public bool TryGetSettingsType(string pluginName, out Type type)
        {
            return _registry.TryGetValue(pluginName, out type!);
        }

        public IEnumerable<string> GetRegisteredPluginNames() => _registry.Keys;
    }
}
