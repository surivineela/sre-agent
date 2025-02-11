using Xunit;

namespace OperationalAgent.Tests.Integration
{
    [CollectionDefinition(nameof(CombinedTestCollection))]
    public class CombinedTestCollection : ICollectionFixture<CombinedFixture> { }
}