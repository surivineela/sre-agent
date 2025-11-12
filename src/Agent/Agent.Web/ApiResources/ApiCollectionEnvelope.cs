//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Agent.Web.ApiResources;

public class ApiCollectionEnvelope<T>
{
    /// <summary>
    /// Collection of resources.
    /// </summary>
    public ApiResponseEnvelope<T>[]? Value { get; set; }

    /// <summary>
    /// Link to next page of resources.
    /// </summary>
    public string? NextLink { get; set; }
}
