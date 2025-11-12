// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Prometheus;

namespace Agent.Tests.Unit;

public class PrometheusQueryResponseTests
{
    [Fact]
    public void TestQueryResponseDataMatrix()
    {
        // Sample JSON response from https://prometheus.io/docs/prometheus/latest/querying/api/#range-queries
        var json = """
        {
            "status" : "success",
            "data" : {
                "resultType" : "matrix",
                "result" : [
                    {
                        "metric" : {
                            "__name__" : "up",
                            "job" : "prometheus",
                            "instance" : "localhost:9090"
                        },
                        "values" : [
                            [ 1435781430.781, "1" ],
                            [ 1435781445.781, "1" ],
                            [ 1435781460.781, "1" ]
                        ]
                    },
                    {
                        "metric" : {
                            "__name__" : "up",
                            "job" : "node",
                            "instance" : "localhost:9091"
                        },
                        "values" : [
                            [ 1435781430.781, "0" ],
                            [ 1435781445.781, "0" ],
                            [ 1435781460.781, "1" ]
                        ]
                    }
                ]
            }
        }
        """;
        var options = new JsonSerializerOptions
        {
            Converters =
            {
                new MetricItemConverter()
            }
        };
        var response = JsonSerializer.Deserialize<SuccessMatrixResponse>(json, options);
        Assert.NotNull(response);
        Assert.Equal(ResponseStatus.Success, response.Status);
        Assert.Equal(ResultType.Matrix, response.Data.ResultType);
        Assert.Equal(2, response.Data.Result.Length);
        Assert.Equal("up", response.Data.Result[0].Metric["__name__"]);
        Assert.Equal("prometheus", response.Data.Result[0].Metric["job"]);
        Assert.Equal("localhost:9090", response.Data.Result[0].Metric["instance"]);
        Assert.Equal(3, response.Data.Result[0].Values.Count);
        Assert.Equal(1435781430.781, response.Data.Result[0].Values[0].Item1, tolerance: 1e-3);
        Assert.Equal("1", response.Data.Result[0].Values[0].Item2);
        Assert.Equal(1435781445.781, response.Data.Result[0].Values[1].Item1, tolerance: 1e-3);
        Assert.Equal("1", response.Data.Result[0].Values[1].Item2);
        Assert.Equal(1435781460.781, response.Data.Result[0].Values[2].Item1, tolerance: 1e-3);
        Assert.Equal("1", response.Data.Result[0].Values[2].Item2);
        Assert.Equal("up", response.Data.Result[1].Metric["__name__"]);
        Assert.Equal("node", response.Data.Result[1].Metric["job"]);
        Assert.Equal("localhost:9091", response.Data.Result[1].Metric["instance"]);
        Assert.Equal(3, response.Data.Result[1].Values.Count);
        Assert.Equal(1435781430.781, response.Data.Result[1].Values[0].Item1, tolerance: 1e-3);
        Assert.Equal("0", response.Data.Result[1].Values[0].Item2);
        Assert.Equal(1435781445.781, response.Data.Result[1].Values[1].Item1, tolerance: 1e-3);
        Assert.Equal("0", response.Data.Result[1].Values[1].Item2);
        Assert.Equal(1435781460.781, response.Data.Result[1].Values[2].Item1, tolerance: 1e-3);
        Assert.Equal("1", response.Data.Result[1].Values[2].Item2);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ErrorResponse>(json, options));
    }

    [Fact]
    public void TestQueryResponseDataVector()
    {
        // Sample JSON response for vector type
        var json = """
        {
            "status" : "success",
            "data" : {
                "resultType" : "vector",
                "result" : [
                    {
                        "metric" : {
                            "__name__" : "up",
                            "job" : "prometheus",
                            "instance" : "localhost:9090"
                        },
                        "value": [ 1435781451.781, "1" ]
                    },
                    {
                        "metric" : {
                            "__name__" : "up",
                            "job" : "node",
                            "instance" : "localhost:9100"
                        },
                        "value" : [ 1435781451.781, "0" ]
                    }
                ]
            }
        }
        """;
        var options = new JsonSerializerOptions
        {
            Converters =
            {
                new MetricItemConverter()
            }
        };
        var response = JsonSerializer.Deserialize<SuccessVectorResponse>(json, options);
        Assert.NotNull(response);
        Assert.Equal(ResponseStatus.Success, response.Status);
        Assert.Equal(ResultType.Vector, response.Data.ResultType);
        Assert.Equal(2, response.Data.Result.Length);
        Assert.Equal("up", response.Data.Result[0].Metric["__name__"]);
        Assert.Equal("prometheus", response.Data.Result[0].Metric["job"]);
        Assert.Equal("localhost:9090", response.Data.Result[0].Metric["instance"]);
        Assert.Equal(1435781451.781, response.Data.Result[0].Value.Item1, tolerance: 1e-3);
        Assert.Equal("1", response.Data.Result[0].Value.Item2);
        Assert.Equal("up", response.Data.Result[1].Metric["__name__"]);
        Assert.Equal("node", response.Data.Result[1].Metric["job"]);
        Assert.Equal("localhost:9100", response.Data.Result[1].Metric["instance"]);
        Assert.Equal(1435781451.781, response.Data.Result[1].Value.Item1, tolerance: 1e-3);
        Assert.Equal("0", response.Data.Result[1].Value.Item2);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ErrorResponse>(json, options));
    }

    [Fact]
    public void TestQueryResponseError()
    {
        // Sample JSON response for an error
        var json = """
        {
            "status" : "error",
            "errorType" : "bad_data",
            "error" : "invalid query"
        }
        """;
        var options = new JsonSerializerOptions
        {
            Converters =
            {
                new MetricItemConverter()
            }
        };
        var response = JsonSerializer.Deserialize<ErrorResponse>(json, options);
        Assert.NotNull(response);
        Assert.Equal(ResponseStatus.Error, response.Status);
        Assert.Equal("bad_data", response.ErrorType);
        Assert.Equal("invalid query", response.Error);
    }
}
