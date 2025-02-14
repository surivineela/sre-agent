using Agent.Tests.Integration.Fixtures;
using Xunit;

namespace Agent.Tests.Integration
{
    [CollectionDefinition(nameof(CombinedTestCollection))]
    public class CombinedTestCollection : ICollectionFixture<CombinedFixture> { }
}