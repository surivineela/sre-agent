// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface
{
    public interface ICdbSDKDiagnosePlugin
    {
        string SDKAnalyze(string error);
        
        Task<string> FetchCosmosDbSdkError(string appInsightsResourceId, string? timeSpan = "PT1H");
        
        Task<string> DiagnoseCosmosDbSdkErrors(string appInsightsResourceId, string? timeSpan = "PT1H");
    }
}
