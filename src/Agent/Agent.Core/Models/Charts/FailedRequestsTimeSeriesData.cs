// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Charts
{
    /// <summary>
    /// Represents time series data for function failures
    /// </summary>
    /// <param name="TimeStamp">The timestamp of the data point</param>
    /// <param name="FunctionName">The name of the function</param>
    /// <param name="FailedCount">The number of failed requests for the function</param>
    public record FailedRequestsTimeSeriesData(DateTime TimeStamp, string FunctionName, double FailedCount);
}
