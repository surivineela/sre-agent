// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KustoToolView), ToolDocumentModel.KustoToolType)]
[JsonDerivedType(typeof(LinkToolView), ToolDocumentModel.LinkToolType)]
[JsonDerivedType(typeof(PythonToolView), ToolDocumentModel.PythonToolType)]
[JsonDerivedType(typeof(HttpClientToolView), ToolDocumentModel.HttpClientToolType)]
public class ToolView
{
    [JsonIgnore] // this is a must because type is already be serialized due to polymorphic deserialization
    public Settable<string> Type { get; set; }

    public Settable<string> Connector { get; set; }

    public Settable<string> Description { get; set; }

    public Settable<List<ParameterView>> Parameters { get; set; }

    public Settable<List<string>> Attributes { get; set; }

    public Settable<ToolMode> ToolMode { get; set; } = Framework.ToolMode.Auto;

    public static ApiResponseEnvelope<ToolView> CreateApiResponseEnvelope(ToolDocumentModel toolDoc)
    {
        ToolView toolView = toolDoc switch
        {
            KustoToolDocumentModel kustoToolDocumentModel => KustoToolView.CreateApiResponseEnvelope(kustoToolDocumentModel).Properties!,
            LinkToolDocumentModel linkToolDocumentModel => LinkToolView.CreateApiResponseEnvelope(linkToolDocumentModel).Properties!,
            PythonToolDocumentModel pythonToolDocumentModel => PythonToolView.CreateApiResponseEnvelope(pythonToolDocumentModel).Properties!,
            HttpClientToolDocumentModel httpClientToolDocumentModel => HttpClientToolView.CreateApiResponseEnvelope(httpClientToolDocumentModel).Properties!,
            _ => throw new NotSupportedException($"Unsupported tool type: {toolDoc.Type}"),
        };

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
                        Properties = new Settable<LinkToolView>(linkToolView),
                    };
                    return LinkToolView.CreateModel(newEnvelope, metadata, (LinkToolDocumentModel?)baseModel);
                }

            case PythonToolView pythonToolView:
                {
                    var newEnvelope = new ApiRequestEnvelope<PythonToolView>
                    {
                        Name = envelope.Name,
                        Type = envelope.Type,
                        Tags = envelope.Tags,
                        Properties = new Settable<PythonToolView>(pythonToolView),
                    };
                    return PythonToolView.CreateModel(newEnvelope, metadata, (PythonToolDocumentModel?)baseModel);
                }

            case HttpClientToolView httpClientToolView:
                {
                    var newEnvelope = new ApiRequestEnvelope<HttpClientToolView>
                    {
                        Name = envelope.Name,
                        Type = envelope.Type,
                        Tags = envelope.Tags,
                        Properties = new Settable<HttpClientToolView>(httpClientToolView),
                    };
                    return HttpClientToolView.CreateModel(newEnvelope, metadata, (HttpClientToolDocumentModel?)baseModel);
                }

            case null:
                {
                    var result = baseModel ?? new ToolDocumentModel(
                        new ResourceMetadata
                        {
                            CreatedAt = DateTime.UtcNow,
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

                    envelope.Name.ApplyTo(name => result.Metadata.Name = name!);
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

    public Settable<bool> Required { get; set; }

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

    public Settable<KustoDisplayOptionsDefinition> DisplayOptions { get; set; }

    public static ApiResponseEnvelope<KustoToolView> CreateApiResponseEnvelope(KustoToolDocumentModel toolDoc)
    {
        var tool = toolDoc.Spec;
        var toolView = new KustoToolView
        {
            Type = tool.Type,
            Connector = tool.Connector,
            Description = tool.Description,
            Attributes = tool.Attributes,
            Mode = tool.Mode,
            Function = tool.Function,
            Query = tool.Query,
            File = tool.File,
            Database = tool.Database,
            ClusterHint = tool.ClusterHint,
            RegionalClusterGroups = tool.RegionalClusterGroups,
            ClusterUri = tool.ClusterUri,
            DisplayOptions = tool.DisplayOptions,
            ToolMode = tool.ToolMode
        };

        var paramView = new List<ParameterView>();
        foreach (var parameter in tool.Parameters ?? [])
        {
            var parameterView = new ParameterView
            {
                Name = parameter.Name,
                Type = parameter.Type,
                Description = parameter.Description,
                Required = parameter.Required,
                Value = parameter.Value,
            };
            paramView.Add(parameterView);
        }
        toolView.Parameters = paramView;

        ApiResponseEnvelope<KustoToolView> apiResponse = new()
        {
            Name = toolDoc.Name,
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

        envelope.Name.ApplyTo(name => result.Metadata.Name = name!);
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
                    Required = p.Required,
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
            properties.DisplayOptions.ApplyTo(value => result.Spec.DisplayOptions = value);
            properties.ToolMode.ApplyTo(value => result.Spec.ToolMode = value);
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
        var toolView = new LinkToolView
        {
            Type = tool.Type,
            Connector = tool.Connector,
            Description = tool.Description,
            Attributes = tool.Attributes,
            Template = tool.Template,
            ToolMode = tool.ToolMode,
        };

        var paramView = new List<ParameterView>();
        foreach (var parameter in tool.Parameters ?? [])
        {
            var parameterView = new ParameterView
            {
                Name = parameter.Name,
                Type = parameter.Type,
                Description = parameter.Description,
                Required = parameter.Required,
                Value = parameter.Value,
            };
            paramView.Add(parameterView);
        }
        toolView.Parameters = paramView;

        ApiResponseEnvelope<LinkToolView> apiResponse = new()
        {
            Name = toolDoc.Name,
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

        envelope.Name.ApplyTo(name => result.Metadata.Name = name!);
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
                    Required = p.Required,
                    Value = p.Value,
                }).ToList();
            });
            properties.Attributes.ApplyTo(value => result.Spec.Attributes = value!);
            properties.Template.ApplyTo(value => result.Spec.Template = value!);
            properties.ToolMode.ApplyTo(value => result.Spec.ToolMode = value);
        });

        return result;
    }
}

