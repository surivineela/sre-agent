// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Agent.Tests.Unit.Framework.Hooks;

public class HookManagerTests
{
    private readonly Mock<IHookExecutor> _mockExecutor;
    private readonly HookManager _hookManager;

    public HookManagerTests()
    {
        _mockExecutor = new Mock<IHookExecutor>();
        _mockExecutor.Setup(e => e.SupportedType).Returns(HookType.Prompt);

        var logger = NullLogger<HookManager>.Instance;
        _hookManager = new HookManager(new[] { _mockExecutor.Object }, logger);
    }

    [Fact]
    public async Task ExecuteHooksAsync_ReturnsSuccess_WhenNoHooksConfigured()
    {
        var config = new AgentHookConfiguration();
        var context = new StopHookContext();

        var result = await _hookManager.ExecuteHooksAsync(config, HookEventType.Stop, context);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ExecuteHooksAsync_ReturnsSuccess_WhenNullConfiguration()
    {
        var context = new StopHookContext();

        var result = await _hookManager.ExecuteHooksAsync(null, HookEventType.Stop, context);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ExecuteHooksAsync_ExecutesHook_WhenConfigured()
    {
        _mockExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HookResult.Success());

        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition> { new() { Type = HookType.Prompt, Prompt = "Test" } }
            }
        };
        var context = new StopHookContext();

        var result = await _hookManager.ExecuteHooksAsync(config, HookEventType.Stop, context);

        Assert.True(result.Ok);
        _mockExecutor.Verify(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteHooksAsync_ReturnsRejection_WhenHookRejects()
    {
        const string rejectionReason = "Tasks incomplete";
        _mockExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HookResult.Reject(rejectionReason));

        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition> { new() { Type = HookType.Prompt } }
            }
        };
        var context = new StopHookContext();

        var result = await _hookManager.ExecuteHooksAsync(config, HookEventType.Stop, context);

        Assert.False(result.Ok);
        Assert.Equal(rejectionReason, result.Reason);
    }

    [Fact]
    public async Task ExecuteHooksAsync_CombinesRejectionReasons_WhenMultipleHooksReject()
    {
        var callCount = 0;
        _mockExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return HookResult.Reject($"Reason {callCount}");
            });

        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition>
                {
                    new() { Type = HookType.Prompt },
                    new() { Type = HookType.Prompt }
                }
            }
        };
        var context = new StopHookContext();

        var result = await _hookManager.ExecuteHooksAsync(config, HookEventType.Stop, context);

        Assert.False(result.Ok);
        Assert.Contains("Reason 1", result.Reason);
        Assert.Contains("Reason 2", result.Reason);
    }

    [Fact]
    public void CreateFromDictionary_CreatesConfiguration()
    {
        var hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition> { new() { Prompt = "Test" } }
        };

        var config = HookManager.CreateFromDictionary(hooks);

        Assert.True(config.HasHooksForEvent(HookEventType.Stop));
        Assert.Equal("Test", config.GetHooksForEvent(HookEventType.Stop)[0].Prompt);
    }

    [Fact]
    public void CreateFromDictionary_HandlesNull()
    {
        var config = HookManager.CreateFromDictionary(null);

        Assert.Empty(config.Hooks);
    }

    [Fact]
    public async Task ExecuteHooksAsync_ReturnsSuccess_WhenHooksDisabled()
    {
        // Arrange: create hook manager with enabled=false
        var logger = NullLogger<HookManager>.Instance;
        var disabledHookManager = new HookManager(new[] { _mockExecutor.Object }, logger, enabled: false);

        var config = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] = new List<HookDefinition> { new() { Type = HookType.Prompt, Prompt = "Test" } }
            }
        };
        var context = new StopHookContext();

        // Act
        var result = await disabledHookManager.ExecuteHooksAsync(config, HookEventType.Stop, context);

        // Assert: should return success without executing hooks
        Assert.True(result.Ok);
        _mockExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<HookDefinition>(),
            It.IsAny<HookContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Enabled_ReturnsCorrectValue()
    {
        var logger = NullLogger<HookManager>.Instance;

        var enabledManager = new HookManager(Array.Empty<IHookExecutor>(), logger, enabled: true);
        var disabledManager = new HookManager(Array.Empty<IHookExecutor>(), logger, enabled: false);

        Assert.True(enabledManager.Enabled);
        Assert.False(disabledManager.Enabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(26)]
    [InlineData(100)]
    public void CreateFromDictionary_ThrowsOnInvalidMaxRejections(int invalidValue)
    {
        var hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new() { Prompt = "Test", MaxRejections = invalidValue }
            }
        };

        var exception = Assert.Throws<ArgumentException>(() => HookManager.CreateFromDictionary(hooks));
        Assert.Contains("MaxRejections", exception.Message);
        Assert.Contains(invalidValue.ToString(), exception.Message);
        Assert.Contains("1-25", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(25)]
    public void CreateFromDictionary_AcceptsValidMaxRejections(int validValue)
    {
        var hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new() { Prompt = "Test", MaxRejections = validValue }
            }
        };

        var config = HookManager.CreateFromDictionary(hooks);

        Assert.Equal(validValue, config.GetHooksForEvent(HookEventType.Stop)[0].MaxRejections);
    }

    [Fact]
    public void CreateFromDictionary_AcceptsNullMaxRejections()
    {
        var hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new() { Prompt = "Test", MaxRejections = null }
            }
        };

        var config = HookManager.CreateFromDictionary(hooks);

        Assert.Null(config.GetHooksForEvent(HookEventType.Stop)[0].MaxRejections);
    }
}
