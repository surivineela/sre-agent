// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Kusto;
using Agent.Web.Models.ExtendedAgents;
using Agent.Web.Services;
using Xunit;

namespace Agent.Tests.Unit;

public class KustoToolDisplayOptionsTests
{
    [Fact]
    public void Validate_ShouldThrow_WhenDisplayOptionsHaveNegativeValues()
    {
        var definition = new KustoToolDefinition
        {
            Name = "Test",
            Type = "KustoTool",
            Connector = "connector",
            Description = "description",
            Database = "db",
            Function = "Func",
            Mode = KustoExecutionMode.Function,
            DisplayOptions = new KustoDisplayOptionsDefinition
            {
                MaxTableRows = -1
            }
        };

        Assert.Throws<ArgumentException>(() => definition.Validate());
    }

    [Fact]
    public void ToYamlToolDefinition_ShouldRoundTripDisplayOptions()
    {
        var document = new KustoToolDocumentModel(
            new ResourceMetadata
            {
                Name = "Tool"
            },
            new KustoToolSpec
            {
                Type = ToolDocumentModel.KustoToolType,
                Connector = "connector",
                Description = "desc",
                Parameters = new List<YamlParameter>(),
                Attributes = new List<string>(),
                Mode = KustoExecutionMode.Function,
                Function = "Func",
                Database = "db",
                DisplayOptions = new KustoDisplayOptionsDefinition
                {
                    ShowTable = true,
                    ShowChart = true,
                    MaxTableRows = 25,
                    MaxChartPoints = 150,
                    ChartTitle = "Title",
                    XField = "x",
                    SeriesFields = new List<string> { "s1", "s2" }
                }
            });

        var runtime = document.ToYamlToolDefinition() as KustoToolDefinition;

        Assert.NotNull(runtime);
        Assert.NotNull(runtime!.DisplayOptions);
        Assert.True(runtime.DisplayOptions!.ShowTable);
        Assert.True(runtime.DisplayOptions.ShowChart);
        Assert.Equal(25, runtime.DisplayOptions.MaxTableRows);
        Assert.Equal(150, runtime.DisplayOptions.MaxChartPoints);
        Assert.Equal("Title", runtime.DisplayOptions.ChartTitle);
        Assert.Equal("x", runtime.DisplayOptions.XField);
        Assert.Equal(new[] { "s1", "s2" }, runtime.DisplayOptions.SeriesFields);
    }

    [Fact]
    public void ApiToRuntimeMapper_ShouldPersistDisplayOptions()
    {
        var apiModel = new KustoToolApiModel
        {
            Name = "Tool",
            Type = "KustoTool",
            Connector = "connector",
            Description = "desc",
            Database = "db",
            Function = "Func",
            Mode = KustoExecutionMode.Function,
            DisplayOptions = new KustoDisplayOptionsDefinition { ShowTable = true, MaxTableRows = 10 }
        };

        var document = ApiToRuntimeMapper.ToDocumentTool(apiModel, "operation") as KustoToolDocumentModel;

        Assert.NotNull(document);
        Assert.True(document!.Spec.DisplayOptions?.ShowTable);
        Assert.Equal(10, document.Spec.DisplayOptions?.MaxTableRows);

        var runtime = document.ToYamlToolDefinition() as KustoToolDefinition;

        Assert.NotNull(runtime);
        Assert.True(runtime!.DisplayOptions?.ShowTable);
        Assert.Equal(10, runtime.DisplayOptions?.MaxTableRows);
    }

    [Fact]
    public void ToApiTool_ShouldPersistDisplayOptions()
    {
        var runtime = new KustoToolDefinition
        {
            Name = "Tool",
            Type = "KustoTool",
            Connector = "connector",
            Description = "desc",
            Database = "db",
            Function = "Func",
            Mode = KustoExecutionMode.Function,
            DisplayOptions = new KustoDisplayOptionsDefinition { ShowChart = true, MaxChartPoints = 42 }
        };

        var apiModel = ApiToRuntimeMapper.ToApiTool(runtime) as KustoToolApiModel;

        Assert.NotNull(apiModel);
        Assert.True(apiModel!.DisplayOptions?.ShowChart);
        Assert.Equal(42, apiModel.DisplayOptions?.MaxChartPoints);
    }
}
