using Azure.Core;
using System.Text.Json.Serialization;

namespace Agent.Core.Models;


public class AzureSupportCenterApolloRequestPayloadWrapper
{
    [JsonPropertyName("properties")]
    public AzureSupportCenterApolloRequestPayload Properties { get; set; }

    public static AzureSupportCenterApolloRequestPayloadWrapper CreateForSapIdTrigger(string resourceId, string productId, string legacyProductId, string sapId, string legacyTopicId, string serviceIdentifierName, string question)
    {
        ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
        string subscriptionId = resourceId.Split('/')[2];
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
                    SubscriptionId = resourceIdentifier.SubscriptionId,
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
    public List<TriggerCriteria> TriggerCriteria { get; set; }

    [JsonPropertyName("parameters")]
    public ApolloRequestParameters Parameters { get; set; }
}

public class TriggerCriteria
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }
}

public class ApolloRequestParameters
{
    [JsonPropertyName("ResourceUri")]
    public string ResourceUri { get; set; }

    [JsonPropertyName("SubscriptionId")]
    public string SubscriptionId { get; set; }

    [JsonPropertyName("SapId")]
    public string SapId { get; set; }

    [JsonPropertyName("SearchText")]
    public string SearchText { get; set; }

    [JsonPropertyName("ProductId")]
    public string ProductId { get; set; }

    [JsonPropertyName("LegacyProductId")]
    public string LegacyProductId { get; set; }

    [JsonPropertyName("LegacyTopicId")]
    public string LegacyTopicId { get; set; }

    [JsonPropertyName("Preview")]
    public string Preview { get; set; } = "false";

    [JsonPropertyName("ResourceTag")]
    public string ResourceTag { get; set; }

    [JsonPropertyName("UseInsightPickerV2")]
    public string UseInsightPickerV2 { get; set; } = "true";
}

