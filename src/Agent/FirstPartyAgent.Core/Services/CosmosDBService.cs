using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using FirstPartyAgent.Core.Services;
using Gremlin.Net.Process.Traversal;
using Microsoft.Azure.Cosmos.Linq;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;


namespace FirstPartyAgent.Core.Services
{
    /// <summary>
    /// Implementation of the CosmosDB service
    /// </summary>
    /// 
    public interface ICosmosDBService
    {
        IOrderedQueryable<T> GetQueryableContainer<T>(string databaseName, string containerName);

        Task BulkWriteAsync<T>(string databaseName, string containerName, IEnumerable<T> items, PartitionKey partitionKey);

        bool IsEnabled { get; }
        string IcmConfigsDatabaseName { get; }
    }

    public class CosmosDBServiceDisabled : ICosmosDBService
    {
        public IOrderedQueryable<T> GetQueryableContainer<T>(string databaseName, string containerName)
        {
            return new List<T>().AsQueryable().OrderBy(x => 0);
        }

        public Task BulkWriteAsync<T>(string databaseName, string containerName, IEnumerable<T> items, PartitionKey partitionKey)
        {
            return Task.CompletedTask;
        }

        public string IcmConfigsDatabaseName => "IcmConfigs";

        public bool IsEnabled => false;
    }

    public class CosmosDBService : ICosmosDBService
    {
        private readonly ILogger<CosmosDBService> _logger;
        private readonly CosmosClient _cosmosClient;
        private string _accountUrl;
        private string _managedIdentityClient;
        public string _icmConfigsDatabaseName;
        private bool Enabled = true;

        public bool IsEnabled => Enabled;

        public string IcmConfigsDatabaseName => _icmConfigsDatabaseName;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDBService"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration</param>
        /// <param name="logger">The logger</param>
        public CosmosDBService(IHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<CosmosDBService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var config = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _accountUrl = configuration.GetValue<string>("AppSettings:Core:External:CosmosDB:AccountUrl", string.Empty);
            _managedIdentityClient = configuration.GetValue<string>("AppSettings:Core:External:CosmosDB:MsiClientId", string.Empty); //"051f9d76-ce6d-4428-b55c-048b6ded238a"; //configuration.GetValue<string>("CosmosDb:ManagedIdentityClient", string.Empty);
            _icmConfigsDatabaseName = configuration.GetValue<string>("AppSettings:Core:External:CosmosDB:IcmConfigsDatabaseName", string.Empty); // "IcmConfigs"; //configuration.GetValue<string>("CosmosDb:IcmConfigsDatabaseName", string.Empty);

            if (!hostEnvironment.IsDevelopment() && string.IsNullOrWhiteSpace(_accountUrl))
            {
                throw new InvalidOperationException("CosmosDb:AccountUrl is not configured");
            }


            if (!hostEnvironment.IsDevelopment() && string.IsNullOrWhiteSpace(_managedIdentityClient))
            {
                throw new InvalidOperationException("CosmosDb:ManagedIdentityClient is not configured");
            }

            _logger.LogInformation("Initializing CosmosDB service with endpoint {Endpoint}", _accountUrl);

            if (string.IsNullOrWhiteSpace(_accountUrl))
            {
                Enabled = false;
                return;
            }
            
            // Create the CosmosClient based on environment
            _cosmosClient = CreateCosmosClient(_accountUrl);
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
                return new CosmosClient(endpoint, new DefaultAzureCredential(), options);
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
            
            var container = _cosmosClient.GetContainer(databaseName, containerName);

            return container.GetItemLinqQueryable<T>(true);
        }

        public async Task BulkWriteAsync<T>(string databaseName, string containerName, IEnumerable<T> items, PartitionKey partitionKey)
        {
            var container = _cosmosClient.GetContainer(databaseName, containerName);

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