public class PythonToolView : ToolView
{
    public Settable<string> FunctionCode { get; set; }

    public Settable<int> TimeoutSeconds { get; set; }

    public Settable<List<string>> Dependencies { get; set; }

    public Settable<bool> AuthEnabled { get; set; }

    public Settable<List<string>> AuthScopes { get; set; }

    public static ApiResponseEnvelope<PythonToolView> CreateApiResponseEnvelope(PythonToolDocumentModel toolDoc)
    {
        var tool = toolDoc.Spec;
        var toolView = new PythonToolView
        {
            Type = tool.Type,
            Connector = tool.Connector,
            Description = tool.Description,
            Attributes = tool.Attributes,
            FunctionCode = tool.FunctionCode,
            TimeoutSeconds = tool.TimeoutSeconds,
            Dependencies = tool.Dependencies,
            ToolMode = tool.ToolMode,
            AuthEnabled = tool.AuthEnabled,
            AuthScopes = tool.AuthScopes,
        };

        var paramView = new List<ParameterView>();
        foreach (var parameter in tool.Parameters ?? [])
        {
            var parameterView = new ParameterView
            {
                Name = parameter.Name,
                Type = parameter.Type,
                Description = parameter.Description,
                Required = parameter.Required,
                Value = parameter.Value,
            };
            paramView.Add(parameterView);
        }
        toolView.Parameters = paramView;

        ApiResponseEnvelope<PythonToolView> apiResponse = new()
        {
            Name = toolDoc.Name,
            Type = toolDoc.DocumentType,
            Tags = toolDoc.Metadata.Tags,
            Properties = toolView,
        };

        return apiResponse;
    }

    public static PythonToolDocumentModel CreateModel(ApiRequestEnvelope<PythonToolView> envelope, ResourceMetadata? metadata = null, PythonToolDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new PythonToolDocumentModel(
            new ResourceMetadata
            {
                CreatedAt = DateTime.UtcNow,
            },
            new PythonToolSpec()
            {
                Type = ToolDocumentModel.PythonToolType, // This is a must because PythonToolView.Type is always not set due to polymorphic deserialization
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

        envelope.Name.ApplyTo(name => result.Metadata.Name = name!);
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
                    Required = p.Required,
                    Value = p.Value,
                }).ToList();
            });
            properties.Attributes.ApplyTo(value => result.Spec.Attributes = value!);
            properties.FunctionCode.ApplyTo(value => result.Spec.FunctionCode = value!);
            properties.TimeoutSeconds.ApplyTo(value => result.Spec.TimeoutSeconds = value);
            properties.Dependencies.ApplyTo(value => result.Spec.Dependencies = value);
            properties.ToolMode.ApplyTo(value => result.Spec.ToolMode = value);
            properties.AuthEnabled.ApplyTo(value => result.Spec.AuthEnabled = value);
            properties.AuthScopes.ApplyTo(value => result.Spec.AuthScopes = value);
        });

        return result;
    }
}

