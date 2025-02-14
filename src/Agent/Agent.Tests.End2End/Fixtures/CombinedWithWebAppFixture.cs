using Agent.Tests.Common;
using Xunit.Abstractions;

namespace Agent.Tests.End2End.Fixtures
{
    /// <summary>
    /// 
    /// </summary>
    public class CombinedWithWebAppFixture : IDisposable
    {
        public ConfigFixture ConfigFixture { get; }
        public ChatClientFixture ChatClientFixture { get; }
        public AzureFunctionsFixture AzureFunctionsFixture { get; }
        public WebAppFixture WebAppFixture { get; }

        public CombinedWithWebAppFixture(IMessageSink sink)
        {
            ConfigFixture = new ConfigFixture();
            ChatClientFixture = new ChatClientFixture(ConfigFixture.AzureSettings);
            AzureFunctionsFixture = new AzureFunctionsFixture(sink);
            WebAppFixture = new WebAppFixture(sink);
        }

        public void Dispose()
        {
            AzureFunctionsFixture.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}