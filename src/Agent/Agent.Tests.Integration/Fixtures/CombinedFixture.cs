using Agent.Tests.Common;
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

        public CombinedFixture(IMessageSink sink)
        {
            ConfigFixture = new ConfigFixture();
            EmbeddingGeneratorFixture = new EmbeddingGeneratorFixture(ConfigFixture.AzureSettings);
        }
    }
}