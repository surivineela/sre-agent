//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Web.ApiResources;


/// <summary>
/// Body of the error response returned from the API.
/// </summary>
public class ErrorEntity
{
    /// <summary>
    /// Inner Error entity, according to ARM RPC. 
    /// https://github.com/Azure/azure-resource-manager-rpc/blob/master/v1.0/common-api-details.md#error-response-content
    /// The minimum valid error response schema should have an error entity whilc contains code and message inside.
    /// </summary>
    public ErrorDetail Error { get; set; }

    //----below---To be depreciated, It's a common mistake to have the code and message properties at the top level of the error response

    /// <summary>
    /// Basic error code. For Log and Test, Json Ignore in response according to ARM RPC.
    /// </summary>
    [JsonIgnore]
    public string? Code
    {
        get
        {
            return Error?.Code;
        }
    }

    /// <summary>
    /// Any details of the error. For Log and Test, Json Ignore in response according to ARM RPC.
    /// </summary>
    [JsonIgnore]
    public string? Message
    {
        get
        {
            return Error?.Message;
        }
    }

    /// <summary>
    /// Error Details. For Log and Test, Json Ignore in response according to ARM RPC.
    /// </summary>
    [JsonIgnore]
    public IList<ErrorDetail>? Details
    {
        get
        {
            return Error?.Details;
        }
    }

    /// <summary>
    /// The error target. For Log and Test, Json Ignore in response according to ARM RPC.
    /// </summary>
    [JsonIgnore]
    public string? Target
    {
        get
        {
            return Error?.Target;
        }
    }


    /// <summary>
    /// The error otel traceId. For Log and Test, Json Ignore in response according to ARM RPC.
    /// </summary>
    [JsonIgnore]
    public string? TraceId
    {
        get
        {
            return Error?.TraceId;
        }
    }

    /// <summary>
    /// ToString() override.
    /// </summary>
    /// <returns>string</returns>
    public override string ToString()
    {
        return string.Format("Code: {0}, Message: {1}", Code, Message);
    }

    public ErrorEntity(ErrorDetail innerError)
    {
        Error = innerError;
    }

    public ErrorEntity(string message)
    {
        Error = new ErrorDetail(Code: null, Message: message);
    }

    public ErrorEntity(string code, string message)
    {
        Error = new ErrorDetail(Code: code, Message: message);
    }

    public ErrorEntity(string code, string message, List<ErrorDetail> details, string target)
    {
        Error = new ErrorDetail(Code: code, Message: message, Details: details, Target: target);
    }

    public ErrorEntity(string code, string message, List<ErrorDetail> details, string target, string traceId)
    {
        Error = new ErrorDetail(Code: code, Message: message, Details: details, Target: target, TraceId: traceId);
    }
}
