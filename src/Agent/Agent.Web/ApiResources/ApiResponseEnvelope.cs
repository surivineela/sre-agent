//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


using Agent.Data.DataModels;

namespace Agent.Web.ApiResources;

/// <summary>
/// Azure Resource Manager API Envelope
/// </summary>
public class ApiResponseEnvelope<T>
{
    /// <summary>
    /// Resource Name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Type of the Resource
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Tags associated with resource.
    /// </summary>
    public IList<string>? Tags { get; set; }

    /// <summary>
    /// Resource specific properties.
    /// </summary>
    public T? Properties { get; set; }
}