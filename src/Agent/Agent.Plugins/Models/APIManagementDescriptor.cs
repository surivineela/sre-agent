using System.Text.Json.Serialization;
using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Plugins.Models
{
    public sealed record APIManagementDescriptor(
        string ResourceId,
        string Name,
        string Type,
        string Location,
        string ResourceGroup,
        string? PublisherEmail = null,
        string? PublisherName = null,
        string? PublicIPAddresses = null,
        string? PrivateIPAddresses = null,
        string? VirtualNetworkType = null,
        string? PublicNetworkAccess = null,
        string? GatewayUri = null,
        string? GatewayRegionalUri = null,
        string? ManagementApiUri = null,
        string? DeveloperPortalUri = null,
        string? DeveloperPortalStatus = null,
        string? PortalUri = null,
        string? ScmUri = null,
        string? Certificates = null,
        string? EnableClientCertificate = null,
        string? CustomProperties = null,
        string? ProvisioningState = null,
        string? PlatformVersion = null,
        string? CreatedAtUtc = null,
        string? NatGatewayState = null,
        string? LegacyPortalStatus = null,
        string? HostNames = null,
        SkuDescriptor? SkuData = null,
        VNetConfigDescriptor? VNetConfig = null,
        SystemDataDescriptor? SystemData = null,
        AppHealthInfo? AppHealthInfo = null,
        List<APIManagementBackendDescriptor>? Backends = null
    );

    public sealed record SystemDataDescriptor(
        string? CreatedOn,
        string? CreatedBy,
        string? CreatedByType,
        string? LastModifiedOn,
        string? LastModifiedBy,
        string? LastModifiedByType
    );

    public sealed record SkuDescriptor(
        string? SkuName,
        int? SkuCapacity
    );

    public sealed record VNetConfigDescriptor(
        string? SubnetName,
        string? SubnetResourceId,
        Guid? VnetId
    );
    
    public record APIMActivityLogEntry(
        string Timestamp,
        string Operation,
        string Event,
        string Status,
        string URI,
        string Caller
    );

    public record APIManagementApiDescriptor(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("properties")] APIMApiProperties Properties
    );

    public record APIMApiProperties(
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("apiRevision")] string ApiRevision,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("subscriptionRequired")] bool SubscriptionRequired,
        [property: JsonPropertyName("serviceUrl")] string? ServiceUrl,
        [property: JsonPropertyName("backendId")] string? BackendId,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("protocols")] string[] Protocols,
        [property: JsonPropertyName("authenticationSettings")] APIMAuthenticationSettings AuthenticationSettings,
        [property: JsonPropertyName("subscriptionKeyParameterNames")] APIMSubscriptionKeyParameterNames SubscriptionKeyParameterNames,
        [property: JsonPropertyName("isCurrent")] bool IsCurrent
    );

    public record APIMAuthenticationSettings(
        [property: JsonPropertyName("oAuth2")] object? OAuth2,
        [property: JsonPropertyName("openid")] object? Openid,
        [property: JsonPropertyName("oAuth2AuthenticationSettings")] object[] OAuth2AuthenticationSettings,
        [property: JsonPropertyName("openidAuthenticationSettings")] object[] OpenidAuthenticationSettings
    );

    public record APIManagementApiOperationSummary(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("properties")] APIMApiOperationSummaryProperties Properties
    );

    public record APIMApiOperationSummaryProperties(
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("urlTemplate")] string UrlTemplate
    );

    public record APIMSubscriptionKeyParameterNames(
        [property: JsonPropertyName("header")] string Header,
        [property: JsonPropertyName("query")] string Query
    );

    public record APIManagementApiOperationDescriptor(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("properties")] APIMApiOperationProperties Properties
    );

    public record APIMApiOperationProperties(
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("urlTemplate")] string UrlTemplate,
        [property: JsonPropertyName("templateParameters")] object[] TemplateParameters,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("request")] APIMApiOperationRequest? Request,
        [property: JsonPropertyName("responses")] List<APIMApiOperationResponse> Responses,
        [property: JsonPropertyName("policies")] string? Policies
    );

    public record APIMApiOperationResponse(
        [property: JsonPropertyName("statusCode")] int StatusCode,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("representations")] List<APIMApiRepresentation> Representations,
        [property: JsonPropertyName("headers")] object[] Headers
    );

    public record APIMApiRepresentation(
        [property: JsonPropertyName("contentType")] string ContentType,
        [property: JsonPropertyName("examples")] Dictionary<string, APIMApiExample> Examples,
        [property: JsonPropertyName("schemaId")] string? SchemaId,
        [property: JsonPropertyName("typeName")] string? TypeName,
        [property: JsonPropertyName("generatedSample")] string? GeneratedSample,
        [property: JsonPropertyName("sample")] string? Sample
    );

    public record APIMApiOperationRequest(
        [property: JsonPropertyName("queryParameters")] object[] QueryParameters,
        [property: JsonPropertyName("headers")] object[] Headers,
        [property: JsonPropertyName("representations")] object[] Representations
    );

    public record APIMApiExample(
        [property: JsonPropertyName("value")] object Value
    );

    public record APIManagementBackendDescriptor(
        [property: JsonPropertyName("backendName")] string BackendName,
        [property: JsonPropertyName("resourceUri")] string? ResourceUri,
        [property: JsonPropertyName("armResourceId")] string? ArmResourceId,
        [property: JsonPropertyName("connections")] List<APIManagementBackendConnectionDescriptor> Connections
    );

    public record APIManagementBackendConnectionDescriptor(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("level")] string Level
    );
}
