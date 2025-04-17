// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Prometheus.Services;

using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Google.Protobuf;
using Snappier;
using System.Net.Http.Headers;
using Agent.Prometheus.Extensions;
using System.Buffers;

// push metric to azure managed workspace(Azure managed prometheus) using Remote write
// https://prometheus.io/docs/specs/prw/remote_write_spec/
public class RemoteWriteService(ILogger<RemoteWriteService> logger,
                                DashboardSettings dashboardSettings,
                                IHttpClientFactory httpClientFactory,
                                IAuthenticationService authService) : IRemoteWriteService
{
    private async Task<HttpClient> CreateHttpClientAsync()
    {
        var client = httpClientFactory.CreateClient();
        var token = await GetTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // The caller needs to dispose the returned IMemoryOwner<byte> object, e.g. using it in a using statement.
    private static IMemoryOwner<byte> SerializeAndCompress(global::Prometheus.Protobuf.WriteRequest request)
    {
        var serialized = request.ToByteString();
        return Snappy.CompressToMemory(serialized.Span);
    }

    // These headers are required by Prometheus remote write spec
    // https://prometheus.io/docs/specs/prw/remote_write_spec/#headers
    private static void SetRequiredHeaders(HttpContent content)
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        content.Headers.ContentEncoding.Add("snappy");
        content.Headers.Add("X-Prometheus-Remote-Write-Version", "0.1.0");
    }

    private async Task<string> GetTokenAsync()
    {
        // The audience here follows Prometheus's original implementation https://github.com/prometheus/prometheus/blob/v3.2.1/storage/remote/azuread/azuread.go#L44
        // TODO: support fairfax and mooncake since the audience is different from public cloud
        var token = await authService.GetAzureMonitorWorkspaceCredential().GetTokenAsync(new TokenRequestContext(new[] { "https://monitor.azure.com//.default" }), default);
        return token.Token;
    }

    public async Task<bool> RemoteWriteAsync(global::Prometheus.Protobuf.WriteRequest writeRequest)
    {
        using var memoryOwner = SerializeAndCompress(writeRequest);
        // TODO: use Memory<byte>/ReadOnlySpan<byte> instead of byte[] to avoid copying the data
        using var content = new ByteArrayContent(memoryOwner.Memory.ToArray());
        SetRequiredHeaders(content);

        using var client = await CreateHttpClientAsync();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(dashboardSettings.MetricsIngestionEndpoint))
        {
            Content = content,
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            logger.LogError("Remote-write failed with status code: {StatusCode}. Messge: {ErrorMessage}", response.StatusCode, errorMessage);
            return false;
        }

        logger.LogInformation("Remote-write succeeded with status code: {StatusCode}", response.StatusCode);
        return true;
    }
}