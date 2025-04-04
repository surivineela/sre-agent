// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Tests.End2End.Fixtures;
using Xunit;

namespace E2ETests
{
    [CollectionDefinition(nameof(CombinedTestCollection))]
    public class CombinedTestCollection : ICollectionFixture<CombinedFixture> { }

    [CollectionDefinition(nameof(CombinedWithWebAppTestCollection))]
    public class CombinedWithWebAppTestCollection : ICollectionFixture<CombinedWithWebAppFixture> { }
}
