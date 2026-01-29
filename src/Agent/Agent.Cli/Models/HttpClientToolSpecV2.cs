// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// HTTP Client-specific tool specification for YAML configurations (V2).
    /// Extends the base tool spec with HTTP Client-specific properties.
    /// </summary>
    public class HttpClientToolSpecV2 : ToolSpecV2
    {
        /// <summary>
        /// The URL template with optional {{param}} placeholders.
        /// </summary>
        [YamlMember(Alias = "url", Order = 10)]
        public string? Url { get; set; }

        /// <summary>
        /// The HTTP method to use (GET, POST, PUT, DELETE, PATCH).
        /// </summary>
        [YamlMember(Alias = "method", Order = 11)]
        public string? Method { get; set; }

        /// <summary>
        /// Optional request body template with {{param}} placeholders.
        /// </summary>
        [YamlMember(Alias = "body", Order = 12, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Body { get; set; }

        /// <summary>
        /// Optional list of HTTP headers as key-value pairs.
        /// </summary>
        [YamlMember(Alias = "headers", Order = 13, DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
        public List<HttpHeaderV2>? Headers { get; set; }

        /// <summary>
        /// Optional authentication settings.
        /// </summary>
        [YamlMember(Alias = "auth", Order = 14)]
        public HttpClientToolAuthV2? Auth { get; set; }

        /// <summary>
        /// Timeout in seconds for the HTTP request.
        /// </summary>
        [YamlMember(Alias = "timeout_seconds", Order = 15)]
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Authentication settings for HttpClientTool.
    /// </summary>
    public class HttpClientToolAuthV2
    {
        /// <summary>
        /// The data connector name to use for authentication.
        /// </summary>
        [YamlMember(Alias = "dataconnector")]
        public string? DataConnector { get; set; }

        /// <summary>
        /// The OAuth scope to request for the token.
        /// </summary>
        [YamlMember(Alias = "scope")]
        public string? Scope { get; set; }
    }

    /// <summary>
    /// Represents a single HTTP header as a key-value pair for YAML configuration.
    /// </summary>
    public class HttpHeaderV2
    {
        [YamlMember(Alias = "key")]
        public string? Key { get; set; }

        [YamlMember(Alias = "value")]
        public string? Value { get; set; }
    }
}
