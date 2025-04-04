// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Tests.Integration.Fixtures;

namespace Agent.Tests.Integration
{
    [CollectionDefinition(nameof(CombinedTestCollection))]
    public class CombinedTestCollection : ICollectionFixture<CombinedFixture> { }
}
