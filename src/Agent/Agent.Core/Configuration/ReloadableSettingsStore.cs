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
    public class ReloadableOptionsChangeTokenSource<T> : IOptionsChangeTokenSource<T>
    {
        private CancellationTokenSource _cts = new();

        public string? Name => Options.DefaultName;

        public IChangeToken GetChangeToken() => new CancellationChangeToken(_cts.Token);

        public void TriggerReload()
        {
            var previous = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            previous.Cancel();
            previous.Dispose();
        }
    }

    public class ReloadableOptionsConfigurator<T> : IConfigureOptions<T> where T : class, new()
    {
        private readonly IReloadableSettingsStore _store;
        private readonly string _pluginName;
        private readonly T? _fallback;

        public ReloadableOptionsConfigurator(IReloadableSettingsStore store, IPluginSettingsTypeRegistry registry, T? fallback = null)
        {
            _store = store;
            _fallback = fallback;

            _pluginName = registry.GetRegisteredPluginNames()
                .FirstOrDefault(name => registry.GetSettingsType(name) == typeof(T))
                ?? typeof(T).Name;
        }

        public void Configure(T options)
        {
            var fromStore = _store.Get<T>(_pluginName) ?? _fallback;
            if (fromStore is null) return;

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

    public interface IReloadableSettingsStore
    {
        object? Get(string pluginName);

        T? Get<T>(string pluginName);

        void Set(string pluginName, object settings);
    }

    public class ReloadableSettingsStore : IReloadableSettingsStore
    {
        private readonly ConcurrentDictionary<string, object> _store = new();

        public object? Get(string pluginName) =>
            _store.TryGetValue(pluginName, out var val) ? val : null;

        public T? Get<T>(string pluginName)
        {
            if (_store.TryGetValue(pluginName, out var value) && value is T typed)
                return typed;
            return default;
        }

        public void Set(string pluginName, object settings)
        {
            _store[pluginName] = settings;
        }
    }
}
