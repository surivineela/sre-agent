// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Text.Json.Serialization;

namespace Agent.Framework
{
    /// <summary>
    /// Represents the outer structure of a streaming error response.
    /// Handles both formats:
    /// 1. Top-level error properties (type, code, message, param)
    /// 2. Nested error object with detailed information
    /// </summary>
    public class AgentStreamingErrorResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("sequence_number")]
        public int? SequenceNumber { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("param")]
        public string? Param { get; set; }

        [JsonPropertyName("error")]
        public AgentStreamingErrorDetail? Error { get; set; }

        /// <summary>
        /// Gets the HTTP status code from the error response.
        /// Checks multiple sources in order: numeric codes, specific error codes, and error types.
        /// Returns -1 if no valid status code can be determined.
        /// </summary>
        public int StatusCode
        {
            get
            {
                // Try parsing direct Code property as numeric
                if (Code != null && int.TryParse(Code, out var parsedCode))
                {
                    return parsedCode;
                }

                // Try parsing nested Error.Code property as numeric
                if (Error?.Code != null && int.TryParse(Error.Code, out var nestedParsedCode))
                {
                    return nestedParsedCode;
                }

                // Check for specific error codes (non-numeric)
                if (Error?.Code != null)
                {
                    if (Error.Code.Equals("too_many_requests", StringComparison.OrdinalIgnoreCase))
                    {
                        return 429;
                    }
                    if (Error.Code.Equals("context_length_exceeded", StringComparison.OrdinalIgnoreCase))
                    {
                        return 400;
                    }
                }

                // Check Error.Type for specific error types
                if (Error?.Type != null)
                {
                    if (Error.Type.Equals("too_many_requests", StringComparison.OrdinalIgnoreCase))
                    {
                        return 429;
                    }
                    if (Error.Type.Equals("invalid_request_error", StringComparison.OrdinalIgnoreCase))
                    {
                        return 400;
                    }
                }

                // Default to -1 if no status code can be determined
                return -1;
            }
        }

        /// <summary>
        /// Gets the error message from the error response.
        /// Checks the direct Message property first, then falls back to nested Error.Message.
        /// Returns empty string if no message is found.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                return Message ?? Error?.Message ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Represents the nested error detail within a streaming error response.
    /// </summary>
    public class AgentStreamingErrorDetail
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("param")]
        public string? Param { get; set; }
    }
}
