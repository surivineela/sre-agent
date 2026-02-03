// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;

namespace Agent.Tests.Unit.Framework.Hooks;

public class HookModelsTests
{
    [Fact]
    public void HookResult_Success_ReturnsOkTrue()
    {
        var result = HookResult.Success();

        Assert.True(result.Ok);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void HookResult_Reject_ReturnsOkFalseWithReason()
    {
        const string reason = "Task incomplete";

        var result = HookResult.Reject(reason);

        Assert.False(result.Ok);
        Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public void HookResult_Error_ReturnsOkTrueWithErrorMessage()
    {
        const string error = "Connection failed";

        var result = HookResult.Error(error);

        Assert.True(result.Ok);
        Assert.Contains(error, result.Reason);
        Assert.Contains("Hook error:", result.Reason);
    }

    [Fact]
    public void HookDefinition_DefaultValues()
    {
        var definition = new HookDefinition();

        Assert.Equal(HookType.Prompt, definition.Type);
        Assert.Equal(30, definition.Timeout);
        Assert.Null(definition.Prompt);
        Assert.Null(definition.Model);
    }

    [Fact]
    public void StopHookContext_DefaultValues()
    {
        var context = new StopHookContext();

        Assert.Equal(HookEventType.Stop, context.HookEventName);
        Assert.False(context.StopHookActive);
        Assert.Equal(0, context.StopRejectionCount);
    }

    [Fact]
    public void AgentHookConfiguration_GetHooksForEvent_ReturnsEmptyListWhenNotConfigured()
    {
        var config = new AgentHookConfiguration();

        var hooks = config.GetHooksForEvent(HookEventType.Stop);

        Assert.Empty(hooks);
    }

    [Fact]
    public void AgentHookConfiguration_GetHooksForEvent_ReturnsConfiguredHooks()
    {
        var hookDef = new HookDefinition { Prompt = "Test prompt" };
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition> { hookDef }
            }
        };

        var hooks = config.GetHooksForEvent(HookEventType.Stop);

        Assert.Single(hooks);
        Assert.Equal("Test prompt", hooks[0].Prompt);
    }

    [Fact]
    public void AgentHookConfiguration_HasHooksForEvent_ReturnsFalseWhenNotConfigured()
    {
        var config = new AgentHookConfiguration();

        var hasHooks = config.HasHooksForEvent(HookEventType.Stop);

        Assert.False(hasHooks);
    }

    [Fact]
    public void AgentHookConfiguration_HasHooksForEvent_ReturnsTrueWhenConfigured()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition> { new() }
            }
        };

        var hasHooks = config.HasHooksForEvent(HookEventType.Stop);

        Assert.True(hasHooks);
    }

    [Fact]
    public void AgentHookConfiguration_Empty_ReturnsEmptyConfiguration()
    {
        var empty = AgentHookConfiguration.Empty;

        Assert.Empty(empty.Hooks);
        Assert.False(empty.HasHooksForEvent(HookEventType.Stop));
    }
}
