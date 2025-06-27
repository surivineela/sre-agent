using System;

namespace Agent.Core.Configuration;

public class GenevaActionsSettings
{
    public string CosmosDbContainerId { get; set; } = string.Empty;
    
    /// <summary>
    /// The duration in hours for which approved approval requests are cached.
    /// Default is 6 hours.
    /// </summary>
    public int ApprovedRequestCacheExpirationHours { get; set; } = 6;
}
