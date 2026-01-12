// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Cli.Tests.E2E.MockBackend;

/// <summary>
/// In-memory implementation of IIncidentFilterApiService for E2E testing.
/// Stores incident filters in a concurrent dictionary without any database dependency.
/// </summary>
public class MockIncidentFilterApiService : IIncidentFilterApiService
{
    private readonly ConcurrentDictionary<string, IIncidentFilterDocument> _filters = new();

    public Task<ApiCommandResult<IIncidentFilterDocument>> GetIncidentFilterAsync(string filterId)
    {
        if (_filters.TryGetValue(filterId, out var filter))
        {
            return Task.FromResult(new ApiCommandResult<IIncidentFilterDocument>(filter));
        }
        return Task.FromResult(new ApiCommandResult<IIncidentFilterDocument>(new NotFoundResult()));
    }

    public Task<ApiCommandResult<IIncidentFilterDocument>> CreateOrUpdateIncidentFilterAsync(
        string filterId,
        IIncidentFilterDocument model,
        bool dryRun = false)
    {
        if (dryRun)
        {
            // Dry run: return success without persisting
            return Task.FromResult(new ApiCommandResult<IIncidentFilterDocument>(model, Guid.NewGuid().ToString()));
        }

        _filters[filterId] = model;
        return Task.FromResult(new ApiCommandResult<IIncidentFilterDocument>(model, Guid.NewGuid().ToString()));
    }

    public Task<ApiCommandResult<IIncidentFilterDocument>> DeleteIncidentFilterAsync(string filterId, bool dryRun = false)
    {
        var exists = _filters.ContainsKey(filterId);

        if (!dryRun && exists)
        {
            _filters.TryRemove(filterId, out _);
        }

        if (exists)
        {
            return Task.FromResult(new ApiCommandResult<IIncidentFilterDocument>(new AcceptedResult()));
        }

        return Task.FromResult(new ApiCommandResult<IIncidentFilterDocument>(new NoContentResult()));
    }

    public Task<ApiCommandResult<List<IIncidentFilterDocument>>> GetIncidentFiltersAsync()
    {
        return Task.FromResult(new ApiCommandResult<List<IIncidentFilterDocument>>([.. _filters.Values]));
    }

    /// <summary>
    /// Clear all stored filters for test isolation.
    /// </summary>
    public void Clear()
    {
        _filters.Clear();
    }
}
