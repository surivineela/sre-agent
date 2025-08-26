using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Hosting;
using Agent.Core.Configuration;


namespace FirstPartyAgent.Core.Services
{
    /// <summary>
    /// Implementation of the CosmosDB service
    /// </summary>
    /// 
    public interface ICosmosDBService
    {
        CosmosClient? CosmosClient { get; }
        IOrderedQueryable<T> GetQueryableContainer<T>(string databaseName, string containerName);

        Task BulkWriteAsync<T>(string databaseName, string containerName, IEnumerable<T> items, PartitionKey partitionKey);

        Task<ItemResponse<T>?> UpsertItemAsync<T>(string databaseName, string containerName, T item);

        bool IsEnabled { get; }

        string IcmAgentDatabaseName { get; }
    }

    public class CosmosDBServiceDisabled : ICosmosDBService
    {
        public CosmosClient? CosmosClient => null;
        public IOrderedQueryable<T> GetQueryableContainer<T>(string databaseName, string containerName)
        {
            return new List<T>().AsQueryable().OrderBy(x => 0);
        }

        public Task BulkWriteAsync<T>(string databaseName, string containerName, IEnumerable<T> items, PartitionKey partitionKey)
        {
            return Task.CompletedTask;
        }

        public Task<ItemResponse<T>?> UpsertItemAsync<T>(string databaseName, string containerName, T item)
        {
            return Task.FromResult<ItemResponse<T>?>(default);
        }

        public string IcmAgentDatabaseName => "HotsiteAgent";

        public bool IsEnabled => false;
    }

    public class CosmosDBService : ICosmosDBService
    {
        private readonly ILogger<CosmosDBService> _logger;
        private readonly CosmosClient? _cosmosClient;
        private FederationSettings _federationSettings;
        private string _managedIdentityClient;
        public string _icmAgentDatabaseName;
        private bool Enabled = true;

        public CosmosClient? CosmosClient => _cosmosClient;
        public bool IsEnabled => Enabled;

        public string IcmAgentDatabaseName => _icmAgentDatabaseName;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDBService"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration</param>
        /// <param name="logger">The logger</param>
        public CosmosDBService(
            ILogger<CosmosDBService> logger,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            IOptions<AzureSettings> azureSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var config = configuration ?? throw new ArgumentNullException(nameof(configuration));

            var cosmosDbSettings = azureSettings.Value.CosmosDB;

            string? cosmosAccountName = null;
            string? domainSuffix = null;
            string? endpoint = null;
            string? cosmosDatabaseName = null;

            if (!string.IsNullOrWhiteSpace(cosmosDbSettings?.Docs?.AccountName))
            {
                cosmosAccountName = cosmosDbSettings.Docs.AccountName;
                domainSuffix = cosmosDbSettings.Docs.DomainSuffix;
                endpoint = $"https://{cosmosAccountName}.{domainSuffix}";
                cosmosDatabaseName = cosmosDbSettings.Docs.Database;
            }
            else
            {
                // Fallback to configuration values if CosmosDB settings are not provided
                endpoint = configuration.GetValue<string>("AppSettings:Core:External:CosmosDB:AccountUrl", string.Empty);
                cosmosDatabaseName = string.Empty;
            }

            // use cosmosDatabaseName if not set in configuration
            _icmAgentDatabaseName = configuration.GetValue<string>("AppSettings:Core:External:CosmosDB:IcmAgentDatabaseName", cosmosDatabaseName) ?? string.Empty;

            if (!hostEnvironment.IsDevelopment() && string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("Cosmos DB endpoint is not configured");
            }

            _federationSettings = azureSettings.Value.Federation;
            _managedIdentityClient = configuration.GetValue<string>("AppSettings:Core:External:CosmosDB:MsiClientId", string.Empty);

            _logger.LogInformation("Initializing CosmosDB service with endpoint {Endpoint}", endpoint);

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Enabled = false;
                return;
            }

            // Create the CosmosClient based on environment
            _cosmosClient = CreateCosmosClient(endpoint);
        }

        /// <summary>
        /// Creates a CosmosClient with appropriate credentials
        /// </summary>
        private CosmosClient CreateCosmosClient(string endpoint)
        {
            CosmosClientOptions options = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.Default,
                }
            };

            if (Debugger.IsAttached)
            {
                _logger.LogInformation("Using DefaultAzureCredential for CosmosDB authentication (Debug Mode)");
                return new CosmosClient(endpoint, new DefaultAzureCredential(), options); // CodeQL [SM05137] This is non-production code which is deprecated and not deployed.
            }
            else if (!string.IsNullOrWhiteSpace(_federationSettings?.ClientId))
            {
                var credOptions = new WorkloadIdentityCredentialOptions()
                {
                    ClientId = _federationSettings.ClientId,
                    TenantId = _federationSettings.TenantId,
                    AuthorityHost = new Uri(_federationSettings.AuthorityHost),
                };

                var cred = new WorkloadIdentityCredential(credOptions);
                _logger.LogInformation("Using WorkloadIdentityCredential for CosmosDB authentication (Production Mode)");
                return new CosmosClient(endpoint, cred, options);
            }
            else
            {
                _logger.LogInformation("Using ManagedIdentityCredential for CosmosDB authentication (Production Mode)");

                if (string.IsNullOrWhiteSpace(_managedIdentityClient))
                {
                    throw new InvalidOperationException("CosmosDb:ManagedIdentityClient is not configured");
                }

                return new CosmosClient(endpoint, new ManagedIdentityCredential(_managedIdentityClient), options);
            }
        }

        public IOrderedQueryable<T> GetQueryableContainer<T>(string databaseName, string containerName)
        {
            _logger.LogInformation("Getting or creating container {ContainerName} in database {DatabaseName}",
                containerName, databaseName);

            var container = _cosmosClient != null ? _cosmosClient.GetContainer(databaseName, containerName) : throw new InvalidOperationException("CosmosClient not initialized");

            return container.GetItemLinqQueryable<T>(true);
        }

        public async Task BulkWriteAsync<T>(string databaseName, string containerName, IEnumerable<T> items, PartitionKey partitionKey)
        {
            var container = _cosmosClient != null ? _cosmosClient.GetContainer(databaseName, containerName) : throw new InvalidOperationException("CosmosClient not initialized");

            var batch = container.CreateTransactionalBatch(partitionKey);


            foreach (var item in items)
            {
                batch.UpsertItem(item);
            }

            using TransactionalBatchResponse response = await batch.ExecuteAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Bulk write succeeded. Status Code: {response.StatusCode}");
            }
            else
            {
                Console.WriteLine($"Bulk write failed. Status Code: {response.StatusCode}");
            }
        }

        public async Task<ItemResponse<T>?> UpsertItemAsync<T>(string databaseName, string containerName, T item)
        {
            var container = _cosmosClient != null ? _cosmosClient.GetContainer(databaseName, containerName) : throw new InvalidOperationException("CosmosClient not initialized");
            return await container.UpsertItemAsync(item);
        }
    }

    public static class CosmosExtensions
    {
        public async static Task<List<T>> ToListAsync<T>(this IQueryable<T> queryable)
        {
            var iterator = queryable.ToFeedIterator();
            var results = new List<T>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            return results;
        }
    }
}
