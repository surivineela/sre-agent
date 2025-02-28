using Agent.Tests.Common;
using Agent.Tests.Common.Fixtures;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.Fixtures
{
    /// <summary>
    /// 
    /// </summary>
    public class CombinedFixture
    {
        public ConfigFixture ConfigFixture { get; }
        public EmbeddingGeneratorFixture EmbeddingGeneratorFixture { get; }
        public TestChatClientFixture TestChatClientFixture { get; }

        public CombinedFixture(IMessageSink sink)
        {
            ConfigFixture = new ConfigFixture();
            EmbeddingGeneratorFixture = new EmbeddingGeneratorFixture(ConfigFixture.AzureSettings.OpenAI);
            TestChatClientFixture = new TestChatClientFixture(ConfigFixture.AzureSettings.OpenAI);
        }
    }
}