// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Framework.Hooks;

namespace Agent.Tests.Unit.Framework.Hooks;

public class YamlHookParsingTests
{
    [Fact]
    public void FromYaml_ParsesHooksSection()
    {
        var yaml = """
            name: test_agent
            system_prompt: You are a test agent.
            hooks:
              Stop:
                - type: prompt
                  prompt: "Check if all tasks are complete."
                  timeout: 30
            """;

        var descriptor = YamlAgentDescriptor.FromYaml(yaml);

        Assert.NotNull(descriptor.Hooks);
        Assert.True(descriptor.Hooks.ContainsKey("Stop"));
        Assert.Single(descriptor.Hooks["Stop"]);

        var hook = descriptor.Hooks["Stop"][0];
        Assert.Equal(HookType.Prompt, hook.Type);
        Assert.Equal("Check if all tasks are complete.", hook.Prompt);
        Assert.Equal(30, hook.Timeout);
    }

    [Fact]
    public void FromYaml_ParsesMultipleHooks()
    {
        var yaml = """
            name: test_agent
            system_prompt: You are a test agent.
            hooks:
              Stop:
                - type: prompt
                  prompt: "First hook"
                  timeout: 20
                - type: prompt
                  prompt: "Second hook"
                  timeout: 40
            """;

        var descriptor = YamlAgentDescriptor.FromYaml(yaml);

        Assert.NotNull(descriptor.Hooks);
        Assert.Equal(2, descriptor.Hooks["Stop"].Count);
        Assert.Equal("First hook", descriptor.Hooks["Stop"][0].Prompt);
        Assert.Equal("Second hook", descriptor.Hooks["Stop"][1].Prompt);
    }

    [Fact]
    public void FromYaml_ParsesHookWithModel()
    {
        var yaml = """
            name: test_agent
            system_prompt: You are a test agent.
            hooks:
              Stop:
                - type: prompt
                  prompt: "Evaluate completion"
                  model: gpt-4o-mini
                  timeout: 60
            """;

        var descriptor = YamlAgentDescriptor.FromYaml(yaml);

        var hook = descriptor.Hooks!["Stop"][0];
        Assert.Equal("gpt-4o-mini", hook.Model);
        Assert.Equal(60, hook.Timeout);
    }

    [Fact]
    public void FromYaml_HooksNullWhenNotSpecified()
    {
        var yaml = """
            name: test_agent
            system_prompt: You are a test agent.
            """;

        var descriptor = YamlAgentDescriptor.FromYaml(yaml);

        Assert.Null(descriptor.Hooks);
    }

    [Fact]
    public void FromYaml_ParsesMultilinePrompt()
    {
        var yaml = """
            name: test_agent
            system_prompt: You are a test agent.
            hooks:
              Stop:
                - type: prompt
                  prompt: |
                    Analyze if the agent should stop.
                    
                    Check:
                    1. All tasks complete
                    2. No errors
                  timeout: 30
            """;

        var descriptor = YamlAgentDescriptor.FromYaml(yaml);

        var hook = descriptor.Hooks!["Stop"][0];
        Assert.Contains("Analyze if the agent should stop.", hook.Prompt);
        Assert.Contains("All tasks complete", hook.Prompt);
        Assert.Contains("No errors", hook.Prompt);
    }

    [Fact]
    public void HookConfiguration_CanBeCreatedFromDescriptor()
    {
        var yaml = """
            name: test_agent
            system_prompt: You are a test agent.
            hooks:
              Stop:
                - type: prompt
                  prompt: "Test hook"
            """;

        var descriptor = YamlAgentDescriptor.FromYaml(yaml);
        var config = HookManager.CreateFromDictionary(descriptor.Hooks);

        Assert.True(config.HasHooksForEvent(HookEventType.Stop));
        Assert.Equal("Test hook", config.GetHooksForEvent(HookEventType.Stop)[0].Prompt);
    }
}