public class HttpHeaderView
{
    public Settable<string> Key { get; set; }
    public Settable<string> Value { get; set; }
}

/// <summary>
/// Authentication settings view for HttpClientTool.
/// </summary>
public class HttpClientToolAuthView
{
    public Settable<string> DataConnector { get; set; }
    public Settable<string> Scope { get; set; }
}

public class HttpClientToolView : ToolView
{
    public Settable<string> Url { get; set; }

    public Settable<string> Method { get; set; }

    public Settable<string> Body { get; set; }

    public Settable<List<HttpHeaderView>> Headers { get; set; }

    public Settable<HttpClientToolAuthView> Auth { get; set; }

    public Settable<int> TimeoutSeconds { get; set; }

    public static ApiResponseEnvelope<HttpClientToolView> CreateApiResponseEnvelope(HttpClientToolDocumentModel toolDoc)
    {
        var tool = toolDoc.Spec;
        var toolView = new HttpClientToolView
        {
            Type = tool.Type,
            Connector = tool.Connector,
            Description = tool.Description,
            Attributes = tool.Attributes,
            Url = tool.Url,
            Method = tool.Method,
            Body = tool.Body,
            Auth = tool.Auth != null ? new HttpClientToolAuthView
            {
                DataConnector = tool.Auth.DataConnector,
                Scope = tool.Auth.Scope
            } : null,
            TimeoutSeconds = tool.TimeoutSeconds,
            ToolMode = tool.ToolMode,
        };

        // Convert headers
        if (tool.Headers != null)
        {
            toolView.Headers = tool.Headers.Select(h => new HttpHeaderView
            {
                Key = h.Key,
                Value = h.Value,
            }).ToList();
        }

        var paramView = new List<ParameterView>();
        foreach (var parameter in tool.Parameters ?? [])
        {
            var parameterView = new ParameterView
            {
                Name = parameter.Name,
                Type = parameter.Type,
                Description = parameter.Description,
                Required = parameter.Required,
                Value = parameter.Value,
            };
            paramView.Add(parameterView);
        }
        toolView.Parameters = paramView;

        ApiResponseEnvelope<HttpClientToolView> apiResponse = new()
        {
            Name = toolDoc.Name,
            Type = toolDoc.DocumentType,
            Tags = toolDoc.Metadata.Tags,
            Properties = toolView,
        };

        return apiResponse;
    }

    public static HttpClientToolDocumentModel CreateModel(ApiRequestEnvelope<HttpClientToolView> envelope, ResourceMetadata? metadata = null, HttpClientToolDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new HttpClientToolDocumentModel(
            new ResourceMetadata
            {
                CreatedAt = DateTime.UtcNow,
            },
            new HttpClientToolSpec()
            {
                Type = ToolDocumentModel.HttpClientToolType,
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

        envelope.Name.ApplyTo(name => result.Metadata.Name = name!);
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
                    Required = p.Required,
                    Value = p.Value,
                }).ToList();
            });
            properties.Attributes.ApplyTo(value => result.Spec.Attributes = value!);
            properties.Url.ApplyTo(value => result.Spec.Url = value!);
            properties.Method.ApplyTo(value => result.Spec.Method = value!);
            properties.Body.ApplyTo(value => result.Spec.Body = value);
            properties.Headers.ApplyTo(value =>
            {
                if (value == null)
                {
                    result.Spec.Headers = null;
                    return;
                }

                result.Spec.Headers = value.Select(h => new Agent.Data.Tools.HttpHeaderDefinition
                {
                    Key = h.Key!,
                    Value = h.Value!,
                }).ToList();
            });
            properties.Auth.ApplyTo(value => result.Spec.Auth = value != null ? new Agent.Data.DataModels.HttpClientToolAuthSpec
            {
                DataConnector = value.DataConnector,
                Scope = value.Scope
            } : null);
            properties.TimeoutSeconds.ApplyTo(value => result.Spec.TimeoutSeconds = value);
            properties.ToolMode.ApplyTo(value => result.Spec.ToolMode = value);
        });

        return result;
    }
}
