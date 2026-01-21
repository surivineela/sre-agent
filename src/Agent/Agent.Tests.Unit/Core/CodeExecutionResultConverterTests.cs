// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models;
using Shouldly;
using Xunit;

namespace Agent.Tests.Unit.Core;

public class CodeExecutionResultConverterTests
{
    private readonly JsonSerializerOptions _options;

    public CodeExecutionResultConverterTests()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _options.Converters.Add(new CodeExecutionResultConverter());
    }

    #region JSON Object Result Tests

    [Fact]
    public void Deserialize_ImageResult_ReturnsImageExecutionResult()
    {
        // Arrange
        var json = """
        {
            "type": "image",
            "format": "png",
            "base64_data": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ImageExecutionResult>();

        var imageResult = (ImageExecutionResult)result;
        imageResult.Type.ShouldBe("image");
        imageResult.Format.ShouldBe("png");
        imageResult.Base64Data.ShouldBe("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    }

    [Fact]
    public void Deserialize_NonImageObject_ThrowsJsonException()
    {
        // Arrange - the converter only supports image objects for direct object deserialization
        // Non-image objects should be passed as stringified JSON
        var json = """
        {
            "name": "test",
            "count": 5,
            "enabled": true
        }
        """;

        // Act & Assert
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<CodeExecutionResult>(json, _options));
    }

    #endregion

    #region Primitive Type Tests

    [Fact]
    public void Deserialize_NullResult_ReturnsNull()
    {
        // Arrange
        var json = "null";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_StringResult_ReturnsObjectExecutionResultWithString()
    {
        // Arrange
        var json = "\"Hello, World!\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe("Hello, World!");
    }

    [Fact]
    public void Deserialize_EmptyStringResult_ReturnsObjectExecutionResultWithEmptyString()
    {
        // Arrange
        var json = "\"\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public void Deserialize_IntegerResult_ReturnsObjectExecutionResultWithInt()
    {
        // Arrange
        var json = "42";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe(42);
        objectResult.Value.ShouldBeOfType<int>();
    }

    [Fact]
    public void Deserialize_NegativeIntegerResult_ReturnsObjectExecutionResultWithInt()
    {
        // Arrange
        var json = "-123";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe(-123);
        objectResult.Value.ShouldBeOfType<int>();
    }

    [Fact]
    public void Deserialize_LongResult_ReturnsObjectExecutionResultWithLong()
    {
        // Arrange
        var longValue = (long)int.MaxValue + 1;
        var json = longValue.ToString();

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe(longValue);
        objectResult.Value.ShouldBeOfType<long>();
    }

    [Fact]
    public void Deserialize_DoubleResult_ReturnsObjectExecutionResultWithDouble()
    {
        // Arrange
        var json = "3.14159";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe(3.14159);
        objectResult.Value.ShouldBeOfType<double>();
    }

    [Fact]
    public void Deserialize_BooleanTrueResult_ReturnsObjectExecutionResultWithTrue()
    {
        // Arrange
        var json = "true";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe(true);
        objectResult.Value.ShouldBeOfType<bool>();
    }

    [Fact]
    public void Deserialize_BooleanFalseResult_ReturnsObjectExecutionResultWithFalse()
    {
        // Arrange
        var json = "false";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe(false);
        objectResult.Value.ShouldBeOfType<bool>();
    }

    [Fact]
    public void Serialize_ObjectResultWithString_WritesStringValue()
    {
        // Arrange
        var objectResult = new ObjectExecutionResult { Value = "test string" };

        // Act
        var json = JsonSerializer.Serialize<CodeExecutionResult>(objectResult, _options);

        // Assert
        json.ShouldBe("\"test string\"");
    }

    [Fact]
    public void Serialize_ObjectResultWithInt_WritesIntValue()
    {
        // Arrange
        var objectResult = new ObjectExecutionResult { Value = 42 };

        // Act
        var json = JsonSerializer.Serialize<CodeExecutionResult>(objectResult, _options);

        // Assert
        json.ShouldBe("42");
    }

    [Fact]
    public void Serialize_ObjectResultWithBool_WritesBoolValue()
    {
        // Arrange
        var objectResult = new ObjectExecutionResult { Value = true };

        // Act
        var json = JsonSerializer.Serialize<CodeExecutionResult>(objectResult, _options);

        // Assert
        json.ShouldBe("true");
    }

    [Fact]
    public void Serialize_ObjectResultWithNull_WritesNull()
    {
        // Arrange
        var objectResult = new ObjectExecutionResult { Value = null };

        // Act
        var json = JsonSerializer.Serialize<CodeExecutionResult>(objectResult, _options);

        // Assert
        json.ShouldBe("null");
    }

    #endregion

    #region List/Array Result Tests

    [Fact]
    public void Deserialize_StringifiedArrayResult_ReturnsObjectExecutionResultWithJsonElement()
    {
        // Arrange - code interpreter returns array as a stringified JSON
        var json = "\"[1, 2, 3, 4, 5]\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBeOfType<JsonElement>();

        var jsonElement = (JsonElement)objectResult.Value!;
        jsonElement.ValueKind.ShouldBe(JsonValueKind.Array);
        jsonElement.GetArrayLength().ShouldBe(5);
    }

    [Fact]
    public void Deserialize_StringifiedMixedArrayResult_ReturnsObjectExecutionResultWithJsonElement()
    {
        // Arrange - array with mixed types
        var json = "\"[1, \\\"hello\\\", true, null, 3.14]\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBeOfType<JsonElement>();

        var jsonElement = (JsonElement)objectResult.Value!;
        jsonElement.ValueKind.ShouldBe(JsonValueKind.Array);
        jsonElement.GetArrayLength().ShouldBe(5);
    }

    [Fact]
    public void Deserialize_StringifiedNestedArrayResult_ReturnsObjectExecutionResultWithJsonElement()
    {
        // Arrange - nested array
        var json = "\"[[1, 2], [3, 4], [5, 6]]\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBeOfType<JsonElement>();

        var jsonElement = (JsonElement)objectResult.Value!;
        jsonElement.ValueKind.ShouldBe(JsonValueKind.Array);
        jsonElement.GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public void Deserialize_StringifiedEmptyArrayResult_ReturnsObjectExecutionResultWithJsonElement()
    {
        // Arrange
        var json = "\"[]\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBeOfType<JsonElement>();

        var jsonElement = (JsonElement)objectResult.Value!;
        jsonElement.ValueKind.ShouldBe(JsonValueKind.Array);
        jsonElement.GetArrayLength().ShouldBe(0);
    }

    #endregion

    #region Complex Object Result Tests

    [Fact]
    public void Deserialize_StringifiedObjectResult_ReturnsObjectExecutionResultWithJsonElement()
    {
        // Arrange - code interpreter returns object as stringified JSON
        var json = "\"{\\\"name\\\": \\\"John\\\", \\\"age\\\": 30}\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBeOfType<JsonElement>();

        var jsonElement = (JsonElement)objectResult.Value!;
        jsonElement.ValueKind.ShouldBe(JsonValueKind.Object);
        jsonElement.GetProperty("name").GetString().ShouldBe("John");
        jsonElement.GetProperty("age").GetInt32().ShouldBe(30);
    }

    [Fact]
    public void Deserialize_StringifiedNestedObjectResult_ReturnsObjectExecutionResultWithJsonElement()
    {
        // Arrange - nested object
        var json = "\"{\\\"person\\\": {\\\"name\\\": \\\"Jane\\\", \\\"address\\\": {\\\"city\\\": \\\"Seattle\\\", \\\"zip\\\": \\\"98101\\\"}}}\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBeOfType<JsonElement>();

        var jsonElement = (JsonElement)objectResult.Value!;
        jsonElement.ValueKind.ShouldBe(JsonValueKind.Object);

        var person = jsonElement.GetProperty("person");
        person.GetProperty("name").GetString().ShouldBe("Jane");

        var address = person.GetProperty("address");
        address.GetProperty("city").GetString().ShouldBe("Seattle");
        address.GetProperty("zip").GetString().ShouldBe("98101");
    }

    [Fact]
    public void Deserialize_StringifiedObjectWithArrayResult_ReturnsObjectExecutionResultWithJsonElement()
    {
        // Arrange - object containing arrays
        var json = "\"{\\\"items\\\": [1, 2, 3], \\\"tags\\\": [\\\"a\\\", \\\"b\\\"]}\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBeOfType<JsonElement>();

        var jsonElement = (JsonElement)objectResult.Value!;
        jsonElement.ValueKind.ShouldBe(JsonValueKind.Object);

        var items = jsonElement.GetProperty("items");
        items.ValueKind.ShouldBe(JsonValueKind.Array);
        items.GetArrayLength().ShouldBe(3);

        var tags = jsonElement.GetProperty("tags");
        tags.ValueKind.ShouldBe(JsonValueKind.Array);
        tags.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void Deserialize_StringifiedEmptyObjectResult_ReturnsObjectExecutionResultWithJsonElement()
    {
        // Arrange
        var json = "\"{}\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBeOfType<JsonElement>();

        var jsonElement = (JsonElement)objectResult.Value!;
        jsonElement.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Deserialize_NonJsonString_ReturnsObjectExecutionResultWithString()
    {
        // Arrange - a plain string that is not valid JSON
        var json = "\"This is just a plain text result\"";

        // Act
        var result = JsonSerializer.Deserialize<CodeExecutionResult>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ObjectExecutionResult>();

        var objectResult = (ObjectExecutionResult)result;
        objectResult.Value.ShouldBe("This is just a plain text result");
        objectResult.Value.ShouldBeOfType<string>();
    }

    #endregion
}
