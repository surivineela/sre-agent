// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130 // Extension methods should be in the same namespace as the containing type
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130 // Extension methods should be in the same namespace as the containing type

public static class DataConnectorRegistrationExtensions
{
    /// <summary>
    /// Registers data connectors based on configuration settings and their implementation types.
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IHostApplicationBuilder RegisterDataConnectors(this IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Services.AddSingleton<DataConnectorIndex>();

        hostBuilder.Services.Configure<DataConnectorSettings>(
            hostBuilder.Configuration.GetSection("AppSettings:Core:DataConnectorSettings"));

        hostBuilder.Services.Configure<List<DataConnectorInstanceSettings>>(
            hostBuilder.Configuration.GetSection("AppSettings:Core:DataConnectors"));

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        // Find all types with DataConnector attribute that implement BackgroundService
        IEnumerable<Type> dataConnectorTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && assembly.GetName()?.Name?.StartsWith("Agent.") == true)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetCustomAttribute<DataConnectorAttribute>() != null);

        foreach (Type connectorType in dataConnectorTypes)
        {
            if (!typeof(IDataConnector).IsAssignableFrom(connectorType))
            {
                throw new InvalidOperationException(
                    $"Data connector type '{connectorType.FullName}' does not implement IDataConnector interface.");
            }

            hostBuilder.Services.AddKeyedTransient(typeof(IDataConnector), connectorType, connectorType);

            Type storageType = typeof(DataConnectorStorage<>).MakeGenericType(connectorType);
            hostBuilder.Services.AddSingleton(storageType);
        }

        hostBuilder.Services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DataConnectorService>>();
            IOptions<DataConnectorSettings> dataConnectorSettings = sp.GetRequiredService<IOptions<DataConnectorSettings>>();
            IOptions<List<DataConnectorInstanceSettings>> dataConnectorInstanceSettings = sp.GetRequiredService<IOptions<List<DataConnectorInstanceSettings>>>();

            List<DataConnectorInstance> dataConnectorInstances = new List<DataConnectorInstance>(dataConnectorInstanceSettings.Value.Count);

            foreach (DataConnectorInstanceSettings dataConnectorInstanceSetting in dataConnectorInstanceSettings.Value)
            {
                // If ExtendedPropertiesJson is not provided, fix ExtendedProperties by re-parsing from configuration
                // to handle arrays and nested objects
                if (string.IsNullOrWhiteSpace(dataConnectorInstanceSetting.ExtendedPropertiesJson))
                {
                    var configSection = hostBuilder.Configuration
                        .GetSection($"AppSettings:Core:DataConnectors")
                        .GetChildren()
                        .FirstOrDefault(c => c["Name"] == dataConnectorInstanceSetting.Name);

                    var extPropsSection = configSection?.GetSection("ExtendedProperties");
                    if (extPropsSection?.Exists() == true)
                    {
                        var tempDict = new Dictionary<string, object?>();
                        foreach (var child in extPropsSection.GetChildren())
                        {
                            tempDict[child.Key] = GetConfigurationValue(child);
                        }

                        var json = JsonSerializer.Serialize(tempDict);
                        dataConnectorInstanceSetting.ExtendedProperties =
                            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    }
                }

                Type? connectorType = dataConnectorTypes.FirstOrDefault(t => t.GetCustomAttribute<DataConnectorAttribute>()?.Type.Equals(dataConnectorInstanceSetting.DataConnectorType, StringComparison.OrdinalIgnoreCase) == true);

                if (connectorType == null)
                {
                    logger.LogInternalWarning(
                        "No data connector service implementation found for '{DataConnectorType}'. Available data connector types are: {AvailableDataConnectors}.",
                        dataConnectorInstanceSetting.DataConnectorType,
                        string.Join(", ", dataConnectorTypes.Select(type => $"{type.GetCustomAttribute<DataConnectorAttribute>()?.Type} ({type.Name})")));

                    continue;
                }

                IDataConnector dataConnectorInstance = sp.GetRequiredKeyedService<IDataConnector>(connectorType);

                dataConnectorInstances.Add(new DataConnectorInstance(
                    DataConnector: dataConnectorInstance,
                    InstanceSettings: dataConnectorInstanceSetting));
            }

            return new DataConnectorService(
                dataConnectorInstances,
                sp.GetRequiredService<DataConnectorIndex>(),
                logger);
        });

        return hostBuilder;
    }

    /// <summary>
    /// Recursively extracts configuration values, handling simple values, arrays, and nested dictionaries.
    /// </summary>
    private static object? GetConfigurationValue(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();

        if (!children.Any())
        {
            // Simple value
            return section.Value;
        }

        if (children.All(c => int.TryParse(c.Key, out _)))
        {
            // It's an array
            return children.OrderBy(c => int.Parse(c.Key)).Select(c => GetConfigurationValue(c)).ToList();
        }
        else
        {
            // It's a nested dictionary
            var dict = new Dictionary<string, object?>();
            foreach (var child in children)
            {
                dict[child.Key] = GetConfigurationValue(child);
            }
            return dict;
        }
    }
}
