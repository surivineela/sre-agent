// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Azure.Core;
using Azure.ResourceManager;

namespace Agent.Core.Interfaces;

public interface IArmClientFactory
{

    /// <summary>
    /// Get Arm client for arm readonly operations
    /// </summary>
    /// <returns></returns>
    public Task<ArmClient> GetArmOperationClient();

    /// <summary>
    /// Get Arm client for crawling
    /// </summary>
    /// <returns></returns>
    public ArmClient GetCrawlerArmClient();
    
}

