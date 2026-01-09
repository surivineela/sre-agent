// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Graph.Crawler;
using Shouldly;

namespace Agent.Tests.Unit.Graph.Crawler;

public class CrawlerExtensionsTests
{
    [Fact]
    public void GetSanitizedCosmosDBId_StripsQueryAndFragment()
    {
        var input = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1?id=123#frag";

        var result = CrawlerExtensions.GetSanitizedCosmosDBId(input);

        result.ShouldBe("_subscriptions_sub1_resourcegroups_rg1_providers_microsoft.web_sites_app1");
    }

    [Fact]
    public void GetSanitizedCosmosDBId_ReplacesInvalidCharacters()
    {
        var input = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1\\child:1 name?id=123#x";

        var result = CrawlerExtensions.GetSanitizedCosmosDBId(input);

        result.ShouldBe("_subscriptions_sub1_resourcegroups_rg1_providers_microsoft.web_sites_app1_child_1_name");
    }
}
