using Azure;
using Agent.Core.Configuration;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace FirstPartyAgent.Core.Services
{
    public interface IAzureSearchClient
    {
        Task<SearchResults<SearchDocument>> SearchAsync<SearchDocument>(string searchText, SearchOptions options = null, CancellationToken cancellationToken = default);
    }

    public class AzureSearchClient: IAzureSearchClient
    {   
        private readonly AzureSearchSettings _azureSearchSettings;
        private SearchClient Client { get; }

        public AzureSearchClient(AzureSearchSettings azureSearchSettings) 
        {
            _azureSearchSettings = azureSearchSettings;
            if (!string.IsNullOrWhiteSpace(_azureSearchSettings.SearchApiKeyOverride))
            {
                var credential = new AzureKeyCredential(_azureSearchSettings.SearchApiKeyOverride);
                Client = new SearchClient(new Uri(_azureSearchSettings.SearchServiceUri), _azureSearchSettings.IndexName, credential);
            }
            else if (!string.IsNullOrWhiteSpace(_azureSearchSettings.UserAssignedMIClientId))
            {
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = _azureSearchSettings.UserAssignedMIClientId
                });
                Client = new SearchClient(new Uri(_azureSearchSettings.SearchServiceUri), _azureSearchSettings.IndexName, credential);
            }
            else
            {
                var missingConfig = IsDevelopment() ? "SearchApiKeyOverride" : "UserAssignedMIClientId";
                throw new ArgumentException($"Configuration for {missingConfig} is missing or invalid.");
            }
        }

        private static bool IsDevelopment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }

        public async Task<SearchResults<SearchDocument>> SearchAsync<SearchDocument>(string searchText, SearchOptions options = null, CancellationToken cancellationToken = default)
        {
            return (await Client.SearchAsync<SearchDocument>(searchText, options, cancellationToken)).Value;
        }
    }
}
