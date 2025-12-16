// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E;

/// <summary>
/// xUnit collection definition for Agent CLI E2E tests.
/// All E2E tests share the same MockWebApplicationFactory instance and run sequentially.
/// Sequential execution is required because ApiService.SetHttpClientFactory() uses a static field.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class AgentCommandTestCollection : ICollectionFixture<MockWebApplicationFactory>
{
    public const string Name = "Agent Command Tests";

    // This class has no code, and is never instantiated.
    // Its purpose is simply to define the collection and fixture.
}
