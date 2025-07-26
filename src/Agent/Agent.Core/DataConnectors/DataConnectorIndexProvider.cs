// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Collections.Concurrent;
using Agent.Core.Clients.Search;
using Agent.Core.Configuration;
using Agent.Core.Clients.Storage;
using Microsoft.Extensions.Options;

namespace Agent.Core.DataConnectors;

public class DataConnectorIndexProvider
{
    private readonly DataConnectorSettings _dataConnectorSettings;
    private readonly ISearchIndexingClient _searchIndexingClient;
    private readonly IndexingSettings _indexingSettings;
    private readonly OpenAISettings _openAiSettings;
    private readonly IAzureBlobStorageClient _azureBlobStorageClient;

    private readonly ConcurrentDictionary<string, DataConnectorIndex> _indexCache = new();

    public DataConnectorIndexProvider(
        ISearchIndexingClient searchIndexingClient,
        IAzureBlobStorageClient azureBlobStorageClient,
        IndexingSettings indexingSettings,
        OpenAISettings openAiSettings,
        IOptions<DataConnectorSettings> dataConnectorSettings)
    {
        _searchIndexingClient = searchIndexingClient ?? throw new ArgumentNullException(nameof(searchIndexingClient));
        _azureBlobStorageClient = azureBlobStorageClient ?? throw new ArgumentNullException(nameof(azureBlobStorageClient));
        _indexingSettings = indexingSettings ?? throw new ArgumentNullException(nameof(indexingSettings));
        _openAiSettings = openAiSettings ?? throw new ArgumentNullException(nameof(openAiSettings));
        _dataConnectorSettings = dataConnectorSettings.Value ?? throw new ArgumentNullException(nameof(dataConnectorSettings));
    }

    public DataConnectorIndex GetDataConnectorIndex<TDataConnector>() where TDataConnector : IDataConnector
    {
        string dataConnectorType = typeof(TDataConnector).GetCustomAttribute<DataConnectorAttribute>()?.Type
            ?? throw new InvalidOperationException($"Data connector type {typeof(TDataConnector).FullName} does not have a DataConnector attribute.");

        return GetDataConnectorIndex(dataConnectorType);
    }

    public DataConnectorIndex GetDataConnectorIndex(string dataConnectorType)
    {
        if (!_dataConnectorSettings.Types.TryGetValue(dataConnectorType, out DataConnectorTypeSettings? typeSettings))
        {
            throw new InvalidOperationException($"No settings found for data connector type {dataConnectorType}.");
        }

        if (typeSettings.Search == null || typeSettings.Storage == null)
        {
            throw new InvalidOperationException($"Search or storage settings are missing for data connector type {dataConnectorType}.");
        }

        return _indexCache.GetOrAdd(dataConnectorType, _ =>
            new DataConnectorIndex(
                _searchIndexingClient,
                _azureBlobStorageClient,
                _indexingSettings,
                _openAiSettings,
                typeSettings.Search,
                typeSettings.Storage));
    }
}
