using Azure.Core;
using System.Text.Json.Serialization;

namespace Agent.Core.Models;


public class AzureSupportCenterApolloRequestPayloadWrapper
{
    [JsonPropertyName("properties")]
    public required AzureSupportCenterApolloRequestPayload Properties { get; set; }

    public static AzureSupportCenterApolloRequestPayloadWrapper CreateForSapIdTrigger(string resourceId, string productId, string legacyProductId, string sapId, string legacyTopicId, string serviceIdentifierName, string question)
    {
        ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
        string subscriptionId = resourceIdentifier.SubscriptionId ?? throw new ArgumentException("Resource ID must contain a valid subscription ID", nameof(resourceId));
        return new AzureSupportCenterApolloRequestPayloadWrapper
        {
            Properties = new AzureSupportCenterApolloRequestPayload()
            {
                TriggerCriteria = new List<TriggerCriteria>() {
                    new TriggerCriteria
                    {
                        Name = "SapId",
                        Value = sapId
                    }
                },
                Parameters = new ApolloRequestParameters() {
                    ResourceUri = resourceId,
                    SubscriptionId = subscriptionId,
                    SapId = sapId,
                    SearchText = question,
                    ProductId = productId,
                    LegacyProductId = legacyProductId,
                    LegacyTopicId = legacyTopicId,
                    Preview = "false",
                    UseInsightPickerV2 = "true",
                    ResourceTag = serviceIdentifierName ?? string.Empty
                }
            }
        };
    }
}

public class AzureSupportCenterApolloRequestPayload
{
    [JsonPropertyName("triggerCriteria")]
    public required List<TriggerCriteria> TriggerCriteria { get; set; }

    [JsonPropertyName("parameters")]
    public required ApolloRequestParameters Parameters { get; set; }
}

public class TriggerCriteria
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

public class ApolloRequestParameters
{
    [JsonPropertyName("ResourceUri")]
    public required string ResourceUri { get; set; }

    [JsonPropertyName("SubscriptionId")]
    public required string SubscriptionId { get; set; }

    [JsonPropertyName("SapId")]
    public required string SapId { get; set; }

    [JsonPropertyName("SearchText")]
    public required string SearchText { get; set; }

    [JsonPropertyName("ProductId")]
    public required string ProductId { get; set; }

    [JsonPropertyName("LegacyProductId")]
    public required string LegacyProductId { get; set; }

    [JsonPropertyName("LegacyTopicId")]
    public required string LegacyTopicId { get; set; }

    [JsonPropertyName("Preview")]
    public required string Preview { get; set; } = "false";

    [JsonPropertyName("ResourceTag")]
    public required string ResourceTag { get; set; }

    [JsonPropertyName("UseInsightPickerV2")]
    public required string UseInsightPickerV2 { get; set; } = "true";
}

