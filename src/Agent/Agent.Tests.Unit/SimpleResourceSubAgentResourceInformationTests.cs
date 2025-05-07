using Agent.Runtime.SubAgents;

namespace Agent.Tests.Unit;

public class SimpleResourceSubAgentResourceInformationTests
{
    [Fact]
    public void SimpleResourceSubAgentResourceInformation_Constructor_InitializesProperties()
    {
        var resourceId = "/subscriptions/fe2ef518-fe95-41c5-9264-467faa5d6182/resourceGroups/avip2-operations-agent-3p-rg/providers/Microsoft.Storage/storageAccounts/avipteststorage";
        var resourceName = "avipteststorage";
        var resourceLocation = "westus2";

        var resourceInfo = new SimpleResourceSubAgentResourceInformation(resourceId, resourceName, resourceLocation);
        // Assert
        Assert.Equal(resourceId, resourceInfo.ResourceId);
        Assert.Equal(resourceName, resourceInfo.Name);
        Assert.Equal(resourceLocation, resourceInfo.Location);
    }

    [Fact]
    public void SimpleResourceSubAgentResourceInformation_ExtractsResourceProviderName()
    {
        var resourceInfo = new SimpleResourceSubAgentResourceInformation(
            "/subscriptions/fe2ef518-fe95-41c5-9264-467faa5d6182/resourceGroups/avip2-operations-agent-3p-rg/providers/Microsoft.Storage/storageAccounts/avipteststorage",
            "avipteststorage",
            "westus2"
        );
        Assert.Equal("microsoft.storage/storageaccounts", resourceInfo.ResourceProviderName, ignoreCase: false);
    }

    [Fact]
    public void SimpleResourceSubAgentResourceInformation_ExtractsAndLowerCasesResourceProviderName()
    {
        var resourceInfo = new SimpleResourceSubAgentResourceInformation(
            "/subscriptions/fe2ef518-fe95-41c5-9264-467faa5d6182/resourceGroups/avip2-operations-agent-3p-rg/providers/Microsoft.STORAGE/storageAccounts/avipteststorage",
            "avipteststorage",
            "westus2"
        );
        Assert.Equal("microsoft.storage/storageaccounts", resourceInfo.ResourceProviderName, ignoreCase: false);
    }

    [Fact]
    public void SimpleResourceSubAgentResourceInformation_DoesNotThrowOnMalformedResourceId()
    {
        var resourceInfo = new SimpleResourceSubAgentResourceInformation(
            // This is not a valid resource ID, but we want to ensure it doesn't throw an exception.
            "/subscriptions/fe2ef518-fe95-41c5-9264-467faa5d6182/resourceGroups/storageAccounts/avipteststorage",
            "avipteststorage",
            "westus2"
        );
        Assert.Equal("unknown", resourceInfo.ResourceProviderName, ignoreCase: false);
    }

}
