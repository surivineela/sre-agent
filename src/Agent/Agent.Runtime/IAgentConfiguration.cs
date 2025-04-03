// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace Agent.Runtime;

public interface IAgentConfiguration
{
    void ConfigureServices(IServiceCollection services);
    string PathName { get; }
}

