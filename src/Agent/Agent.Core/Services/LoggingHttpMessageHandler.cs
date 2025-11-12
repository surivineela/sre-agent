// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services
{
    public class LoggingHttpMessageHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingHttpMessageHandler> _logger;

        public LoggingHttpMessageHandler(ILogger<LoggingHttpMessageHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString();

            // Log request
            _logger.LogInternalInformation("HTTP Request [{RequestId}]: {Method} {Uri}", requestId, request.Method, request.RequestUri);

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInternalInformation("HTTP Response [{RequestId}]: {StatusCode} in {ElapsedMs}ms",
                        requestId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    string requestContent = string.Empty;
                    if (request.Content is not null)
                    {
                        requestContent = await request.Content.ReadAsStringAsync();
                    }
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInternalWarning("HTTP Request Failed [{RequestId}]: {Method} {Uri} returned {StatusCode} in {ElapsedMs}ms. Request: {RequestContent}, Response: {ResponseContent}",
                        requestId, request.Method, request.RequestUri, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, requestContent, responseContent);
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogInternalError(ex, "HTTP Request Exception [{RequestId}]: {Method} {Uri} failed after {ElapsedMs}ms",
                    requestId, request.Method, request.RequestUri, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
