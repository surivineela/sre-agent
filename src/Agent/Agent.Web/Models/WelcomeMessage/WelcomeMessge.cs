// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Core.Models;

namespace Agent.Web.Models.WelcomeMessage;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KnowledgeGraphStatusEnum
{
    InProgress,
    Completed
};

public record OverallCrawlProgress(
    [property: JsonPropertyName("crawled")] uint Crawled,
    [property: JsonPropertyName("totalResources")] uint TotalResources,
    [property: JsonPropertyName("finishedInitialCrawl")] bool FinishedInitialCrawl
);

public record CrawlProgressByResourceType(
    [property: JsonPropertyName("crawled")] uint Crawled,
    [property: JsonPropertyName("totalResources")] uint TotalResources
);

public record KnowledgeGraphStatus(
    [property: JsonPropertyName("status")] KnowledgeGraphStatusEnum Status, // InProgress, Completed
    [property: JsonPropertyName("crawlProgress")] OverallCrawlProgress CrawlProgress,
    [property: JsonPropertyName("crawlProgressByResourceType")] Dictionary<string, CrawlProgressByResourceType> CrawlProgressByResourceType
);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SourceCodeLinkageStatusEnum
{
    Linked,
    RequiresAuth,
    NotLinked
}

public record SourceCodeLinkageStatus(
    [property: JsonPropertyName("status")] SourceCodeLinkageStatusEnum Status, // Linked, RequiresAuth, NotLinked
    [property: JsonPropertyName("repositoryUrl")] string? RepositoryUrl, // present if status is Linked/RequiresAuth
    [property: JsonPropertyName("linkedTimestamp")] DateTime? LinkedTimestamp, // present if status is Linked
    [property: JsonPropertyName("loginCallbackUrl")] string? LoginCallbackUrl // present if status is RequiresAuth
);
public record LogicalApplication(
    [property: JsonPropertyName("resourceId")] string ResourceId,
    [property: JsonPropertyName("sourceCodeLinkageStatus")] SourceCodeLinkageStatus SourceCodeLinkageStatus
);

public record WelcomeMessage(
    [property: JsonPropertyName("knowledgeGraphStatus")] KnowledgeGraphStatus KnowledgeGraphStatus,
    [property: JsonPropertyName("logicalApplications")] List<LogicalApplication> LogicalApplications,
    [property: JsonPropertyName("integrations")] List<IntegrationInfo> Integrations
);
