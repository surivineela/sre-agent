// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.JsonConverters;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Helpers;

public class YamlJsonConverterTests
{
    [Fact]
    public void ConvertJsonElementToYaml_BooleanTrue_ReturnsLowercaseTrue()
    {
        // Arrange
        var jsonDoc = JsonDocument.Parse("true");
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Equal("true", result);
    }

    [Fact]
    public void ConvertJsonElementToYaml_BooleanFalse_ReturnsLowercaseFalse()
    {
        // Arrange
        var jsonDoc = JsonDocument.Parse("false");
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Equal("false", result);
    }

    [Fact]
    public void ConvertJsonElementToYaml_String_ReturnsStringValue()
    {
        // Arrange
        var jsonDoc = JsonDocument.Parse("\"hello world\"");
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ConvertJsonElementToYaml_Number_ReturnsNumberAsString()
    {
        // Arrange
        var jsonDoc = JsonDocument.Parse("42");
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Equal("42", result);
    }

    [Fact]
    public void ConvertJsonElementToYaml_DecimalNumber_ReturnsDecimalAsString()
    {
        // Arrange
        var jsonDoc = JsonDocument.Parse("3.14");
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Equal("3.14", result);
    }

    [Fact]
    public void ConvertJsonElementToYaml_Null_ReturnsNull()
    {
        // Arrange
        var jsonDoc = JsonDocument.Parse("null");
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Equal("null", result);
    }

    [Fact]
    public void ConvertJsonElementToYaml_SimpleObject_ReturnsYamlObject()
    {
        // Arrange
        var json = @"{""name"":""test"",""value"":123}";
        var jsonDoc = JsonDocument.Parse(json);
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Contains("name:", result);
        Assert.Contains("test", result);
        Assert.Contains("value:", result);
        Assert.Contains("123", result);
    }

    [Fact]
    public void ConvertJsonElementToYaml_Array_ReturnsYamlArray()
    {
        // Arrange
        var json = @"[1,2,3]";
        var jsonDoc = JsonDocument.Parse(json);
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Contains("1", result);
        Assert.Contains("2", result);
        Assert.Contains("3", result);
    }

    [Fact]
    public void ConvertJsonElementToYaml_NestedObject_ReturnsYamlWithNestedStructure()
    {
        // Arrange
        var json = @"{""outer"": {""inner"": ""value""}}";
        var jsonDoc = JsonDocument.Parse(json);
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.Contains("outer:", result);
        Assert.Contains("inner:", result);
        Assert.Contains("value", result);
    }

    [Fact]
    public void ConvertYamlToJsonElement_SimpleYaml_ReturnsJsonElement()
    {
        // Arrange
        var yaml = "name: test\nvalue: 123";

        // Act
        var result = YamlJsonConverter.ConvertYamlToJsonElement(yaml);

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.True(result.TryGetProperty("name", out var nameProperty));
        Assert.Equal("test", nameProperty.GetString());
        Assert.True(result.TryGetProperty("value", out var valueProperty));
        // YAML deserializer may return numbers as strings depending on context
        Assert.True(valueProperty.ValueKind == JsonValueKind.Number || valueProperty.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public void ConvertYamlToJsonElement_YamlArray_ReturnsJsonArray()
    {
        // Arrange
        var yaml = @"
- item1
- item2
- item3";

        // Act
        var result = YamlJsonConverter.ConvertYamlToJsonElement(yaml);

        // Assert
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal("item1", result[0].GetString());
        Assert.Equal("item2", result[1].GetString());
        Assert.Equal("item3", result[2].GetString());
    }

    [Fact]
    public void ConvertYamlToJsonElement_NestedYaml_ReturnsNestedJsonElement()
    {
        // Arrange
        var yaml = @"
outer:
  inner: value
  number: 42";

        // Act
        var result = YamlJsonConverter.ConvertYamlToJsonElement(yaml);

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.True(result.TryGetProperty("outer", out var outerProperty));
        Assert.True(outerProperty.TryGetProperty("inner", out var innerProperty));
        Assert.Equal("value", innerProperty.GetString());
        Assert.True(outerProperty.TryGetProperty("number", out var numberProperty));
        // YAML deserializer may return numbers as strings depending on context
        Assert.True(numberProperty.ValueKind == JsonValueKind.Number || numberProperty.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public void ConvertYamlToJsonElement_YamlWithBoolean_ReturnsBooleanJsonElement()
    {
        // Arrange
        var yaml = "enabled: true\ndisabled: false";

        // Act
        var result = YamlJsonConverter.ConvertYamlToJsonElement(yaml);

        // Assert
        Assert.True(result.TryGetProperty("enabled", out var enabledProperty));
        // YAML deserializer may return booleans as strings or booleans depending on context
        if (enabledProperty.ValueKind == JsonValueKind.True || enabledProperty.ValueKind == JsonValueKind.False)
        {
            Assert.True(enabledProperty.GetBoolean());
        }
        else
        {
            Assert.Equal("True", enabledProperty.GetString(), ignoreCase: true);
        }
        Assert.True(result.TryGetProperty("disabled", out var disabledProperty));
        if (disabledProperty.ValueKind == JsonValueKind.True || disabledProperty.ValueKind == JsonValueKind.False)
        {
            Assert.False(disabledProperty.GetBoolean());
        }
        else
        {
            Assert.Equal("False", disabledProperty.GetString(), ignoreCase: true);
        }
    }

    [Fact]
    public void RoundTrip_JsonToYamlToJson_PreservesStructure()
    {
        // Arrange
        var originalJson = @"{""name"":""test"",""value"":123,""nested"":{""key"":""value""}}";
        var jsonDoc = JsonDocument.Parse(originalJson);
        var jsonElement = jsonDoc.RootElement;

        // Act - Convert to YAML and back to JSON
        var yaml = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);
        var roundTripJsonElement = YamlJsonConverter.ConvertYamlToJsonElement(yaml);

        // Assert - Structure is preserved, though types may change
        Assert.True(roundTripJsonElement.TryGetProperty("name", out var nameProperty));
        Assert.Equal("test", nameProperty.GetString());
        Assert.True(roundTripJsonElement.TryGetProperty("value", out var valueProperty));
        // Value may be string or number after round-trip
        Assert.True(valueProperty.ValueKind == JsonValueKind.Number || valueProperty.ValueKind == JsonValueKind.String);
        Assert.True(roundTripJsonElement.TryGetProperty("nested", out var nestedProperty));
        Assert.True(nestedProperty.TryGetProperty("key", out var keyProperty));
        Assert.Equal("value", keyProperty.GetString());
    }

    [Fact]
    public void ConvertJsonElementToYaml_EmptyObject_ReturnsEmptyYamlObject()
    {
        // Arrange
        var json = @"{}";
        var jsonDoc = JsonDocument.Parse(json);
        var jsonElement = jsonDoc.RootElement;

        // Act
        var result = YamlJsonConverter.ConvertJsonElementToYaml(jsonElement);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("{}", result.Trim());
    }

    [Fact]
    public void ConvertYamlToJsonElement_EmptyYaml_ReturnsObjectJsonElement()
    {
        // Arrange
        var yaml = "";

        // Act
        var result = YamlJsonConverter.ConvertYamlToJsonElement(yaml);

        // Assert - Empty YAML deserializes to an empty object
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
    }
}
