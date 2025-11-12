//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


using Agent.Web.Json;

namespace Agent.Web.ApiResources;

/// <summary>
/// Azure Resource Manager API Envelope
/// </summary>
public class ApiRequestEnvelope<T>
{
    /// <summary>
    /// Name of the Resource
    /// </summary>
    public Settable<string> Name { get; set; }

    /// <summary>
    /// Type of the Resource
    /// </summary>
    public Settable<string> Type { get; set; }

    /// <summary>
    /// Tags associated with resource.
    /// </summary>
    public Settable<List<string>> Tags { get; set; }

    /// <summary>
    /// Owner of the resource.
    /// </summary>
    public Settable<string> Owner { get; set; }

    /// <summary>
    /// Resource specific properties.
    /// </summary>
    public Settable<T> Properties { get; set; }


    /// <summary>
    /// Determines if <see cref="Tags"/> is the only property set on the envelope.
    /// </summary>
    /// <returns></returns>
    public bool TagsOnly()
    {
        return Tags.IsSet &&
            !Type.IsSet &&
            !Properties.IsSet;
    }
}
