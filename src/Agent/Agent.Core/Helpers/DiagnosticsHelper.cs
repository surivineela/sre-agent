// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Core.Configuration;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Helpers
{
    /// <summary>
    /// Helper class for diagnostic operations like fetching detector responses and analyses
    /// </summary>
    public class DiagnosticsHelper
    {
        private readonly ILogger<DiagnosticsHelper> _logger;
        private ApplensSettings _applensSettings;
        private static readonly HttpClient _httpClient = new HttpClient();
        private TokenCredential _tokenCredential;
        private readonly IHostEnvironment _hostEnvironment;

        public DiagnosticsHelper(ILogger<DiagnosticsHelper> logger, ApplensSettings applensSettings, IHostEnvironment hostEnvironment)
        {
            _logger = logger;
            _applensSettings = applensSettings;
            _hostEnvironment = hostEnvironment;
            InitializeTokenCredential();
        }

        private void InitializeTokenCredential()
        {
            var options = new DefaultAzureCredentialOptions();
            
            // Use MsiClientId when running in an environment that supports Managed Identity
            if (!string.IsNullOrEmpty(_applensSettings.MsiClientId) && 
                Environment.GetEnvironmentVariable("MSI_ENDPOINT") != null)
            {
                options.ManagedIdentityClientId = _applensSettings.MsiClientId;
            }
            
            _tokenCredential = new DefaultAzureCredential(options);
        }

        private async Task<string> GetAuthorizationTokenAsync()
        {
            // Skip token retrieval in development environment
            if (_hostEnvironment.IsDevelopment())
            {
                _logger.LogInternalInformation("Skipping authorization token retrieval in development environment");
                return string.Empty;
            }

            try
            {
                var token = await _tokenCredential.GetTokenAsync(
                    new TokenRequestContext(new[] { _applensSettings.Scope }), 
                    CancellationToken.None);
                
                return token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to get Azure authentication token");
                throw;
            }
        }

        /// <summary>
        /// Gets the detector response for a resource with specified start time, enforcing a maximum time range of 3 days.
        /// The end time is always set to current time minus 15 minutes.
        /// </summary>
        /// <param name="resourceId">The Azure resource ID for which to get detector data</param>
        /// <param name="detectorId">The ID of the detector to query</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago if not specified)</param>
        /// <param name="endTime">Optional end time parameter (ignored - always uses current time minus 15 minutes)</param>
        /// <returns>The detector response as a JSON string</returns>
        /// <exception cref="ArgumentException">Thrown when the time range exceeds 3 days</exception>
        public async Task<string> GetDetectorResponseWithTime(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null)
        {
            startTime ??= DateTime.UtcNow.AddHours(-1);
            endTime = DateTime.UtcNow.AddMinutes(-15);

            if (startTime > endTime)
            {
                throw new ArgumentException("Start time must be before end time");
            }

            TimeSpan maxDuration = TimeSpan.FromDays(3);
            TimeSpan actualDuration = endTime.Value - startTime.Value;

            if (actualDuration > maxDuration)
            {
                throw new ArgumentException($"Time range cannot exceed 3 days. Requested: {actualDuration.TotalDays:F1} days");
            }

            string formattedStartTime = startTime.Value.ToString("yyyy-MM-dd HH:mm");
            string formattedEndTime = endTime.Value.ToString("yyyy-MM-dd HH:mm");

            var requestUrl = new Uri(new Uri(_applensSettings.RuntimeHost),
                $"{resourceId}/detectors/{detectorId}?startTime={Uri.EscapeDataString(formattedStartTime)}&endTime={Uri.EscapeDataString(formattedEndTime)}&api-version=2015-08-01");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Add("x-ms-internal-client", "true");
            
            // Add authorization header with bearer token
            string token = await GetAuthorizationTokenAsync();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            // Add proper content with content type header
            request.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
            
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to retrieve detector details. Status Code: {response.StatusCode}, Response: {responseBody}");
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            return jsonResponse;
        }

        /// <summary>
        /// Gets the analysis response for a resource with specified start time, enforcing a maximum time range of 3 days.
        /// The end time is always set to current time minus 15 minutes.
        /// </summary>
        /// <param name="resourceId">The Azure resource ID for which to get analysis data</param>
        /// <param name="analysisId">The ID of the analysis to query</param>
        /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago if not specified)</param>
        /// <param name="endTime">Optional end time parameter (ignored - always uses current time minus 15 minutes)</param>
        /// <returns>The analysis response as a JSON string</returns>
        /// <exception cref="ArgumentException">Thrown when the time range exceeds 3 days</exception>
        public async Task<string> GetAnalysisWithTime(string resourceId, string analysisId, DateTime? startTime = null, DateTime? endTime = null)
        {
            startTime ??= DateTime.UtcNow.AddHours(-1);
            endTime = DateTime.UtcNow.AddMinutes(-15);

            if (startTime > endTime)
            {
                throw new ArgumentException("Start time must be before end time");
            }

            TimeSpan maxDuration = TimeSpan.FromDays(3);
            TimeSpan actualDuration = endTime.Value - startTime.Value;

            if (actualDuration > maxDuration)
            {
                throw new ArgumentException($"Time range cannot exceed 3 days. Requested: {actualDuration.TotalDays:F1} days");
            }

            // First get the analysis response
            string analysisResponse = await GetDetectorResponseWithTime(resourceId, analysisId, startTime, endTime);

            try
            {
                // Parse the JSON response to extract detector IDs
                using JsonDocument document = JsonDocument.Parse(analysisResponse);
                var root = document.RootElement;

                // Create a list to store all detector responses
                List<string> allDetectorResponses = new List<string> { analysisResponse };

                // Check if properties exists in the response
                if (root.TryGetProperty("properties", out JsonElement properties))
                {
                    // Check if dataset exists in properties
                    if (properties.TryGetProperty("dataset", out JsonElement dataset) &&
                        dataset.ValueKind == JsonValueKind.Array)
                    {
                        // Iterate through each item in the dataset array
                        foreach (JsonElement datasetItem in dataset.EnumerateArray())
                        {
                            // Look for renderingProperties which contains detectorIds
                            if (datasetItem.TryGetProperty("renderingProperties", out JsonElement renderingProps))
                            {
                                if (renderingProps.TryGetProperty("detectorIds", out JsonElement detectorIdsElement) &&
                                    detectorIdsElement.ValueKind == JsonValueKind.Array)
                                {
                                    // Extract each detector ID and make a call to GetDetectorResponseWithTime
                                    foreach (JsonElement detectorIdElement in detectorIdsElement.EnumerateArray())
                                    {
                                        string subDetectorId = detectorIdElement.GetString();
                                        if (!string.IsNullOrEmpty(subDetectorId))
                                        {
                                            try
                                            {
                                                // Call GetDetectorResponseWithTime for this detector ID
                                                string detectorResponse = await GetDetectorResponseWithTime(resourceId, subDetectorId, startTime, endTime);
                                                allDetectorResponses.Add(detectorResponse);
                                            }
                                            catch (Exception ex)
                                            {
                                                // Log the error but continue with other detector IDs
                                                _logger.LogInternalWarning(ex, "Failed to get detector response for {subDetectorId}", subDetectorId);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // Deserialize each response and add to a list
                var combinedResponses = new List<JsonElement>();
                foreach (var response in allDetectorResponses)
                {
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(response);
                        combinedResponses.Add(doc.RootElement.Clone());
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogInternalWarning(ex, "Failed to parse detector response. Skipping this response.");
                    }
                }

                // Serialize the list into a JSON array
                return JsonSerializer.Serialize(combinedResponses);
            }
            catch (JsonException ex)
            {
                // If JSON parsing fails, just return the original response
                return $"Failed to parse detector response: {ex.Message}. Original response: {analysisResponse}";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to process analysis response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets detector response for a resource
        /// </summary>
        /// <param name="resourceId">The resource ID to analyze</param>
        /// <param name="detectorId">The ID of the detector to run</param>
        /// <returns>JSON string containing detector results</returns>
        public async Task<string> GetDetectorResponse(string resourceId, string detectorId)
        {
            var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{resourceId}/detectors/{detectorId}");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            
            // Add authorization header with bearer token
            string token = await GetAuthorizationTokenAsync();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to retrieve detector details. Status Code: {response.StatusCode}, Response: {responseBody}");
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            return jsonResponse;
        }
    }
}
