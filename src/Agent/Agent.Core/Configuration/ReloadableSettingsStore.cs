// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Reflection;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace Agent.Core.Configuration
{
    /// <summary>
    /// Provides a change token source for reloadable options, enabling runtime configuration updates.
    /// Implements both IOptionsChangeTokenSource and IReloadableTokenSource for flexibility.
    /// </summary>
    public class ReloadableOptionsChangeTokenSource<T> : IOptionsChangeTokenSource<T>, IReloadableTokenSource where T : class, new()
    {
        // Manages cancellation for configuration reload signals
        private CancellationTokenSource _cts = new();

        /// <summary>
        /// Gets the name of the options instance, using the default Options name
        /// </summary>
        public string? Name => Options.DefaultName;

        /// <summary>
        /// Returns a change token that can be used to detect when the options have been reloaded
        /// </summary>
        public IChangeToken GetChangeToken() => new CancellationChangeToken(_cts.Token);

        /// <summary>
        /// Triggers a configuration reload by replacing and canceling the current CancellationTokenSource
        /// </summary>
        public void TriggerReload()
        {
            var previous = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            previous.Cancel();
            previous.Dispose();
        }
    }

    /// <summary>
    /// Configures options by loading values from a reloadable settings store.
    /// Supports plugin-specific settings with fallback values.
    /// </summary>
    public class ReloadableOptionsConfigurator<T> : IConfigureOptions<T> where T : class, new()
    {
        private readonly IReloadableSettingsStore _store;
        private readonly string _pluginName;
        private readonly T? _fallback;

        /// <summary>
        /// Initializes a new configurator with the specified store, registry, and optional fallback values
        /// </summary>
        /// <param name="store">The settings store to load values from</param>
        /// <param name="registry">Registry to look up plugin settings types</param>
        /// <param name="fallback">Optional fallback settings if none are found in the store</param>
        public ReloadableOptionsConfigurator(IReloadableSettingsStore store, IPluginSettingsTypeRegistry registry, T? fallback = null)
        {
            _store = store;
            _fallback = fallback;

            // Determine plugin name by looking up the settings type in the registry,
            // falling back to the type name if not registered
            _pluginName = registry.GetRegisteredPluginNames()
                .FirstOrDefault(name => registry.GetSettingsType(name) == typeof(T))
                ?? typeof(T).Name;
        }

        /// <summary>
        /// Configures the options instance by copying properties from stored or fallback settings
        /// </summary>
        public void Configure(T options)
        {
            var fromStore = _store.Get<T>(_pluginName) ?? _fallback;
            if (fromStore is null) return;

            // Copy all writable public properties from the stored/fallback settings
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanWrite)
                {
                    var value = prop.GetValue(fromStore);
                    prop.SetValue(options, value);
                }
            }
        }
    }

    /// <summary>
    /// Defines the contract for a store that can hold and retrieve plugin-specific settings
    /// </summary>
    public interface IReloadableSettingsStore
    {
        /// <summary>
        /// Gets settings for a plugin as an untyped object
        /// </summary>
        object? Get(string pluginName);

        /// <summary>
        /// Gets strongly-typed settings for a plugin
        /// </summary>
        T? Get<T>(string pluginName);

        /// <summary>
        /// Stores settings for a plugin
        /// </summary>
        void Set(string pluginName, object settings);
    }

    /// <summary>
    /// Thread-safe implementation of IReloadableSettingsStore using a concurrent dictionary
    /// </summary>
    public class ReloadableSettingsStore : IReloadableSettingsStore
    {
        // Thread-safe storage for plugin settings
        private readonly ConcurrentDictionary<string, object> _store = new();

        /// <summary>
        /// Retrieves untyped settings for the specified plugin
        /// </summary>
        public object? Get(string pluginName) =>
            _store.TryGetValue(pluginName, out var val) ? val : null;

        /// <summary>
        /// Retrieves strongly-typed settings for the specified plugin
        /// </summary>
        public T? Get<T>(string pluginName)
        {
            if (_store.TryGetValue(pluginName, out var value) && value is T typed)
                return typed;
            return default;
        }

        /// <summary>
        /// Stores settings for the specified plugin, overwriting any existing values
        /// </summary>
        public void Set(string pluginName, object settings)
        {
            _store[pluginName] = settings;
        }
    }
}
