// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.ScheduledTasks.Services;

public static class ScheduledTaskServiceCollectionExtensions
{
    public static IServiceCollection AddScheduledTaskServices(this IServiceCollection services)
    {
        services.AddSingleton<IScheduledTaskManagementService, ScheduledTaskManagementService>();
        services.AddSingleton<ScheduledTaskExecutionService, RuntimeScheduledTaskExecutionService>();

        return services;
    }
}
