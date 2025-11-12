//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Agent.Web.ApiResources;


/// <summary>
/// Error entity in error response
/// </summary>
public sealed record ErrorDetail(
    string? Code,
    string? Message,
    IList<ErrorDetail>? Details = null,
    string? Target = null,
    IList<ErrorAdditionalInfo>? AdditionalInfo = null,
    string? TraceId = null
)
{
    /// <summary>
    /// ToString() override.
    /// </summary>
    /// <returns>string</returns>
    public override string ToString()
    {
        return string.Format("Code: {0}, Message: {1}", Code, Message);
    }
}
