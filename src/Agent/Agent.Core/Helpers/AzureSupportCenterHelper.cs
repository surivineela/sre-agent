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
    private const string SUPPORT_PRODUCTS_FILE_NAME = "SupportProductsFromArm.json";
    private const string SUPPORT_CLASSIFICATION_FILE_NAME = "SupportProblemClassification.json";

    // Crawler MI is used for production environment as current solution
    public AzureSupportCenterHelper(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<SupportProductFromArmModel>> GetSupportProductsFromArm(string resourceId)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(Helpers), "SupportProductMetadata", SUPPORT_PRODUCTS_FILE_NAME);

        var supportProductFromArmResponse = await File.ReadAllTextAsync(path);
        var matchingSupportProducts = new List<SupportProductFromArmModel>();
        try
        {
            var json = JObject.Parse(supportProductFromArmResponse);

            // In GetSupportProductsFromArm, ensure non-null values for SupportProductFromArmModel constructor parameters
            var supportProductFromArm = json["value"]?.Select(suppSvc => new SupportProductFromArmModel(
                    suppSvc["id"]?.ToString() ?? string.Empty,
                    suppSvc["name"]?.ToString() ?? string.Empty,
                    suppSvc["type"]?.ToString() ?? string.Empty,
                    new SupportProductFromArmPropertiesModel(
                        suppSvc["properties"]?["displayName"]?.ToString() ?? string.Empty,
                        suppSvc["properties"]?["resourceTypes"]?.ToObject<List<string>>() ?? new List<string>(),
                        new SupportProductFromArmPropertiesMetadataModel(
                            suppSvc["properties"]?["metadata"]?["state"]?.ToString() ?? string.Empty,
                            suppSvc["properties"]?["metadata"]?["groupIds"]?.ToString() ?? string.Empty,
                            suppSvc["properties"]?["metadata"]?["legacyId"]?.ToString() ?? string.Empty,
                            suppSvc["properties"]?["metadata"]?["serviceIdentifierName"]?.ToString() ?? string.Empty
                        )
                    )
                ))?.ToList() ?? new List<SupportProductFromArmModel>();

            ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);

            matchingSupportProducts = supportProductFromArm
                ?.Where(supportProduct => supportProduct.properties.resourceTypes
                    .Any(resourceType => resourceIdentifier.ToString().IndexOf(resourceType, StringComparison.OrdinalIgnoreCase) > -1))
                ?.ToList() ?? new List<SupportProductFromArmModel>();
        }
        catch (JsonException ex)
        {
            throw new Exception($"Error parsing support product response: {ex.Message}", ex);
        }

        return matchingSupportProducts;
    }

    public async Task<List<SupportProblemClassificationModel>> GetSupportProblemClassificationsForProduct(Guid productId)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(Helpers), "SupportProductMetadata", $"{productId}/{SUPPORT_CLASSIFICATION_FILE_NAME}");
        if (!Path.Exists(path))
        {
            throw new FileNotFoundException($"Support problem classification for {productId} missing. Please update.");
        }

        var supportClassificationResponse = await File.ReadAllTextAsync(path);
        var supportProblemClassification = new List<SupportProblemClassificationModel>();
        try
        {
            var json = JObject.Parse(supportClassificationResponse);

            supportProblemClassification = json["value"]?.Select(problemClassification => new SupportProblemClassificationModel(
                    problemClassification["id"]?.ToString() ?? string.Empty,
                    problemClassification["name"]?.ToString() ?? string.Empty,
                    new SupportProblemClassificationPropertiesModel(
                        problemClassification["properties"]?["displayName"]?.ToString() ?? string.Empty,
                        problemClassification["properties"]?["secondaryConsentEnabled"]?.Select(c => new SupportProblemSecondaryConsentModel(
                            c["description"]?.ToString() ?? string.Empty,
                            c["type"]?.ToString() ?? string.Empty)
                        )?.ToList() ?? new List<SupportProblemSecondaryConsentModel>(),
                        new SupportProblemClassificationMetadataModel(
                            problemClassification["properties"]?["metadata"]?["shortDescription"]?.ToString() ?? string.Empty,
                            problemClassification["properties"]?["metadata"]?["diagnosticid"]?.ToString() ?? string.Empty,
                            problemClassification["properties"]?["metadata"]?["category"]?.ToString() ?? string.Empty,
                            problemClassification["properties"]?["metadata"]?["searchTags"]?.ToString() ?? string.Empty,
                            problemClassification["properties"]?["metadata"]?["state"]?.ToString() ?? string.Empty,
                            problemClassification["properties"]?["metadata"]?["azureSubscriptionRequired"]?.ToString() ?? string.Empty,
                            problemClassification["properties"]?["metadata"]?["legacyId"]?.ToString() ?? string.Empty
                            )
                    )
                ))?.ToList() ?? new List<SupportProblemClassificationModel>();

            supportProblemClassification = supportProblemClassification
                ?.Where(problemClassification => !problemClassification.properties.metadata.state.Equals("retired", StringComparison.OrdinalIgnoreCase))
                ?.ToList() ?? new List<SupportProblemClassificationModel>();
        }
        catch (JsonException ex)
        {
            throw new Exception($"Error parsing support classification response: {ex.Message}", ex);
        }

        return supportProblemClassification;
    }

    public async Task<AzureSupportCenterApolloResponsePayload> GetDiagnosticResultsFromApollo(string resourceId, SupportProductFromArmModel targetSupportProduct, SupportProblemClassificationModel targetSupportProblemClassification, string question)
    {
        string apolloDiagnosticsReqId = Guid.NewGuid().ToString();
        if (!resourceId.StartsWith("/"))
        {
            resourceId = $"/{resourceId}";
        }

        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{resourceId}/providers/Microsoft.Diagnostics/apollo/{apolloDiagnosticsReqId}?api-version=2020-07-01-preview");

        var supportProdctId = ExtractGuidFromId(targetSupportProduct.id);
        var problemClassificationId = ExtractGuidFromId(targetSupportProblemClassification.id);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
        var requestPayload = AzureSupportCenterApolloRequestPayloadWrapper.CreateForSapIdTrigger(resourceId,
            supportProdctId,
            targetSupportProduct.properties.metadata.legacyId,
            problemClassificationId,
            targetSupportProblemClassification.properties.metadata.legacyId,
            targetSupportProduct.properties.metadata.serviceIdentifierName ?? string.Empty,
            question);

        request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        bool isDiagnosticRunning = false;
        int pollingAttempts = 0;
        AzureSupportCenterApolloResponsePayload? apolloDiagnosticResult = null;

        do
        {
            await Task.Delay(WAIT_IN_MS_BETWEEN_POLLS_FOR_APOLLO_DIAGNOSTICS);
            pollingAttempts++;
            var pollResponse = await httpClient.GetAsync(requestUrl);
            pollResponse.EnsureSuccessStatusCode();
            var pollContent = await pollResponse.Content.ReadAsStringAsync();
            apolloDiagnosticResult = JsonSerializer.Deserialize<AzureSupportCenterApolloResponsePayload>(pollContent);

            // If any of the insight status is Running, then the diagnostic is still not complete.
            bool isSectionsDiagnosticRunning = apolloDiagnosticResult?.Properties?.Sections
                .SelectMany(section => section.ReplacementMaps.Diagnostics)?
                .Any(diagnostic => diagnostic.Status.Equals("Running", StringComparison.OrdinalIgnoreCase)) == true;

            bool isDirectMappedDiasgnosticRunning = apolloDiagnosticResult?.Properties?.ReplacementMaps?.Diagnostics?
                .Any(diagnostic => diagnostic.Status.Equals("Running", StringComparison.OrdinalIgnoreCase)) == true;

            isDiagnosticRunning = isSectionsDiagnosticRunning || isDirectMappedDiasgnosticRunning;

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

    private string ExtractGuidFromId(string idField)
    {
        string guidToReturn = idField;
        if (!Guid.TryParseExact(idField, "D", out Guid _))
        {
            // Extract the GUID from id field
            var idParts = idField.TrimEnd('/').Split('/');
            if (idParts.Length > 0)
            {
                idField = idParts.Last();
                if (Guid.TryParseExact(idField, "D", out _))
                {
                    guidToReturn = idField;
                }
                else
                {
                    throw new ArgumentException($"Supplied value does not have a GUID in id as past part: {idField}", nameof(idField));
                }
            }
        }

        return guidToReturn;
    }
}
