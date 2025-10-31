// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Web.Json;
using Agent.Web.ApiResources;
using System.Text.Json.Serialization;

namespace Agent.Web.Views.v2;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KustoToolView), ToolDocumentModel.KustoToolType)]
[JsonDerivedType(typeof(LinkToolView), ToolDocumentModel.LinkToolType)]
public class ToolView
{
    [JsonIgnore] // this is a must because type is already be serialized due to polymorphic deserialization
    public Settable<string> Type { get; set; }

    public Settable<string> Connector { get; set; }

    public Settable<string> Description { get; set; }

    public Settable<List<ParameterView>> Parameters { get; set; }

    public Settable<List<string>> Attributes { get; set; }

    public static ApiResponseEnvelope<ToolView> CreateApiResponseEnvelope(ToolDocumentModel toolDoc)
    {
        ToolView toolView;
        switch (toolDoc)
        {
            case KustoToolDocumentModel kustoToolDocumentModel:
                toolView = KustoToolView.CreateApiResponseEnvelope(kustoToolDocumentModel).Properties!;
                break;
            case LinkToolDocumentModel linkToolDocumentModel:
                toolView = LinkToolView.CreateApiResponseEnvelope(linkToolDocumentModel).Properties!;
                break;
            default:
                throw new NotSupportedException($"Unsupported tool type: {toolDoc.Type}");
        }

        return new ApiResponseEnvelope<ToolView>
        {
            Name = toolDoc.Name,
            Type = toolDoc.DocumentType,
            Tags = toolDoc.Metadata.Tags,
            Properties = toolView,
        };
    }

    public static ToolDocumentModel CreateModel(ApiRequestEnvelope<ToolView> envelope, ResourceMetadata? metadata = null, ToolDocumentModel? baseModel = null)
    {
        switch (envelope.Properties.Value)
        {
            case KustoToolView kustoToolView:
                {
                    var newEnvelope = new ApiRequestEnvelope<KustoToolView>
                    {
                        Name = envelope.Name,
                        Type = envelope.Type,
                        Tags = envelope.Tags,
                        Owner = envelope.Owner,
                        Properties = new Settable<KustoToolView>(kustoToolView),
                    };
                    return KustoToolView.CreateModel(newEnvelope, metadata, (KustoToolDocumentModel?)baseModel);
                }

            case LinkToolView linkToolView:
                {
                    var newEnvelope = new ApiRequestEnvelope<LinkToolView>
                    {
                        Name = envelope.Name,
                        Type = envelope.Type,
                        Tags = envelope.Tags,
                        Owner = envelope.Owner,
                        Properties = new Settable<LinkToolView>(linkToolView),
                    };
                    return LinkToolView.CreateModel(newEnvelope, metadata, (LinkToolDocumentModel?)baseModel);
                }

            case null:
                {
                    var result = baseModel ?? new ToolDocumentModel(
                        new ResourceMetadata
                        {
                            CreatedAt = DateTime.UtcNow,
                            Version = "v2",
                        },
                        new ToolSpec()
                    );

                    if (metadata != null)
                    {
                        result = result with
                        {
                            Metadata = metadata,
                        };
                    }

                    result.Metadata.UpdatedAt = DateTime.UtcNow;

                    envelope.Name.ApplyTo(name => result.Spec.Name = name!);
                    envelope.Owner.ApplyTo(owner => result.Metadata.Owner = owner);
                    envelope.Tags.ApplyTo(tag => result.Metadata.Tags = tag);

                    return result;
                }
            default:
                throw new NotSupportedException($"Unsupported tool type: {envelope.Properties.Value.GetType().Name}");
        }
    }
}

public class ParameterView
{
    public Settable<string> Name { get; set; }

    public Settable<string> Type { get; set; }

    public Settable<string> Description { get; set; }

    public Settable<string> MapTo { get; set; }

    public Settable<bool> Required { get; set; }

    public Settable<string> Target { get; set; }

    public Settable<object?> Value { get; set; }
}

public class KustoToolView : ToolView
{
    public Settable<KustoExecutionMode> Mode { get; set; }

    public Settable<string> Function { get; set; }

    public Settable<string> Query { get; set; }

    public Settable<string> File { get; set; }

    public Settable<string> Database { get; set; }

    public Settable<string> ClusterHint { get; set; }

    public Settable<List<KustoRegionalGroupSettings>> RegionalClusterGroups { get; set; }

    public Settable<string> ClusterUri { get; set; }

    public static ApiResponseEnvelope<KustoToolView> CreateApiResponseEnvelope(KustoToolDocumentModel toolDoc)
    {
        var tool = toolDoc.Spec;
        var toolView = new KustoToolView();

        toolView.Type = tool.Type;
        toolView.Connector = tool.Connector;
        toolView.Description = tool.Description;

        var paramView = new List<ParameterView>();
        foreach (var parameter in tool.Parameters ?? [])
        {
            var parameterView = new ParameterView
            {
                Name = parameter.Name,
                Type = parameter.Type,
                Description = parameter.Description,
                MapTo = parameter.MapTo,
                Required = parameter.Required,
                Target = parameter.Target,
                Value = parameter.Value,
            };
            paramView.Add(parameterView);
        }
        toolView.Parameters = paramView;
        toolView.Attributes = tool.Attributes;
        toolView.Mode = tool.Mode;
        toolView.Function = tool.Function;
        toolView.Query = tool.Query;
        toolView.File = tool.File;
        toolView.Database = tool.Database;
        toolView.ClusterHint = tool.ClusterHint;
        toolView.RegionalClusterGroups = tool.RegionalClusterGroups;
        toolView.ClusterUri = tool.ClusterUri;

        ApiResponseEnvelope<KustoToolView> apiResponse = new()
        {
            Name = tool.Name,
            Type = toolDoc.DocumentType,
            Tags = toolDoc.Metadata.Tags,
            Properties = toolView,
        };

        return apiResponse;
    }

