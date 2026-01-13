// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;

namespace Agent.Core.Models;

public class CodeExecuteRequest
{
    public string Code { get; set; } = string.Empty;
    public int TimeoutInSeconds { get; set; }
    public bool EnableEgress { get; set; } = true;
    public string ExecutionType { get; set; } = "synchronous"; // synchronous for now
    public int? StandardMsgLength { get; set; }
}

public class CodeExecutionResponse
{
    public int? Hresult { get; set; }
    public string? Status { get; set; }
    public string? Result { get; set; }
    public string? ErrorName { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public CodeExecutionDiagnosticInfo? DiagnosticInfo { get; set; }
    public string? OperationId { get; set; }
}

public class CodeExecutionDiagnosticInfo
{
    public int? ExecutionRequestTimeInMilliSeconds { get; set; }
    public int? ExecutionProcessResponseTimeInMilliSeconds { get; set; }
    public int? ExecutionDuration { get; set; }
    public string? Identifier { get; set; }
}
