// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Data.DatabaseClients.Attributes;
using Newtonsoft.Json;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public class APICenterNode : ArmResourceNode
{
    [GraphJsonProperty("resourceLinks")] public List<ApicLinkEntity>? ResourceLinks { get; set; }

    // The key string is an ApicResource identifier
    [GraphJsonProperty("apiConnectionInfo")] public Dictionary<string, ApiConnectionEntity>? ApiConnectionInfo { get; set; }

    public class ApicLinkEntity
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public ApicResource? Source { get; set; }
        public ApicResource? Target { get; set; }
        public Dictionary<string, object>? CustomProperties { get; set; }
    }

    public class ApicResource
    {
        public string? Identifier { get; set; }
        public ApicResourceType? Type { get; set; }
    }

    public class ApiConnectionEntity
    {
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? ApiSourceId { get; set; }
        public string? ApimSourceId { get; set; }
        public string? Workspace { get; set; }
    }

    [DataContract]
    public enum ApicResourceType
    {
        [EnumMember(Value = "api")] Api,
        [EnumMember(Value = "api-version")] ApiVersion,
        [EnumMember(Value = "definition")] Definition,
        [EnumMember(Value = "environment")] Environment,
        [EnumMember(Value = "deployment")] Deployment,
        [EnumMember(Value = "git-repo")] GitRepo,
        [EnumMember(Value = "azure-resource")] AzureResource,
        [EnumMember(Value = "aws-resource")] AwsResource,
        [EnumMember(Value = "unspecified")] Unspecified
    }

    public APICenterNode(string resourceType, string resourceId, string subscriptionId, string resourceGroupName, string resourceName, string? location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
    }

    public void PopulateFromApiCenterResourceLinks(string resourceLinksJson)
    {
        try
        {
            // Initialize ApiConnectionInfo if it doesn't exist
            ApiConnectionInfo ??= new Dictionary<string, ApiConnectionEntity>();

            using var jsonDocument = JsonDocument.Parse(resourceLinksJson);
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("value", out var valueArray))
            {
                ResourceLinks = new List<ApicLinkEntity>();

                foreach (var linkElement in valueArray.EnumerateArray())
                {
                    var linkEntity = new ApicLinkEntity
                    {
                        Id = linkElement.TryGetProperty("id", out var id) ? id.GetString() : null,
                        Name = linkElement.TryGetProperty("name", out var name) ? name.GetString() : null,
                        Type = linkElement.TryGetProperty("type", out var type) ? type.GetString() : null
                    };

                    if (linkElement.TryGetProperty("properties", out var properties))
                    {
                        if (properties.TryGetProperty("title", out var title))
                        {
                            linkEntity.Title = title.GetString();
                        }

                        if (properties.TryGetProperty("source", out var source))
                        {
                            linkEntity.Source = ParseApicResource(source);

                            // Add source identifier to ApiConnectionInfo as we process each link
                            if (linkEntity.Source?.Identifier != null && !string.IsNullOrEmpty(linkEntity.Source.Identifier))
                            {
                                if (!ApiConnectionInfo.ContainsKey(linkEntity.Source.Identifier))
                                {
                                    // Identifier format: /workspaces/{workspace}/apis/{api}
                                    var match = Regex.Match(linkEntity.Source.Identifier, @"^/workspaces/([^/]+)/apis/([^/]+)$");
                                    if (match.Success)
                                    {
                                        ApiConnectionInfo[linkEntity.Source.Identifier] = new ApiConnectionEntity
                                        {
                                            Workspace = match.Groups[1].Value,
                                            Name = match.Groups[2].Value
                                        };
                                    }
                                }
                            }
                        }

                        if (properties.TryGetProperty("target", out var target))
                        {
                            linkEntity.Target = ParseApicResource(target);
                        }

                        if (properties.TryGetProperty("customProperties", out var customProps))
                        {
                            linkEntity.CustomProperties = new Dictionary<string, object>();
                            foreach (var prop in customProps.EnumerateObject())
                            {
                                if (!string.IsNullOrEmpty(prop.Name) && prop.Value.ValueKind != JsonValueKind.Undefined)
                                {
                                    linkEntity.CustomProperties.Add(prop.Name, prop.Value.GetRawText());
                                }
                            }
                        }
                    }

                    if (linkEntity.Id != null && linkEntity.Name != null && linkEntity.Type != null)
                    {
                        ResourceLinks.Add(linkEntity);
                    }
                }
            }
        } // Check the APIConnection Info dictionary to ensure it has the expected structure
        catch (Exception)
        {
            Console.WriteLine($"Failed to parse API Center resource links JSON: {resourceLinksJson}");
            ResourceLinks = null;
        }
    }

    private ApicResource ParseApicResource(JsonElement resourceElement)
    {
        var resource = new ApicResource();

        if (resourceElement.TryGetProperty("identifier", out var identifier))
        {
            resource.Identifier = identifier.GetString();
        }

        if (resourceElement.TryGetProperty("type", out var type))
        {
            var typeString = type.GetString()?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(typeString))
            {
                return new ApicResource();
            }

            resource.Type = JsonConvert.DeserializeObject<ApicResourceType>($"\"{typeString}\"");
        }

        return resource;
    }
}

public class ApiCenterEdgeDependencyInfo
{
    public string? SourceApiId { get; set; }
    public string? SourceApiTitle { get; set; }
    public APICenterNode.ApicResource? TargetResource { get; set; }
}
