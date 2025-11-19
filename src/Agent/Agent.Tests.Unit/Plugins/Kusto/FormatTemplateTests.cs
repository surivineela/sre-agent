// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Kusto;

namespace Agent.Tests.Unit.Plugins.Kusto;

public class FormatTemplateTests
{
    [Fact]
    public void Replaces_DoubleHash_Placeholders()
    {
        var args = new Dictionary<string, string> { { "region", "eastus" } };
        var input = "##region##";
        var result = KustoPlugin.FormatTemplate(input, args);
        Assert.Equal("eastus", result);
    }

    [Fact]
    public void Replaces_Dollar_Placeholders()
    {
        var args = new Dictionary<string, string> { { "env", "prod" } };
        var input = "Deployment in $env";
        var result = KustoPlugin.FormatTemplate(input, args);
        Assert.Equal("Deployment in prod", result);
    }

    [Fact]
    public void Replaces_Mixed_Placeholders()
    {
        var args = new Dictionary<string, string>
    {
        { "region", "eastus" },
        { "env", "prod" }
    };
        var input = "##env##-$region";
        var result = KustoPlugin.FormatTemplate(input, args);
        Assert.Equal("prod-eastus", result);
    }

    [Fact]
    public void DoubleQuoted_Strings()
    {
        var args = new Dictionary<string, string> { { "region", "eastus" } };
        var input = "\"##region##\" \"$region\" outside ##region##";
        var result = KustoPlugin.FormatTemplate(input, args);
        Assert.Equal("\"eastus\" \"eastus\" outside eastus", result);
    }

    [Fact]
    public void Handles_Overlapping_Keys()
    {
        var args = new Dictionary<string, string>
    {
        { "region", "eastus" },
        { "regionCode", "EUS" }
    };
        var input = "Location: $region ($regionCode)";
        var result = KustoPlugin.FormatTemplate(input, args);
        Assert.Equal("Location: eastus (EUS)", result);
    }

    [Fact]
    public void Handles_Empty_Input()
    {
        var args = new Dictionary<string, string> { { "region", "eastus" } };
        var result = KustoPlugin.FormatTemplate(string.Empty, args);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Handles_Null_Input()
    {
        var args = new Dictionary<string, string> { { "region", "eastus" } };
        var result = KustoPlugin.FormatTemplate(string.Empty, args);
        Assert.Empty(result);
    }

    [Fact]
    public void Handles_Null_Args()
    {
        var result = KustoPlugin.FormatTemplate("##region##", new Dictionary<string, string>());
        Assert.Equal("##region##", result);
    }

    [Fact]
    public void Handles_Adjacent_Placeholders()
    {
        var args = new Dictionary<string, string>
    {
        { "a", "1" },
        { "b", "2" }
    };
        var input = "##a####b##";
        var result = KustoPlugin.FormatTemplate(input, args);
        Assert.Equal("12", result);
    }

    [Fact]
    public void Handles_Multiple_Quoted_Sections()
    {
        var args = new Dictionary<string, string> { { "env", "prod" } };
        var input = "'ignore this' and 'this too' but use ##env##";
        var result = KustoPlugin.FormatTemplate(input, args);
        Assert.Equal("'ignore this' and 'this too' but use prod", result);
    }
}
