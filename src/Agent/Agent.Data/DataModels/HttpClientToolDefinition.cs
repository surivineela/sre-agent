// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Data.Tools
{
    /// <summary>
    /// YAML tool definition for making HTTP requests to external APIs.
    /// </summary>
    public class HttpClientToolDefinition : YamlToolDefinitionBase
    {
        /// <summary>
        /// The URL template with optional {{param}} placeholders.
        /// </summary>
        [YamlMember(Alias = "url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// The HTTP method to use (GET, POST, PUT, DELETE, PATCH).
        /// </summary>
        [YamlMember(Alias = "method")]
        public string Method { get; set; } = "GET";

        /// <summary>
        /// Optional request body template with {{param}} placeholders.
        /// </summary>
        [YamlMember(Alias = "body")]
        public string? Body { get; set; }

        /// <summary>
        /// Optional list of HTTP headers as key-value pairs.
        /// </summary>
        [YamlMember(Alias = "headers")]
        public List<HttpHeaderDefinition>? Headers { get; set; }

        /// <summary>
        /// Optional authentication settings for the HTTP request.
        /// </summary>
        [YamlMember(Alias = "auth")]
        public HttpClientToolAuth? Auth { get; set; }

        /// <summary>
        /// Optional timeout in seconds for the HTTP request.
        /// </summary>
        [YamlMember(Alias = "timeout_seconds")]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Validates the HTTP client tool configuration.
        /// </summary>
        public override void Validate()
        {
            if (string.IsNullOrWhiteSpace(Url))
                throw new ArgumentException("HTTP client tool must define a non-empty 'url'.");

            var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
            if (!validMethods.Contains(Method.ToUpperInvariant()))
                throw new ArgumentException($"HTTP client tool method must be one of: {string.Join(", ", validMethods)}");
        }
    }

    /// <summary>
    /// Authentication settings for HttpClientTool.
    /// </summary>
    public class HttpClientToolAuth
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
    /// Represents a single HTTP header as a key-value pair.
    /// </summary>
    public class HttpHeaderDefinition
    {
        [YamlMember(Alias = "key")]
        public string Key { get; set; } = string.Empty;

        [YamlMember(Alias = "value")]
        public string Value { get; set; } = string.Empty;
    }
}
