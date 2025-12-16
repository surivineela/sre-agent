// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Helpers;
using Xunit;

namespace Agent.Cli.UnitTests.Validations;

public class ValidationHelperTests
{
    #region ValidateResourceName - Valid Names

    [Theory]
    [InlineData("valid-name")]
    [InlineData("valid_name")]
    [InlineData("ValidName")]
    [InlineData("valid123")]
    [InlineData("123valid")]
    [InlineData("a")]
    [InlineData("A")]
    [InlineData("0")]
    [InlineData("my-tool-name")]
    [InlineData("my_tool_name")]
    [InlineData("MyToolName")]
    [InlineData("tool-123")]
    [InlineData("tool_123")]
    [InlineData("UPPERCASE")]
    [InlineData("lowercase")]
    [InlineData("MixedCase")]
    [InlineData("name-with-many-hyphens")]
    [InlineData("name_with_many_underscores")]
    [InlineData("name123with456numbers")]
    public void ValidateResourceName_WithValidName_ReturnsTrue(string name)
    {
        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name);

        // Assert
        Assert.True(isValid, $"Name '{name}' should be valid");
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateResourceName_WithMaxLengthName_ReturnsTrue()
    {
        // Arrange - exactly 128 characters
        var name = new string('a', 128);

        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    #endregion

    #region ValidateResourceName - Invalid Names

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void ValidateResourceName_WithNullOrWhitespace_ReturnsFalse(string? name)
    {
        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "tool");

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("Tool name must not be empty", errorMessage);
    }

    [Fact]
    public void ValidateResourceName_WithTooLongName_ReturnsFalse()
    {
        // Arrange - 129 characters (exceeds max of 128)
        var name = new string('a', 129);

        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "tool");

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("Tool name must be less than 128 characters", errorMessage);
        Assert.Contains("Current length: 129", errorMessage);
    }

    [Theory]
    [InlineData("name with spaces")]
    [InlineData("name@with!special")]
    [InlineData("name.with.dots")]
    [InlineData("name/with/slashes")]
    [InlineData("name\\with\\backslashes")]
    [InlineData("name:with:colons")]
    [InlineData("name;with;semicolons")]
    [InlineData("name,with,commas")]
    [InlineData("name<with>brackets")]
    [InlineData("name[with]brackets")]
    [InlineData("name{with}braces")]
    [InlineData("name|with|pipes")]
    [InlineData("name?with?questions")]
    [InlineData("name*with*asterisks")]
    [InlineData("name+with+plus")]
    [InlineData("name=with=equals")]
    [InlineData("name&with&ampersands")]
    [InlineData("name$with$dollars")]
    [InlineData("name#with#hashes")]
    [InlineData("name%with%percents")]
    [InlineData("name^with^carets")]
    [InlineData("name~with~tildes")]
    [InlineData("name`with`backticks")]
    [InlineData("name'with'quotes")]
    [InlineData("name\"with\"doublequotes")]
    public void ValidateResourceName_WithInvalidCharacters_ReturnsFalse(string name)
    {
        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "tool");

        // Assert
        Assert.False(isValid, $"Name '{name}' should be invalid");
        Assert.NotNull(errorMessage);
        Assert.Contains("Tool name must only contain letters (a-z, A-Z), numbers (0-9), underscores (_), and hyphens (-)", errorMessage);
    }

    #endregion

    #region ValidateResourceName - Custom Resource Types

    [Fact]
    public void ValidateResourceName_WithCustomResourceType_UsesCorrectTypeInErrorMessage()
    {
        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName("", "agent");

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("Agent name must not be empty", errorMessage);
    }

    [Fact]
    public void ValidateResourceName_WithCustomResourceType_CapitalizesFirstLetter()
    {
        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName("invalid name", "prompt");

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("Prompt name must only contain", errorMessage);
    }

    [Fact]
    public void ValidateResourceName_WithDefaultResourceType_UsesGenericMessage()
    {
        // Act - no resource type specified
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName("");

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("Resource name must not be empty", errorMessage);
    }

    #endregion

    #region ValidateResourceName - Edge Cases

    [Theory]
    [InlineData("-")]
    [InlineData("_")]
    [InlineData("-_")]
    [InlineData("_-")]
    [InlineData("---")]
    [InlineData("___")]
    [InlineData("-name")]
    [InlineData("_name")]
    [InlineData("name-")]
    [InlineData("name_")]
    public void ValidateResourceName_WithOnlyHyphensOrUnderscores_ReturnsTrue(string name)
    {
        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name);

        // Assert
        Assert.True(isValid, $"Name '{name}' should be valid");
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateResourceName_WithMixedCaseAndSpecialAllowedChars_ReturnsTrue()
    {
        // Arrange
        var name = "My-Tool_Name123";

        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateResourceName_WithUnicodeCharacters_ReturnsFalse()
    {
        // Arrange
        var name = "name-with-unicode-émojis-🚀";

        // Act
        var (isValid, errorMessage) = ValidationHelper.ValidateResourceName(name, "tool");

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("Tool name must only contain letters (a-z, A-Z), numbers (0-9), underscores (_), and hyphens (-)", errorMessage);
    }

    #endregion
}
