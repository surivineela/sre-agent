using Xunit;

namespace E2ETests
{
    [CollectionDefinition(nameof(AzureFunctionsTestsCollection))]
    public class AzureFunctionsTestsCollection : ICollectionFixture<TestFixture> { }
}