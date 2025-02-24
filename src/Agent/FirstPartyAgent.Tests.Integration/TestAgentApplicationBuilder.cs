// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Xunit.Abstractions;

namespace FirstPartyAgent.Tests.Integration
{
    public class TestAgentApplicationBuilder
    {
        public TestAgentApplication Build()
        {
            return new TestAgentApplication(this);
        }

        public bool BackgroundTaskEnabled { get; private set; } = true;

        public ITestOutputHelper? TestOutputHelper { get; private set; } = null;

        public TestAgentApplicationBuilder AddLogger(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
            return this;
        }

        public TestAgentApplicationBuilder DisableBackgroundTask()
        {
            BackgroundTaskEnabled = false;
            return this;
        }
    }
}
