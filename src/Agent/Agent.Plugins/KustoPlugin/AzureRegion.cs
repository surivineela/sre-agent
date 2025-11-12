// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Agent.Plugins.Kusto
{
    /// <summary>
    /// Serializable enumeration of Azure regions for Kusto operations.
    /// This ensures agents pass valid region identifiers and provides compile-time validation.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AzureRegion
    {
        [Description("East US")]
        EastUS,

        [Description("East US 2")]
        EastUS2,

        [Description("West US")]
        WestUS,

        [Description("West US 2")]
        WestUS2,

        [Description("West US 3")]
        WestUS3,

        [Description("Central US")]
        CentralUS,

        [Description("South Central US")]
        SouthCentralUS,

        [Description("North Central US")]
        NorthCentralUS,

        [Description("Canada Central")]
        CanadaCentral,

        [Description("Canada East")]
        CanadaEast,

        [Description("Brazil South")]
        BrazilSouth,

        [Description("North Europe")]
        NorthEurope,

        [Description("West Europe")]
        WestEurope,

        [Description("UK South")]
        UKSouth,

        [Description("UK West")]
        UKWest,

        [Description("France Central")]
        FranceCentral,

        [Description("France South")]
        FranceSouth,

        [Description("Germany West Central")]
        GermanyWestCentral,

        [Description("Germany North")]
        GermanyNorth,

        [Description("Switzerland North")]
        SwitzerlandNorth,

        [Description("Switzerland West")]
        SwitzerlandWest,

        [Description("Norway East")]
        NorwayEast,

        [Description("Norway West")]
        NorwayWest,

        [Description("Sweden Central")]
        SwedenCentral,

        [Description("Sweden South")]
        SwedenSouth,

        [Description("Southeast Asia")]
        SoutheastAsia,

        [Description("East Asia")]
        EastAsia,

        [Description("Australia East")]
        AustraliaEast,

        [Description("Australia Southeast")]
        AustraliaSoutheast,

        [Description("Australia Central")]
        AustraliaCentral,

        [Description("Australia Central 2")]
        AustraliaCentral2,

        [Description("Japan East")]
        JapanEast,

        [Description("Japan West")]
        JapanWest,

        [Description("Korea Central")]
        KoreaCentral,

        [Description("Korea South")]
        KoreaSouth,

        [Description("India Central")]
        IndiaCentral,

        [Description("India South")]
        IndiaSouth,

        [Description("India West")]
        IndiaWest,

        [Description("UAE Central")]
        UAECentral,

        [Description("UAE North")]
        UAENorth,

        [Description("South Africa North")]
        SouthAfricaNorth,

        [Description("South Africa West")]
        SouthAfricaWest
    }

    /// <summary>
    /// Extension methods for AzureRegion enum to handle conversion to/from normalized string formats.
    /// </summary>
    public static class AzureRegionExtensions
    {
        /// <summary>
        /// Converts the AzureRegion enum to its normalized string representation 
        /// (lowercase, no spaces or special characters) as expected by Azure APIs.
        /// </summary>
        /// <param name="region">The Azure region enum value</param>
        /// <returns>Normalized string representation of the region</returns>
        public static string ToNormalizedString(this AzureRegion region)
        {
            return region switch
            {
                AzureRegion.EastUS => "eastus",
                AzureRegion.EastUS2 => "eastus2",
                AzureRegion.WestUS => "westus",
                AzureRegion.WestUS2 => "westus2",
                AzureRegion.WestUS3 => "westus3",
                AzureRegion.CentralUS => "centralus",
                AzureRegion.SouthCentralUS => "southcentralus",
                AzureRegion.NorthCentralUS => "northcentralus",
                AzureRegion.CanadaCentral => "canadacentral",
                AzureRegion.CanadaEast => "canadaeast",
                AzureRegion.BrazilSouth => "brazilsouth",
                AzureRegion.NorthEurope => "northeurope",
                AzureRegion.WestEurope => "westeurope",
                AzureRegion.UKSouth => "uksouth",
                AzureRegion.UKWest => "ukwest",
                AzureRegion.FranceCentral => "francecentral",
                AzureRegion.FranceSouth => "francesouth",
                AzureRegion.GermanyWestCentral => "germanywestcentral",
                AzureRegion.GermanyNorth => "germanynorth",
                AzureRegion.SwitzerlandNorth => "switzerlandnorth",
                AzureRegion.SwitzerlandWest => "switzerlandwest",
                AzureRegion.NorwayEast => "norwayeast",
                AzureRegion.NorwayWest => "norwaywest",
                AzureRegion.SwedenCentral => "swedencentral",
                AzureRegion.SwedenSouth => "swedensouth",
                AzureRegion.SoutheastAsia => "southeastasia",
                AzureRegion.EastAsia => "eastasia",
                AzureRegion.AustraliaEast => "australiaeast",
                AzureRegion.AustraliaSoutheast => "australiasoutheast",
                AzureRegion.AustraliaCentral => "australiacentral",
                AzureRegion.AustraliaCentral2 => "australiacentral2",
                AzureRegion.JapanEast => "japaneast",
                AzureRegion.JapanWest => "japanwest",
                AzureRegion.KoreaCentral => "koreacentral",
                AzureRegion.KoreaSouth => "koreasouth",
                AzureRegion.IndiaCentral => "indiacentral",
                AzureRegion.IndiaSouth => "indiasouth",
                AzureRegion.IndiaWest => "indiawest",
                AzureRegion.UAECentral => "uaecentral",
                AzureRegion.UAENorth => "uaenorth",
                AzureRegion.SouthAfricaNorth => "southafricanorth",
                AzureRegion.SouthAfricaWest => "southafricawest",
                _ => throw new ArgumentOutOfRangeException(nameof(region), region, "Unknown Azure region")
            };
        }

        /// <summary>
        /// Parses a normalized string representation to an AzureRegion enum value.
        /// </summary>
        /// <param name="normalizedRegion">The normalized region string (lowercase, no spaces)</param>
        /// <returns>The corresponding AzureRegion enum value</returns>
        /// <exception cref="ArgumentException">Thrown when the region string is not recognized</exception>
        public static AzureRegion FromNormalizedString(string normalizedRegion)
        {
            if (string.IsNullOrWhiteSpace(normalizedRegion))
                throw new ArgumentException("Region cannot be null or empty", nameof(normalizedRegion));

            return normalizedRegion.ToLowerInvariant() switch
            {
                "eastus" => AzureRegion.EastUS,
                "eastus2" => AzureRegion.EastUS2,
                "westus" => AzureRegion.WestUS,
                "westus2" => AzureRegion.WestUS2,
                "westus3" => AzureRegion.WestUS3,
                "centralus" => AzureRegion.CentralUS,
                "southcentralus" => AzureRegion.SouthCentralUS,
                "northcentralus" => AzureRegion.NorthCentralUS,
                "canadacentral" => AzureRegion.CanadaCentral,
                "canadaeast" => AzureRegion.CanadaEast,
                "brazilsouth" => AzureRegion.BrazilSouth,
                "northeurope" => AzureRegion.NorthEurope,
                "westeurope" => AzureRegion.WestEurope,
                "uksouth" => AzureRegion.UKSouth,
                "ukwest" => AzureRegion.UKWest,
                "francecentral" => AzureRegion.FranceCentral,
                "francesouth" => AzureRegion.FranceSouth,
                "germanywestcentral" => AzureRegion.GermanyWestCentral,
                "germanynorth" => AzureRegion.GermanyNorth,
                "switzerlandnorth" => AzureRegion.SwitzerlandNorth,
                "switzerlandwest" => AzureRegion.SwitzerlandWest,
                "norwayeast" => AzureRegion.NorwayEast,
                "norwaywest" => AzureRegion.NorwayWest,
                "swedencentral" => AzureRegion.SwedenCentral,
                "swedensouth" => AzureRegion.SwedenSouth,
                "southeastasia" => AzureRegion.SoutheastAsia,
                "eastasia" => AzureRegion.EastAsia,
                "australiaeast" => AzureRegion.AustraliaEast,
                "australiasoutheast" => AzureRegion.AustraliaSoutheast,
                "australiacentral" => AzureRegion.AustraliaCentral,
                "australiacentral2" => AzureRegion.AustraliaCentral2,
                "japaneast" => AzureRegion.JapanEast,
                "japanwest" => AzureRegion.JapanWest,
                "koreacentral" => AzureRegion.KoreaCentral,
                "koreasouth" => AzureRegion.KoreaSouth,
                "indiacentral" => AzureRegion.IndiaCentral,
                "indiasouth" => AzureRegion.IndiaSouth,
                "indiawest" => AzureRegion.IndiaWest,
                "uaecentral" => AzureRegion.UAECentral,
                "uaenorth" => AzureRegion.UAENorth,
                "southafricanorth" => AzureRegion.SouthAfricaNorth,
                "southafricawest" => AzureRegion.SouthAfricaWest,
                _ => throw new ArgumentException($"Unknown or unsupported Azure region: {normalizedRegion}", nameof(normalizedRegion))
            };
        }
    }
}
