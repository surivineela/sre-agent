namespace FirstPartyAgent.Tests.Integration
{
    public class TestAgentApplicationBuilder
    {
        public TestAgentApplication Build()
        {
            return new TestAgentApplication(this);
        }

        public bool BackgroundTaskEnabled { get; private set; } = true;

        public TestAgentApplicationBuilder DisableBackgroundTask()
        {
            BackgroundTaskEnabled = false;
            return this;
        }
    }
}
