// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Core.Interfaces;

public interface ILogAnalyticsService
{
    public Task<IReadOnlyCollection<ContainerAppLogAnalyticsLog>> GetContainerAppSystemLogsAsync(
       string workspaceId,
       string containerAppName,
       DateTimeOffset startTime,
       DateTimeOffset endTime,
       string? revisionName = null,
       string? aggregateOver = null,
       CancellationToken cancellationToken = default);

    public Task<IReadOnlyCollection<ContainerAppLogAnalyticsLog>> GetContainerAppApplicationLogsAsync(
        string workspaceId,
        string containerAppName,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? revisionName = null,
        string? aggregateOver = null,
        CancellationToken cancellationToken = default);

    public Task<string> GetLatestImagePullingLogAsync(
        string workspaceId,
        string containerAppName,
        string revisionName,
        TimeSpan ago,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyCollection<string>> GetAllImagePullingLogsAsync(
        string workspaceId,
        string containerAppName,
        TimeSpan ago,
        CancellationToken cancellationToken = default);
}
