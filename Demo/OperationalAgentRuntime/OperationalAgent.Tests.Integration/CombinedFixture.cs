using OperationAgent.Tests.Common;
using Xunit.Abstractions;

namespace OperationalAgent.Tests.Integration
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