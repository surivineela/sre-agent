// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using System.Text.Json;
using Agent.Data.DatabaseClients.GraphDbClient;
using Shouldly;

namespace Agent.Tests.Unit.Data.DatabaseClients.GraphDbClient;

public class CustomGraphSON2ReaderTests
{
    private static JsonElement ParseJsonElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("2147483647")]
    [InlineData("-2147483648")]
    public void ToObject_WithIntegerWithinInt32Range_ReturnsLong(string jsonNumber)
    {
        var reader = new CustomGraphSON2Reader();
        var element = ParseJsonElement(jsonNumber);

        object? result = reader.ToObject(element);

        result.ShouldNotBeNull();
        result.ShouldBeOfType<long>();
        ((long)result!).ShouldBe(long.Parse(jsonNumber, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("9223372036854775807")]
    public void ToObject_WithIntegerOutsideInt32Range_ReturnsLong(string jsonNumber)
    {
        var reader = new CustomGraphSON2Reader();
        var element = ParseJsonElement(jsonNumber);

        object? result = reader.ToObject(element);

        result.ShouldNotBeNull();
        result.ShouldBeOfType<long>();
        ((long)result!).ShouldBe(long.Parse(jsonNumber, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("1.25")]
    [InlineData("-0.5")]
    [InlineData("1.0")]
    public void ToObject_WithDecimalNumber_ReturnsDecimal(string jsonNumber)
    {
        var reader = new CustomGraphSON2Reader();
        var element = ParseJsonElement(jsonNumber);

        object? result = reader.ToObject(element);

        result.ShouldNotBeNull();
        result.ShouldBeOfType<decimal>();
        ((decimal)result!).ShouldBe(decimal.Parse(jsonNumber, CultureInfo.InvariantCulture));
    }
}
