using System.Text;
using System.Text.Json;
using Agent.Core.Models;
using Azure.Core;
using Newtonsoft.Json.Linq;

namespace Agent.Core.Helpers;
public class AzureSupportCenterHelper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const int WAIT_IN_MS_BETWEEN_POLLS_FOR_APOLLO_DIAGNOSTICS = 15000;
    private const int MAX_POLLING_ATTEMPTS = 25;

    // Crawler MI is used for production environment as current solution
    public AzureSupportCenterHelper(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<SupportProductFromArmModel>> GetSupportProductsFromArm(string resourceId)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"/providers/Microsoft.Support/services?api-version=2023-06-01-preview");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        var SupportProductFromArm = json["value"]?.Select(suppSvc => new SupportProductFromArmModel(
                suppSvc["id"]?.ToString(),
                suppSvc["name"]?.ToString(),
                suppSvc["type"]?.ToString(),
                new SupportProductFromArmPropertiesModel(
                    suppSvc["properties"]?["displayName"]?.ToString(),
                    suppSvc["properties"]?["resourceTypes"]?.ToObject<List<string>>() ?? new List<string>(),
                    new SupportProductFromArmPropertiesMetadataModel(
                        suppSvc["properties"]?["metadata"]?["state"]?.ToString(),
                        suppSvc["properties"]?["metadata"]?["groupIds"]?.ToString(),
                        suppSvc["properties"]?["metadata"]?["legacyId"]?.ToString(),
                        suppSvc["properties"]?["metadata"]?["serviceIdentifierName"]?.ToString() ?? string.Empty
                    )
                )
            ))?.ToList() ?? new List<SupportProductFromArmModel>();

        ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);

        var matchingSupportProducts = SupportProductFromArm
            ?.Where(supportProduct => supportProduct.properties.resourceTypes
                .Any(resourceType => resourceIdentifier.ToString().IndexOf(resourceType, StringComparison.OrdinalIgnoreCase) > -1))
            ?.ToList() ?? new List<SupportProductFromArmModel>();

        return matchingSupportProducts;
    }

    public async Task<List<SupportProblemClassificationModel>> GetSupportProblemClassificationsForProduct(Guid productId)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"/providers/Microsoft.Support/services/{productId}/problemClassifications?api-version=2023-06-01-preview");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        var supportProblemClassification = json["value"]?.Select(problemClassification => new SupportProblemClassificationModel(
                problemClassification["id"]?.ToString(),
                problemClassification["name"]?.ToString(),
                new SupportProblemClassificationPropertiesModel(
                    problemClassification["properties"]?["displayName"]?.ToString(),
                    problemClassification["properties"]?["secondaryConsentEnabled"]?.Select(c => new SupportProblemSecondaryConsentModel(
                        c["description"].ToString(),
                        c["type"].ToString())
                    )?.ToList() ?? new List<SupportProblemSecondaryConsentModel>(),
                    new SupportProblemClassificationMetadataModel(
                        problemClassification["shortDescription"]?.ToString(),
                        problemClassification["diagnosticid"]?.ToString(),
                        problemClassification["category"]?.ToString(),
                        problemClassification["searchTags"]?.ToString(),
                        problemClassification["state"]?.ToString(),
                        problemClassification["azureSubscriptionRequired"]?.ToString(),
                        problemClassification["legacyId"]?.ToString()
                        )
                )
            ))?.ToList() ?? new List<SupportProblemClassificationModel>();

        return supportProblemClassification;
    }

    public async Task<AzureSupportCenterApolloResponsePayload> GetDiagnosticResultsFromApollo(string resourceId, SupportProductFromArmModel targetSupportProduct, SupportProblemClassificationModel targetSupportProblemClassification, string question)
    {
        string apolloDiagnosticsReqId = Guid.NewGuid().ToString();
        if(!resourceId.StartsWith("/"))
        {
            resourceId = $"/{resourceId}";
        }

        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{resourceId}/providers/Microsoft.Diagnostics/apollo/{apolloDiagnosticsReqId}?api-version=2020-07-01-preview");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
        var requestPayload = AzureSupportCenterApolloRequestPayloadWrapper.CreateForSapIdTrigger(resourceId,
            targetSupportProduct.name,
            targetSupportProduct.properties.metadata.legacyId,
            targetSupportProblemClassification.name,
            targetSupportProblemClassification.properties.metadata.legacyId,
            targetSupportProduct.properties.metadata.serviceIdentifierName ?? string.Empty,
            question);

        request.Content = new StringContent(JsonSerializer.Serialize<AzureSupportCenterApolloRequestPayloadWrapper>(requestPayload), Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        bool isDiagnosticRunning = false;
        int pollingAttempts = 0;
        AzureSupportCenterApolloResponsePayload apolloDiagnosticResult = null;

        do
        {
            await Task.Delay(WAIT_IN_MS_BETWEEN_POLLS_FOR_APOLLO_DIAGNOSTICS);
            pollingAttempts++;
            var pollResponse = await httpClient.GetAsync(requestUrl);
            pollResponse.EnsureSuccessStatusCode();
            var pollContent = await pollResponse.Content.ReadAsStringAsync();
            apolloDiagnosticResult = JsonSerializer.Deserialize<AzureSupportCenterApolloResponsePayload>(pollContent);

            // If any of the insight status is Running, then the diagnostic is still not complete.
            isDiagnosticRunning = apolloDiagnosticResult?.Properties?.Sections
                .SelectMany(section => section.ReplacementMaps.Diagnostics)?
                .Any(diagnostic => diagnostic.Status.Equals("Running", StringComparison.OrdinalIgnoreCase)) == true;

        } while (isDiagnosticRunning && pollingAttempts < MAX_POLLING_ATTEMPTS);

        if (isDiagnosticRunning && pollingAttempts >= MAX_POLLING_ATTEMPTS)
        {
            throw new Exception($"Apollo diagnostic did not complete after {MAX_POLLING_ATTEMPTS} polling attempts.");
        }

        if (apolloDiagnosticResult == null)
        {
            throw new Exception("Apollo diagnostic result is null.");
        }

        return apolloDiagnosticResult;
    }
}
