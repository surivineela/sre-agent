// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Xunit;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// Collection definition to disable parallelization for Tool tests.
/// This is necessary because tests share Console.Out/Error streams.
/// </summary>
[CollectionDefinition("ToolTests", DisableParallelization = true)]
public class ToolTestCollection
{
}