    public static KustoToolDocumentModel CreateModel(ApiRequestEnvelope<KustoToolView> envelope, ResourceMetadata? metadata = null, KustoToolDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new KustoToolDocumentModel(
            new ResourceMetadata
            {
                CreatedAt = DateTime.UtcNow,
                Version = "v2",
            },
            new KustoToolSpec()
            {
                Type = ToolDocumentModel.KustoToolType, // This is a must because KustoToolView.Type is always not set due to polymorphic deserialization
            }
        );

        if (metadata != null)
        {
            result = result with
            {
                Metadata = metadata,
            };
        }

        result.Metadata.UpdatedAt = DateTime.UtcNow;

        envelope.Name.ApplyTo(name => result.Spec.Name = name!);
        envelope.Owner.ApplyTo(owner => result.Metadata.Owner = owner);
        envelope.Tags.ApplyTo(tag => result.Metadata.Tags = tag);
        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null)
            {
                return;
            }

            properties.Type.ApplyTo(value => result.Spec.Type = value!);
            properties.Connector.ApplyTo(value => result.Spec.Connector = value!);
            properties.Description.ApplyTo(value => result.Spec.Description = value!);
            properties.Parameters.ApplyTo(value =>
            {
                if (value == null)
                {
                    result.Spec.Parameters = null;
                    return;
                }

                result.Spec.Parameters = value.Select(p => new YamlParameter
                {
                    Name = p.Name!,
                    Type = p.Type!,
                    Description = p.Description!,
                    MapTo = p.MapTo!,
                    Required = p.Required,
                    Target = p.Target!,
                    Value = p.Value,
                }).ToList();
            });
            properties.Attributes.ApplyTo(value => result.Spec.Attributes = value);
            properties.Mode.ApplyTo(value => result.Spec.Mode = value);
            properties.Function.ApplyTo(value => result.Spec.Function = value);
            properties.Query.ApplyTo(value => result.Spec.Query = value);
            properties.File.ApplyTo(value => result.Spec.File = value);
            properties.Database.ApplyTo(value => result.Spec.Database = value!);
            properties.ClusterHint.ApplyTo(value => result.Spec.ClusterHint = value);
            properties.RegionalClusterGroups.ApplyTo(value => result.Spec.RegionalClusterGroups = value!);
            properties.ClusterUri.ApplyTo(value => result.Spec.ClusterUri = value);
        });

        return result;
    }
}

public class LinkToolView : ToolView
{
    public Settable<string> Template { get; set; }

    public static ApiResponseEnvelope<LinkToolView> CreateApiResponseEnvelope(LinkToolDocumentModel toolDoc)
    {
        var tool = toolDoc.Spec;
        var toolView = new LinkToolView();

        toolView.Type = tool.Type;
        toolView.Connector = tool.Connector;
        toolView.Description = tool.Description;
        var paramView = new List<ParameterView>();
        foreach (var parameter in tool.Parameters ?? [])
        {
            var parameterView = new ParameterView
            {
                Name = parameter.Name,
                Type = parameter.Type,
                Description = parameter.Description,
                MapTo = parameter.MapTo,
                Required = parameter.Required,
                Target = parameter.Target,
                Value = parameter.Value,
            };
            paramView.Add(parameterView);
        }
        toolView.Attributes = tool.Attributes;
        toolView.Template = tool.Template;

        ApiResponseEnvelope<LinkToolView> apiResponse = new()
        {
            Name = tool.Name,
            Type = toolDoc.DocumentType,
            Tags = toolDoc.Metadata.Tags,
            Properties = toolView,
        };

        return apiResponse;
    }

    public static LinkToolDocumentModel CreateModel(ApiRequestEnvelope<LinkToolView> envelope, ResourceMetadata? metadata = null, LinkToolDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new LinkToolDocumentModel(
            new ResourceMetadata
            {
                CreatedAt = DateTime.UtcNow,
                Version = "v2",
            },
            new LinkToolSpec()
            {
                Type = ToolDocumentModel.LinkToolType, // This is a must because LinkToolView.Type is always not set due to polymorphic deserialization
            }
        );

        if (metadata != null)
        {
            result = result with
            {
                Metadata = metadata,
            };
        }

        result.Metadata.UpdatedAt = DateTime.UtcNow;

        envelope.Name.ApplyTo(name => result.Spec.Name = name!);
        envelope.Owner.ApplyTo(owner => result.Metadata.Owner = owner);
        envelope.Tags.ApplyTo(tag => result.Metadata.Tags = tag);
        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null)
            {
                return;
            }

            properties.Type.ApplyTo(value => result.Spec.Type = value!);
            properties.Connector.ApplyTo(value => result.Spec.Connector = value!);
            properties.Description.ApplyTo(value => result.Spec.Description = value!);
            properties.Parameters.ApplyTo(value =>
            {
                if (value == null)
                {
                    result.Spec.Parameters = null;
                    return;
                }

                result.Spec.Parameters = value.Select(p => new YamlParameter
                {
                    Name = p.Name!,
                    Type = p.Type!,
                    Description = p.Description!,
                    MapTo = p.MapTo!,
                    Required = p.Required,
                    Target = p.Target!,
                    Value = p.Value,
                }).ToList();
            });
            properties.Attributes.ApplyTo(value => result.Spec.Attributes = value!);
            properties.Template.ApplyTo(value => result.Spec.Template = value!);
        });

        return result;
    }
}
