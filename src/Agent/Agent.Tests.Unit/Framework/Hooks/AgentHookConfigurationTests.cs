// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;

namespace Agent.Tests.Unit.Framework.Hooks;

public class AgentHookConfigurationTests
{
    #region GetPromptStopHooks Tests

    [Fact]
    public void GetPromptStopHooks_ReturnsEmpty_WhenNoStopHooksConfigured()
    {
        var config = new AgentHookConfiguration();

        var result = config.GetPromptStopHooks();

        Assert.Empty(result);
    }

    [Fact]
    public void GetPromptStopHooks_ReturnsOnlyPromptHooks()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Prompt 1" },
                    new() { Type = HookType.Command, Command = "echo test" },
                    new() { Type = HookType.Prompt, Prompt = "Prompt 2" }
                }
            }
        };

        var result = config.GetPromptStopHooks();

        Assert.Equal(2, result.Count);
        Assert.All(result, h => Assert.Equal(HookType.Prompt, h.Type));
        Assert.Contains(result, h => h.Prompt == "Prompt 1");
        Assert.Contains(result, h => h.Prompt == "Prompt 2");
    }

    [Fact]
    public void GetPromptStopHooks_ReturnsEmpty_WhenOnlyCommandHooksExist()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Command, Command = "echo 1" },
                    new() { Type = HookType.Command, Script = "#!/bin/bash" }
                }
            }
        };

        var result = config.GetPromptStopHooks();

        Assert.Empty(result);
    }

    #endregion

    #region GetCommandStopHooks Tests

    [Fact]
    public void GetCommandStopHooks_ReturnsEmpty_WhenNoStopHooksConfigured()
    {
        var config = new AgentHookConfiguration();

        var result = config.GetCommandStopHooks();

        Assert.Empty(result);
    }

    [Fact]
    public void GetCommandStopHooks_ReturnsOnlyCommandHooks()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Prompt 1" },
                    new() { Type = HookType.Command, Command = "echo test" },
                    new() { Type = HookType.Command, Script = "#!/bin/bash" }
                }
            }
        };

        var result = config.GetCommandStopHooks();

        Assert.Equal(2, result.Count);
        Assert.All(result, h => Assert.Equal(HookType.Command, h.Type));
    }

    [Fact]
    public void GetCommandStopHooks_ReturnsEmpty_WhenOnlyPromptHooksExist()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Prompt 1" },
                    new() { Type = HookType.Prompt, Prompt = "Prompt 2" }
                }
            }
        };

        var result = config.GetCommandStopHooks();

        Assert.Empty(result);
    }

    #endregion

    #region HasPromptBasedStopHooks Tests

    [Fact]
    public void HasPromptBasedStopHooks_ReturnsFalse_WhenNoStopHooksConfigured()
    {
        var config = new AgentHookConfiguration();

        var result = config.HasPromptBasedStopHooks();

        Assert.False(result);
    }

    [Fact]
    public void HasPromptBasedStopHooks_ReturnsTrue_WhenPromptHooksExist()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Test" }
                }
            }
        };

        var result = config.HasPromptBasedStopHooks();

        Assert.True(result);
    }

    [Fact]
    public void HasPromptBasedStopHooks_ReturnsFalse_WhenOnlyCommandHooksExist()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Command, Command = "echo test" }
                }
            }
        };

        var result = config.HasPromptBasedStopHooks();

        Assert.False(result);
    }

    #endregion

    #region HasCommandBasedStopHooks Tests

    [Fact]
    public void HasCommandBasedStopHooks_ReturnsFalse_WhenNoStopHooksConfigured()
    {
        var config = new AgentHookConfiguration();

        var result = config.HasCommandBasedStopHooks();

        Assert.False(result);
    }

    [Fact]
    public void HasCommandBasedStopHooks_ReturnsTrue_WhenCommandHooksExist()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Command, Command = "echo test" }
                }
            }
        };

        var result = config.HasCommandBasedStopHooks();

        Assert.True(result);
    }

    [Fact]
    public void HasCommandBasedStopHooks_ReturnsFalse_WhenOnlyPromptHooksExist()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Test" }
                }
            }
        };

        var result = config.HasCommandBasedStopHooks();

        Assert.False(result);
    }

    #endregion

    #region GetMaxStopHookRejections Tests

    [Fact]
    public void GetMaxStopHookRejections_ReturnsNull_WhenNoStopHooksConfigured()
    {
        var config = new AgentHookConfiguration();

        var result = config.GetMaxStopHookRejections();

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxStopHookRejections_ReturnsNull_WhenNoPromptHooksSpecifyMaxRejections()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Test 1", MaxRejections = null },
                    new() { Type = HookType.Prompt, Prompt = "Test 2", MaxRejections = null }
                }
            }
        };

        var result = config.GetMaxStopHookRejections();

        Assert.Null(result);
    }

    [Fact]
    public void GetMaxStopHookRejections_ReturnsValue_WhenSinglePromptHookSpecifiesMaxRejections()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Test", MaxRejections = 10 }
                }
            }
        };

        var result = config.GetMaxStopHookRejections();

        Assert.Equal(10, result);
    }

    [Fact]
    public void GetMaxStopHookRejections_ReturnsMaxValue_WhenMultiplePromptHooksSpecifyDifferentValues()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Test 1", MaxRejections = 5 },
                    new() { Type = HookType.Prompt, Prompt = "Test 2", MaxRejections = 15 },
                    new() { Type = HookType.Prompt, Prompt = "Test 3", MaxRejections = 8 }
                }
            }
        };

        var result = config.GetMaxStopHookRejections();

        Assert.Equal(15, result);
    }

    [Fact]
    public void GetMaxStopHookRejections_IgnoresNullValues_WhenSomePromptHooksSpecifyMaxRejections()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Test 1", MaxRejections = null },
                    new() { Type = HookType.Prompt, Prompt = "Test 2", MaxRejections = 12 },
                    new() { Type = HookType.Prompt, Prompt = "Test 3", MaxRejections = null }
                }
            }
        };

        var result = config.GetMaxStopHookRejections();

        Assert.Equal(12, result);
    }

    [Fact]
    public void GetMaxStopHookRejections_IgnoresOtherHookTypes()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt, Prompt = "Tool hook", MaxRejections = 20 }
                }
            }
        };

        var result = config.GetMaxStopHookRejections();

        // No Stop hooks, so should return null even though PostToolUse has MaxRejections
        Assert.Null(result);
    }

    [Fact]
    public void GetMaxStopHookRejections_IgnoresCommandTypeStopHooks()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Command, Command = "echo test", MaxRejections = 20 }
                }
            }
        };

        var result = config.GetMaxStopHookRejections();

        // No prompt Stop hooks, so should return null even though command hook has MaxRejections
        Assert.Null(result);
    }

    [Fact]
    public void GetMaxStopHookRejections_OnlyConsidersPromptHooks_InMixedConfiguration()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Command, Command = "echo", MaxRejections = 25 }, // Should be ignored
                    new() { Type = HookType.Prompt, Prompt = "Test 1", MaxRejections = 5 },
                    new() { Type = HookType.Command, Script = "#!/bin/bash", MaxRejections = 20 }, // Should be ignored
                    new() { Type = HookType.Prompt, Prompt = "Test 2", MaxRejections = 10 }
                }
            }
        };

        var result = config.GetMaxStopHookRejections();

        // Should only consider prompt hooks: max(5, 10) = 10
        Assert.Equal(10, result);
    }

    [Fact]
    public void GetMaxStopHookRejections_ReturnsNull_WhenOnlyCommandHooksHaveMaxRejections()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Command, Command = "echo", MaxRejections = 15 },
                    new() { Type = HookType.Prompt, Prompt = "Test", MaxRejections = null } // No value
                }
            }
        };

        var result = config.GetMaxStopHookRejections();

        // Only prompt hooks are considered, and it has null MaxRejections
        Assert.Null(result);
    }

    #endregion
}
