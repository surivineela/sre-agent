// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Kusto;

namespace Agent.Tests.Unit.Plugins.Kusto;

public class AzureRegionTests
{
    [Theory]
    [InlineData(AzureRegion.EastUS, "eastus")]
    [InlineData(AzureRegion.WestUS2, "westus2")]
    [InlineData(AzureRegion.CentralUS, "centralus")]
    [InlineData(AzureRegion.SouthCentralUS, "southcentralus")]
    [InlineData(AzureRegion.AustraliaEast, "australiaeast")]
    [InlineData(AzureRegion.NorthEurope, "northeurope")]
    [InlineData(AzureRegion.SoutheastAsia, "southeastasia")]
    public void ToNormalizedString_ShouldReturnCorrectFormat(AzureRegion region, string expected)
    {
        // Act
        var result = region.ToNormalizedString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("eastus", AzureRegion.EastUS)]
    [InlineData("westus2", AzureRegion.WestUS2)]
    [InlineData("centralus", AzureRegion.CentralUS)]
    [InlineData("southcentralus", AzureRegion.SouthCentralUS)]
    [InlineData("australiaeast", AzureRegion.AustraliaEast)]
    [InlineData("northeurope", AzureRegion.NorthEurope)]
    [InlineData("southeastasia", AzureRegion.SoutheastAsia)]
    [InlineData("EASTUS", AzureRegion.EastUS)] // Case insensitive
    [InlineData("WestUS2", AzureRegion.WestUS2)] // Case insensitive
    public void FromNormalizedString_ShouldReturnCorrectEnum(string normalizedRegion, AzureRegion expected)
    {
        // Act
        var result = AzureRegionExtensions.FromNormalizedString(normalizedRegion);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromNormalizedString_WithInvalidInput_ShouldThrowArgumentException(string? invalidRegion)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => AzureRegionExtensions.FromNormalizedString(invalidRegion!));
    }

    [Fact]
    public void FromNormalizedString_WithUnknownRegion_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => AzureRegionExtensions.FromNormalizedString("invalidregion"));
    }

    [Fact]
    public void RoundTrip_ShouldMaintainEquality()
    {
        // Arrange
        var originalRegion = AzureRegion.WestUS2;

        // Act
        var normalized = originalRegion.ToNormalizedString();
        var parsed = AzureRegionExtensions.FromNormalizedString(normalized);

        // Assert
        Assert.Equal(originalRegion, parsed);
    }

    [Fact]
    public void AllRegions_ShouldHaveNormalizedStringMapping()
    {
        // Arrange
        var allRegions = Enum.GetValues<AzureRegion>();

        // Act & Assert - ensure no exceptions are thrown for any enum value
        foreach (var region in allRegions)
        {
            var normalized = region.ToNormalizedString();
            Assert.NotNull(normalized);
            Assert.NotEmpty(normalized);

            // Verify round trip works
            var parsed = AzureRegionExtensions.FromNormalizedString(normalized);
            Assert.Equal(region, parsed);
        }
    }
}
