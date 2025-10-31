// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.ApiResources;

public class ApiCommandResult<T>
{
    public ApiCommandResult(T response)
    {
        Response = response ?? throw new ArgumentNullException(nameof(response));
    }

    public ApiCommandResult(IActionResult error)
    {
        ActionResult = error ?? throw new ArgumentNullException(nameof(error));
    }

    public ApiCommandResult(T response, string operationId)
    {
        Response = response ?? throw new ArgumentNullException(nameof(response));
        AsyncOperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
    }

    /// <summary>
    /// Only used when the response want to return both location and Azure-AsyncOperation. For example: Patch Container App.
    /// </summary>
    /// <param name="operationId"> At this time, the operationId is for location header</param>
    public ApiCommandResult(string operationId)
    {
        AsyncOperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
    }

    public T? Response { get; private set; }

    public IActionResult? ActionResult { get; private set; }

    public string? AsyncOperationId { get; private set; }

    [MemberNotNullWhen(true, nameof(AsyncOperationId))]
    [MemberNotNullWhen(true, nameof(Response))]
    public bool IsAsyncCreated
    {
        get
        {
            return !string.IsNullOrEmpty(AsyncOperationId) && Response != null;
        }
    }

    [MemberNotNullWhen(true, nameof(AsyncOperationId))]
    public bool IsAsync
    {
        get
        {
            return !string.IsNullOrEmpty(AsyncOperationId);
        }
    }

    [MemberNotNullWhen(true, nameof(Response))]
    public bool IsSyncObjectResult
    {
        get
        {
            return string.IsNullOrEmpty(AsyncOperationId) && Response != null;
        }
    }

    [MemberNotNullWhen(true, nameof(ActionResult))]
    public bool IsStatusCodeResult
    {
        get
        {
            return ActionResult != null;
        }
    }
}
