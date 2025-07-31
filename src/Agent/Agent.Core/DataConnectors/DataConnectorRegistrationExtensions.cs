// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Reflection;
using Agent.Core.Clients.Storage;
using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
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
            IOptions<DataConnectorSettings> dataConnectorSettings = sp.GetRequiredService<IOptions<DataConnectorSettings>>();
            IOptions<List<DataConnectorInstanceSettings>> dataConnectorInstanceSettings = sp.GetRequiredService<IOptions<List<DataConnectorInstanceSettings>>>();

            List<DataConnectorInstance> dataConnectorInstances = new List<DataConnectorInstance>(dataConnectorInstanceSettings.Value.Count);

            foreach (DataConnectorInstanceSettings dataConnectorInstanceSetting in dataConnectorInstanceSettings.Value)
            {
                Type? connectorType = dataConnectorTypes.FirstOrDefault(t => t.GetCustomAttribute<DataConnectorAttribute>()?.Type.Equals(dataConnectorInstanceSetting.DataConnectorType, StringComparison.OrdinalIgnoreCase) == true);

                if (connectorType == null)
                {
                    throw new InvalidOperationException(
                        $"No data connector type found for '{dataConnectorInstanceSetting.DataConnectorType}'. Available data connector types are: {string.Join(", ", dataConnectorTypes.Select(type => $"{type.GetCustomAttribute<DataConnectorAttribute>()?.Type} ({type.Name})"))}.");
                }

                IDataConnector dataConnecterInstance = sp.GetRequiredKeyedService<IDataConnector>(connectorType);

                dataConnectorInstances.Add(new DataConnectorInstance(
                    DataConnector: dataConnecterInstance,
                    InstanceSettings: dataConnectorInstanceSetting));
            }

            return new DataConnectorService(
                dataConnectorInstances,
                sp.GetRequiredService<DataConnectorIndex>(),
                sp.GetRequiredService<ILogger<DataConnectorService>>());
            });

        return hostBuilder;
    }
}
