// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Framework;

public class PromptTemplateResolverTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Dictionary<string, IPromptDescriptor> _promptDescriptors;
    private readonly PromptTemplateResolver _resolver;

    public PromptTemplateResolverTests()
    {
        _mockLogger = new Mock<ILogger>();
        _promptDescriptors = new Dictionary<string, IPromptDescriptor>(StringComparer.OrdinalIgnoreCase);
        _resolver = new PromptTemplateResolver(_promptDescriptors, _mockLogger.Object);
    }

    private void AddPrompt(string name, string content)
    {
        var descriptor = new YamlPromptDescriptor { Name = name, Prompt = content };
        _promptDescriptors[name] = descriptor;
    }

    [Fact]
    public void ResolveTemplates_NoTemplates_ReturnsOriginalText()
    {
        var text = "This is plain text without templates.";
        var result = _resolver.ResolveTemplates(text);

        Assert.Equal(text, result);
    }

    [Fact]
    public void ResolveTemplates_SingleTemplate_Substitutes()
    {
        AddPrompt("test_prompt", "Test content");
        var text = "Start {{test_prompt}} End";

        var result = _resolver.ResolveTemplates(text);

        Assert.Equal("Start Test content End", result);

        // Verify logging of resolved template
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("1") && v.ToString()!.Contains("test_prompt")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTemplates_MultipleTemplates_SubstitutesAll()
    {
        AddPrompt("prompt1", "Content 1");
        AddPrompt("prompt2", "Content 2");
        var text = "{{prompt1}} middle {{prompt2}}";

        var result = _resolver.ResolveTemplates(text);

        Assert.Equal("Content 1 middle Content 2", result);

        // Verify logging of resolved templates
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("2") && v.ToString()!.Contains("prompt1") && v.ToString()!.Contains("prompt2")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTemplates_CaseInsensitive_Matches()
    {
        AddPrompt("test_prompt", "Content");

        var text1 = "{{test_prompt}}";
        var text2 = "{{Test_Prompt}}";
        var text3 = "{{TEST_PROMPT}}";

        var result1 = _resolver.ResolveTemplates(text1);
        var result2 = _resolver.ResolveTemplates(text2);
        var result3 = _resolver.ResolveTemplates(text3);

        Assert.Equal("Content", result1);
        Assert.Equal("Content", result2);
        Assert.Equal("Content", result3);
    }

    [Fact]
    public void ResolveTemplates_UndefinedTemplate_KeepsPlaceholder()
    {
        var text = "Start {{undefined_template}} End";

        var result = _resolver.ResolveTemplates(text);

        Assert.Equal(text, result);
    }

    [Fact]
    public void ResolveTemplates_UndefinedTemplate_LogsWarning()
    {
        var text = "{{undefined_template}}";

        _resolver.ResolveTemplates(text);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("placeholder")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTemplates_NestedTemplates_ResolvesRecursively()
    {
        AddPrompt("inner", "Inner Content");
        AddPrompt("outer", "Outer {{inner}} Content");
        var text = "{{outer}}";

        var result = _resolver.ResolveTemplates(text);

        Assert.Equal("Outer Inner Content Content", result);

        // Verify logging of resolved templates (should be 2: outer and inner)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("2") && v.ToString()!.Contains("outer") && v.ToString()!.Contains("inner")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTemplates_CircularDependency_ReturnsOriginalText()
    {
        AddPrompt("prompt1", "Content {{prompt2}}");
        AddPrompt("prompt2", "Content {{prompt1}}");
        var text = "{{prompt1}}";

        var result = _resolver.ResolveTemplates(text);

        // Should return original text and log error
        Assert.Equal(text, result);

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to resolve templates")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTemplates_SelfReference_ReturnsOriginalText()
    {
        AddPrompt("recursive", "Content {{recursive}}");
        var text = "{{recursive}}";

        var result = _resolver.ResolveTemplates(text);

        // Should return original text and log error
        Assert.Equal(text, result);

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to resolve templates")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTemplates_EmptyText_ReturnsEmpty()
    {
        var result = _resolver.ResolveTemplates("");

        Assert.Equal("", result);
    }

    [Fact]
    public void ResolveTemplates_NullText_ReturnsNull()
    {
        var result = _resolver.ResolveTemplates(null!);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveTemplates_TemplateWithUnderscores_Resolves()
    {
        AddPrompt("test_prompt_123", "Content");
        var text = "{{test_prompt_123}}";

        var result = _resolver.ResolveTemplates(text);

        Assert.Equal("Content", result);
    }

    [Fact]
    public void ResolveTemplates_MultilineContent_Preserves()
    {
        var multilineContent = "Line 1\nLine 2\nLine 3";
        AddPrompt("multiline", multilineContent);
        var text = "Start\n{{multiline}}\nEnd";

        var result = _resolver.ResolveTemplates(text);

        Assert.Contains(multilineContent, result);
    }

    [Fact]
    public void ResolveTemplates_ComplexNestedScenario_ResolvesCorrectly()
    {
        AddPrompt("base", "Base content");
        AddPrompt("with_base", "Start {{base}} End");
        AddPrompt("complex", "Complex {{with_base}} More");

        var text = "Outer {{complex}} Final";

        var result = _resolver.ResolveTemplates(text);

        Assert.Equal("Outer Complex Start Base content End More Final", result);

        // Verify logging of 3 resolved templates
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTemplates_SameTemplateMultipleTimes_ResolvesAll()
    {
        AddPrompt("repeat", "Content");
        var text = "{{repeat}} and {{repeat}} and {{repeat}}";

        var result = _resolver.ResolveTemplates(text);

        Assert.Equal("Content and Content and Content", result);

        // Verify logging of the repeated template (counted once)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("repeat")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTemplates_TemplateWithSpecialCharactersInContent_Preserves()
    {
        AddPrompt("special", "Content with $pecial ch@rs & symbols!");
        var text = "{{special}}";

        var result = _resolver.ResolveTemplates(text);

        Assert.Equal("Content with $pecial ch@rs & symbols!", result);
    }
}
