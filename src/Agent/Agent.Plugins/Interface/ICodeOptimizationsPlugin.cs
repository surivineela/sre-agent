// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;


namespace Agent.Plugins.Interface
{
    public interface ICodeOptimizationsPlugin
    {
        Task<Dictionary<string, IEnumerable<InsightsRecommendationContract>>> GetCodeOptimizationInsightsBulkAsync(IEnumerable<string> resourceIds);
    }
}
