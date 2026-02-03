// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;

namespace Agent.Tests.Unit.Framework.Hooks;

/// <summary>
/// Tests for AgentHookConfiguration matcher functionality.
/// Verifies that hooks are correctly filtered by tool name patterns.
/// </summary>
public class AgentHookConfigurationMatcherTests
{
    [Fact]
    public void GetMatchingHooksForTool_ReturnsEmpty_WhenMatcherIsEmpty()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check output",
                        Matcher = "" // Empty matcher does NOT match any tools
                    }
                ]
            }
        };

        var matchingHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "AnyToolName");

        Assert.Empty(matchingHooks);
    }

    [Fact]
    public void GetMatchingHooksForTool_ReturnsEmpty_WhenMatcherIsNull()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check output",
                        Matcher = null // Null matcher does NOT match any tools
                    }
                ]
            }
        };

        var matchingHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "AnyToolName");

        Assert.Empty(matchingHooks);
    }

    [Fact]
    public void GetMatchingHooksForTool_ReturnsAllHooks_WhenMatcherIsWildcard()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check output",
                        Matcher = "*" // Wildcard matcher
                    }
                ]
            }
        };

        var matchingHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "AnyToolName");

        Assert.Single(matchingHooks);
    }

    [Fact]
    public void GetMatchingHooksForTool_ReturnsHook_WhenExactNameMatches()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check Edit output",
                        Matcher = "Edit"
                    }
                ]
            }
        };

        var matchingHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Edit");

        Assert.Single(matchingHooks);
        Assert.Equal("Check Edit output", matchingHooks[0].Prompt);
    }

    [Fact]
    public void GetMatchingHooksForTool_ReturnsEmpty_WhenExactNameDoesNotMatch()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check Edit output",
                        Matcher = "Edit"
                    }
                ]
            }
        };

        var matchingHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "View");

        Assert.Empty(matchingHooks);
    }

    [Fact]
    public void GetMatchingHooksForTool_ReturnsHook_WhenRegexPatternMatches()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check write operations",
                        Matcher = "Edit|Write|Create"
                    }
                ]
            }
        };

        // Test each option in the regex alternation
        Assert.Single(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Edit"));
        Assert.Single(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Write"));
        Assert.Single(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Create"));
        Assert.Empty(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "View"));
        Assert.Empty(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Delete"));
    }

    [Fact]
    public void GetMatchingHooksForTool_MatchesFullToolName_NotPartial()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check output",
                        Matcher = "Edit"
                    }
                ]
            }
        };

        // Should not match partial tool names
        Assert.Empty(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "EditFile"));
        Assert.Empty(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "QuickEdit"));

        // Should match exact name
        Assert.Single(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Edit"));
    }

    [Fact]
    public void GetMatchingHooksForTool_FiltersMultipleHooks_Independently()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check edit operations",
                        Matcher = "Edit"
                    },
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check view operations",
                        Matcher = "View"
                    },
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check all operations",
                        Matcher = "*"
                    }
                ]
            }
        };

        // "Edit" matches first and third hooks
        var editHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Edit");
        Assert.Equal(2, editHooks.Count);
        Assert.Contains(editHooks, h => h.Prompt == "Check edit operations");
        Assert.Contains(editHooks, h => h.Prompt == "Check all operations");

        // "View" matches second and third hooks
        var viewHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "View");
        Assert.Equal(2, viewHooks.Count);
        Assert.Contains(viewHooks, h => h.Prompt == "Check view operations");
        Assert.Contains(viewHooks, h => h.Prompt == "Check all operations");

        // "Delete" matches only the wildcard hook
        var deleteHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Delete");
        Assert.Single(deleteHooks);
        Assert.Equal("Check all operations", deleteHooks[0].Prompt);
    }

    [Fact]
    public void GetMatchingHooksForTool_ReturnsEmpty_WhenNoHooksForEvent()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check if done"
                    }
                ]
            }
        };

        var matchingHooks = config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Edit");

        Assert.Empty(matchingHooks);
    }

    [Fact]
    public void GetMatchingHooksForTool_HandlesRegexWithSpecialCharacters()
    {
        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check output",
                        Matcher = "Tool.*" // Regex with dot-star
                    }
                ]
            }
        };

        Assert.Single(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "Tool123"));
        Assert.Single(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "ToolABC"));
        Assert.Empty(config.GetMatchingHooksForTool(HookEventType.PostToolUse, "MyTool"));
    }
}
