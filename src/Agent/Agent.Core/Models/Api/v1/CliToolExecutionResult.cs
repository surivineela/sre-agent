// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;

namespace Agent.Core.Models.Api.v1;

// Common output type for CLI tool executions, e.g. AzCli, Kubectl, Psql, etc.
public record CliToolExecutionResult(CliExecutionResult CliExecutionResult, Guid? ExecutionId);
