// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Agent.Prometheus;

using MetricItem = (double, string); // Prometheus metric timestamp and value

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResponseStatus
{
    [EnumMember(Value = "success")]
    Success,
    [EnumMember(Value = "error")]
    Error,
}

// https://prometheus.io/docs/prometheus/latest/querying/api/#instant-queries
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResultType
{
    // Return type of range queries
    [EnumMember(Value = "matrix")]
    Matrix,
    [EnumMember(Value = "vector")]
    // Return type of instant queries
    Vector,

    // todo: Not supported yet
    [EnumMember(Value = "scalar")]
    Scalar,
    // todo: Not supported yet
    [EnumMember(Value = "string")]
    String,
}

public abstract record QueryResponseData(
    [property: JsonPropertyName("resultType"), JsonRequired] ResultType ResultType
);


public record MatrixResultItem(
    [property: JsonPropertyName("metric"), JsonRequired] Dictionary<string, string> Metric, // Prometheus metric name and labels
    [property: JsonPropertyName("values"), JsonRequired] List<MetricItem> Values // List of values, where each value is a tuple of (timestamp, value)
    // todo: support histograms
);

public record VectorResultItem(
    [property: JsonPropertyName("metric"), JsonRequired] Dictionary<string, string> Metric, // Prometheus metric name and labels
    [property: JsonPropertyName("value"), JsonRequired] MetricItem Value // Single value, where value is a tuple of (timestamp, value)
    // todo : support histogram
);


public record QueryResponseDataMatrix(
    MatrixResultItem[] Result
) : QueryResponseData(ResultType.Matrix)
{
    [JsonPropertyName("result")]
    [JsonRequired]
    public MatrixResultItem[] Result { get; set; } = Result;
}

public record QueryResponseDataVector(
    VectorResultItem[] Result
) : QueryResponseData(ResultType.Vector)
{
    [JsonPropertyName("result")]
    [JsonRequired]
    public VectorResultItem[] Result { get; set; } = Result;
}

// For the structure of response please check https://prometheus.io/docs/prometheus/latest/querying/api/
public abstract record Response(
    [property: JsonPropertyName("status"), JsonRequired] ResponseStatus Status,
    [property: JsonPropertyName("warnings")] string[]? Warnings,
    [property: JsonPropertyName("infos")] string[]? Infos
);

public record SuccessVectorResponse(QueryResponseDataVector Data) : Response(ResponseStatus.Success, null, null)
{
    [JsonPropertyName("data")]
    [JsonRequired]
    public QueryResponseDataVector Data { get; set; } = Data;
}

public record SuccessMatrixResponse(QueryResponseDataMatrix Data) : Response(ResponseStatus.Success, null, null)
{
    [JsonPropertyName("data")]
    [JsonRequired]
    public QueryResponseDataMatrix Data { get; set; } = Data;
}

public record ErrorResponse(string ErrorType, string Error, QueryResponseData? Data, string[]? Warnings, string[]? Infos) : Response(ResponseStatus.Error, Warnings, Infos)
{
    // [JsonPropertyName("data")]
    // public QueryResponseData? Data { get; set; } = Data;

    [JsonPropertyName("errorType")]
    [JsonRequired]
    public string ErrorType { get; set; } = ErrorType;

    [JsonPropertyName("error")]
    [JsonRequired]
    public string Error { get; set; } = Error;
}

public class MetricItemConverter : JsonConverter<MetricItem>
{
    public override MetricItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected StartArray token");
        }

        reader.Read();
        double timestamp = reader.GetDouble();

        reader.Read();
        string? value = reader.GetString() ?? throw new JsonException("Expected string value for value");

        reader.Read(); // Move past EndArray
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Expected EndArray token");
        }

        return (timestamp, value);
    }

    public override void Write(Utf8JsonWriter writer, MetricItem value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Item1);
        writer.WriteStringValue(value.Item2);
        writer.WriteEndArray();
    }
}