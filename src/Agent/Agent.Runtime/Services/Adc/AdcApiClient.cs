// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Adc;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.Adc;

/// <summary>
/// Pure ADC REST API client implementation.
/// Handles HTTP communication with ADC service without any persistence logic.
/// </summary>
public class AdcApiClient : IAdcApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AdcSettings _adcSettings;
    private readonly ILogger<AdcApiClient> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public AdcApiClient(
        IHttpClientFactory httpClientFactory,
        AdcSettings adcSettings,
        ILogger<AdcApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _adcSettings = adcSettings;
        _logger = logger;
    }

    #region Disk Images

    public async Task<string> CreateDiskImageAsync(string baseImage, CancellationToken cancellationToken = default)
    {
        var request = new AdcCreateDiskImageRequest
        {
            Image = new AdcDiskImageSpec { Base = baseImage }
        };

        var response = await SendAdcRequestAsync<AdcCreateDiskImageRequest, AdcDiskImageResponse>(
            HttpMethod.Put, "/diskimages", request, cancellationToken);

        return response.Id;
    }

    public async Task<AdcDiskImageInfo> GetDiskImageAsync(string diskImageId, CancellationToken cancellationToken = default)
    {
        var response = await SendAdcRequestAsync<object?, AdcDiskImageResponse>(
            HttpMethod.Get, $"/diskimages/{diskImageId}", null, cancellationToken);

        return new AdcDiskImageInfo
        {
            Id = response.Id,
            State = response.Status?.State ?? "Unknown",
            ErrorMessage = response.Status?.ErrorMessage
        };
    }

    public async Task WaitForDiskImageReadyAsync(string diskImageId, CancellationToken cancellationToken = default)
    {
        var maxRetries = 60; // 5 minutes with 5-second intervals
        for (int i = 0; i < maxRetries; i++)
        {
            var diskImage = await GetDiskImageAsync(diskImageId, cancellationToken);

            if (diskImage.State == "Ready")
            {
                return;
            }

            if (diskImage.State == "Failed")
            {
                throw new InvalidOperationException($"Disk image creation failed: {diskImage.ErrorMessage}");
            }

            await Task.Delay(5000, cancellationToken);
        }

        throw new TimeoutException($"Disk image {diskImageId} did not become ready within timeout");
    }

    #endregion

    #region Sandboxes

    public async Task<AdcSandboxResponse> CreateSandboxFromDiskImageAsync(string diskImageId, AdcResources resources, CancellationToken cancellationToken = default)
    {
        var request = new AdcCreateSandboxRequest
        {
            SourcesRef = new AdcSourcesRef { DiskImage = new AdcDiskImageRef { Id = diskImageId, IsPublic = false } },
            Resources = new AdcResourcesSpec { Cpu = resources.Cpu, Memory = resources.Memory, Disk = resources.Disk }
        };

        return await SendAdcRequestAsync<AdcCreateSandboxRequest, AdcSandboxResponse>(
            HttpMethod.Put, "/sandboxes", request, cancellationToken);
    }

    public async Task<AdcSandboxResponse> CreateSandboxFromSnapshotAsync(string snapshotId, Dictionary<string, string>? labels = null, CancellationToken cancellationToken = default)
    {
        var request = new AdcCreateSandboxRequest
        {
            Labels = labels ?? new Dictionary<string, string>(),
            SourcesRef = new AdcSourcesRef { Snapshot = new AdcSnapshotRef { Id = snapshotId } }
        };

        return await SendAdcRequestAsync<AdcCreateSandboxRequest, AdcSandboxResponse>(
            HttpMethod.Put, "/sandboxes", request, cancellationToken);
    }

    public async Task<AdcSandboxResponse> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        return await SendAdcRequestAsync<object?, AdcSandboxResponse>(
            HttpMethod.Get, $"/sandboxes/{sandboxId}", null, cancellationToken);
    }

    public async Task WaitForSandboxRunningAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        var maxRetries = 60; // 5 minutes with 5-second intervals
        for (int i = 0; i < maxRetries; i++)
        {
            var sandbox = await GetSandboxAsync(sandboxId, cancellationToken);

            if (sandbox.State == "Running")
            {
                return;
            }

            if (sandbox.State == "Failed" || sandbox.State == "Error")
            {
                throw new InvalidOperationException($"Sandbox creation failed with state: {sandbox.State}");
            }

            await Task.Delay(5000, cancellationToken);
        }

        throw new TimeoutException($"Sandbox {sandboxId} did not become running within timeout");
    }

    public async Task DeleteSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        await SendAdcRequestAsync<object?, object?>(
            HttpMethod.Delete, $"/sandboxes/{sandboxId}", null, cancellationToken);
    }

    public async Task StartSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        await SendAdcRequestAsync<object?, object?>(
            HttpMethod.Post, $"/sandboxes/{sandboxId}/start", null, cancellationToken);
    }

    public async Task StopSandboxAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        await SendAdcRequestAsync<object?, object?>(
            HttpMethod.Post, $"/sandboxes/{sandboxId}/stop", null, cancellationToken);
    }

    #endregion

    #region Ports

    public async Task<IReadOnlyList<AdcPortInfo>> UpdatePortsAsync(string sandboxId, IEnumerable<AdcPortSpec> ports, CancellationToken cancellationToken = default)
    {
        var request = new AdcUpdatePortsRequest
        {
            Ports = ports.ToList()
        };

        var response = await SendAdcRequestAsync<AdcUpdatePortsRequest, AdcUpdatePortsResponse>(
            HttpMethod.Put, $"/sandboxes/{sandboxId}/ports", request, cancellationToken);

        return response.Ports;
    }

    #endregion

    #region Snapshots

    public async Task<string> CreateSnapshotAsync(string sandboxId, string snapshotName, Dictionary<string, string>? labels = null, CancellationToken cancellationToken = default)
    {
        var request = new AdcCreateSnapshotRequest { Labels = labels ?? new Dictionary<string, string>() };

        var response = await SendAdcRequestAsync<AdcCreateSnapshotRequest, AdcCreateSnapshotResponse>(
            HttpMethod.Post, $"/sandboxes/{sandboxId}/snapshot", request, cancellationToken);

        return response.Id;
    }

    public async Task<AdcSnapshotResponse> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default)
    {
        return await SendAdcRequestAsync<object?, AdcSnapshotResponse>(
            HttpMethod.Get, $"/snapshots/{snapshotId}", null, cancellationToken);
    }

    public async Task DeleteSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default)
    {
        await SendAdcRequestAsync<object?, object?>(
            HttpMethod.Delete, $"/snapshots/{snapshotId}", null, cancellationToken);
    }

    #endregion

    #region HTTP Helpers

    private async Task<TResponse> SendAdcRequestAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest? body,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(Constants.HttpClientForAdcManagement);
        var url = $"{_adcSettings.BaseUrl.TrimEnd('/')}{path}";

        using var request = new HttpRequestMessage(method, url);

        if (body != null && method != HttpMethod.Get)
        {
            var json = JsonSerializer.Serialize(body, s_jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        _logger.LogInternalInformation("ADC API {Method} {Path}", method, path);

        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInternalError("ADC API error: {StatusCode} {Content}", (int)response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        if (typeof(TResponse) == typeof(object))
        {
            return default!;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(content, s_jsonOptions)!;
    }

    #endregion
}
