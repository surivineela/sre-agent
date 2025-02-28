using Agent.Tests.Common;
using Agent.Tests.Common.Fixtures;
using Xunit.Abstractions;

namespace Agent.Tests.End2End.Fixtures
{
    /// <summary>
    /// 
    /// </summary>
    public class CombinedFixture : IDisposable
    {
        public ConfigFixture ConfigFixture { get; }
        public TestChatClientFixture TestChatClientFixture { get; }
        public AzureFunctionsFixture AzureFunctionsFixture { get; }

        public CombinedFixture(IMessageSink sink)
        {
            ConfigFixture = new ConfigFixture();
            TestChatClientFixture = new TestChatClientFixture(ConfigFixture.AzureSettings.OpenAI);
            AzureFunctionsFixture = new AzureFunctionsFixture(sink);
        }

        public void Dispose()
        {
            AzureFunctionsFixture.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}