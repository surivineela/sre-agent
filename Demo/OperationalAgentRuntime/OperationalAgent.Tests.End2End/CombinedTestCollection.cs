using OperationalAgent.Tests.End2End.Fixtures;
using Xunit;

namespace E2ETests
{
    [CollectionDefinition(nameof(CombinedTestCollection))]
    public class CombinedTestCollection : ICollectionFixture<CombinedFixture> { }
}