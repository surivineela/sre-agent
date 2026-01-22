// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Agent.Data.Tools;
using Agent.Framework;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent HTTP Client Tool storage
/// </summary>
public record HttpClientToolDocumentModel : ToolDocumentModel
{
    public HttpClientToolDocumentModel(ResourceMetadata metadata, HttpClientToolSpec spec)
        : base(metadata, spec)
    {
    }

    public new HttpClientToolSpec Spec => (HttpClientToolSpec)base.Spec;

    public override YamlToolDefinitionBase ToYamlToolDefinition()
    {
        return new HttpClientToolDefinition
        {
            Name = Name,
            Type = Type,
            Connector = Spec.Connector ?? string.Empty,
            Description = Spec.Description ?? string.Empty,
            Parameters = Spec.Parameters ?? new List<YamlParameter>(),
            Attributes = Spec.Attributes ?? new List<string>(),
            Metadata = Metadata.ToYamlMetadata(),
            ToolMode = Spec.ToolMode,
            Url = Spec.Url,
            Method = Spec.Method,
            Body = Spec.Body,
            Headers = Spec.Headers,
            Auth = Spec.Auth != null ? new HttpClientToolAuth
            {
                DataConnector = Spec.Auth.DataConnector,
                Scope = Spec.Auth.Scope
            } : null,
            TimeoutSeconds = Spec.TimeoutSeconds
        };
    }
}

/// <summary>
/// HTTP Client-specific tool spec
/// </summary>
public class HttpClientToolSpec : ToolSpec
{
    /// <summary>
    /// The URL template with optional {{param}} placeholders.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP method to use (GET, POST, PUT, DELETE, PATCH).
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Optional request body template with {{param}} placeholders.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Optional list of HTTP headers as key-value pairs.
    /// </summary>
    public List<HttpHeaderDefinition>? Headers { get; set; }

    /// <summary>
    /// Optional authentication settings.
    /// </summary>
    public HttpClientToolAuthSpec? Auth { get; set; }

    /// <summary>
    /// Timeout in seconds for the HTTP request.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Authentication settings for HttpClientTool in document model.
/// </summary>
public class HttpClientToolAuthSpec
{
    /// <summary>
    /// The data connector name to use for authentication.
    /// </summary>
    public string? DataConnector { get; set; }

    /// <summary>
    /// The OAuth scope to request for the token.
    /// </summary>
    public string? Scope { get; set; }
}
