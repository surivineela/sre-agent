import { AntUxStringComparison, equals } from './Strings';

export type StringMap<T> = {
    [key: string]: T;
};

export function getCanonicalLocation(userFriendlyLocation?: string): string {
    return userFriendlyLocation ? userFriendlyLocation.replace(/\s+/g, '').replace('(', '').replace(')', '').toLowerCase() : '';
}

export function isSameLocation(locationA?: string, locationB?: string): boolean {
    const canonicalLocationA = getCanonicalLocation(locationA);
    const canonicalLocationB = getCanonicalLocation(locationB);

    return equals(canonicalLocationA, canonicalLocationB, AntUxStringComparison.IgnoreCase);
}

export function getUserFriendlyLocation(location: string): string {
    return locationsMap[location] || location;
}

const locationsMap: StringMap<string> = {
    brazilsouth: 'Brazil South',
    centralus: 'Central US',
    centraluseuap: 'Central US EUAP',
    eastus2: 'East US 2',
    eastus2euap: 'East US 2 EUAP',
    eastus: 'East US',
    japaneast: 'Japan East',
    japanwest: 'Japan West',
    northcentralus: 'North Central US',
    northcentralusstage: 'North Central US (Stage)',
    northeurope: 'North Europe',
    southeastasia: 'Southeast Asia',
    eastasia: 'East Asia',
    westeurope: 'West Europe',
    westus: 'West US',
    francecentral: 'France Central',
    francesouth: 'France South',
    australiasoutheast: 'Australia Southeast',
    australiaeast: 'Australia East',
    australiacentral: 'Australia Central',
    australiacentral2: 'Australia Central 2',
    southcentralus: 'South Central US',
    westindia: 'West India',
    centralindia: 'Central India',
    southindia: 'South India',
    ukwest: 'UK West',
    uksouth: 'UK South',
    westcentralus: 'West Central US',
    canadacentral: 'Canada Central',
    canadaeast: 'Canada East',
    koreacentral: 'Korea Central',
    koreasouth: 'Korea South',
    westus2: 'West US 2',
    westus3: 'West US 3',
    southafricanorth: 'South Africa North',
    switzerlandnorth: 'Switzerland North',
    germanywestcentral: 'Germany West Central',
    uaecentral: 'UAE Central',
    uaenorth: 'UAE North',
    germanynorth: 'Germany North',
    norwayeast: 'Norway East',
    norwaywest: 'Norway West',
    swedencentral: 'Sweden Central',
    qatarcentral: 'Qatar Central',
    //FairFax
    usdodcentral: 'USDoD Central',
    usdodeast: 'USDoD East',
    usgovarizona: 'USGov Arizona',
    usgoviowa: 'USGov Iowa',
    usgovtexas: 'USGov Texas',
    usgovvirginia: 'USGov Virginia',
    //Mooncake
    chinanorth: 'China North',
    chinaeast: 'China East',
    chinanorth2: 'China North 2',
    chinaeast2: 'China East 2',
    chinanorth3: 'China North 3',
    chinaeast3: 'China East 3',

    //USNat
    usnateast: 'USNat East',
    usnatwest: 'USNat West',
    //USSec
    usseceast: 'USSec East',
    ussecwest: 'USSec West',
};

export const failoverPairedLocations: StringMap<string[]> = {
    // Black forest
    germanycentral: ['germanynortheast'],
    germanynortheast: ['germanycentral'],

    // Fairfax
    usdodcentral: ['usdodeast'],
    usdodeast: ['usdodcentral'],
    usgovarizona: ['usgovtexas'],
    usgoviowa: ['usgovvirginia'],
    usgovtexas: ['usgovarizona', 'usgovvirginia'],
    usgovvirginia: ['usgovtexas', 'usgoviowa'], // Iowa is phasing out as secondary

    // Mooncake
    chinaeast: ['chinanorth'],
    chinaeast2: ['chinanorth2'],
    chinanorth: ['chinaeast'],
    chinanorth2: ['chinaeast2'],

    // Public cloud
    australiacentral: ['australiacentral2'],
    australiacentral2: ['australiacentral'],
    australiaeast: ['australiasoutheast'],
    australiasoutheast: ['australiaeast'],
    brazilsouth: ['southcentralus', 'brazilsoutheast'],
    canadacentral: ['canadaeast'],
    canadaeast: ['canadacentral'],
    centralindia: ['southindia'],
    centralus: ['eastus2'],
    centraluseuap: ['eastus2euap'],
    eastasia: ['southeastasia'],
    eastus: ['westus', 'westus3'],
    eastus2: ['centralus'],
    'eastus2(stage)': ['westus.validation'],
    eastus2euap: ['centraluseuap'],
    eastusstg: ['southcentralusstg'],
    francecentral: ['francesouth'],
    francesouth: ['francecentral'],
    germanynorth: ['germanywestcentral'],
    germanywestcentral: ['germanynorth'],
    japaneast: ['japanwest'],
    japanwest: ['japaneast'],
    koreacentral: ['koreasouth'],
    koreasouth: ['koreacentral'],
    northcentralus: ['southcentralus'],
    northeurope: ['westeurope'],
    norwayeast: ['norwaywest'],
    norwaywest: ['norwayeast'],
    southafricanorth: ['southafricawest'],
    southafricawest: ['southafricanorth'],
    southcentralus: ['northcentralus'],
    southeastasia: ['eastasia'],
    southindia: ['centralindia'],
    switzerlandnorth: ['switzerlandwest'],
    switzerlandwest: ['switzerlandnorth'],
    uaecentral: ['uaenorth'],
    uaenorth: ['uaecentral'],
    uknorth: ['uksouth2'],
    uksouth: ['ukwest'],
    uksouth2: ['uknorth'],
    ukwest: ['uksouth'],
    'usnorth(stage)': ['westus.validation'],
    westcentralus: ['westus2'],
    westeurope: ['northeurope'],
    westindia: ['southindia'],
    westus: ['eastus'],
    westus2: ['westcentralus'],

    // USSec
    usseceast: ['ussecwest'],
    ussecwest: ['usseceast'],

    // USNat
    usnateast: ['usnatwest'],
    usnatwest: ['usnateast'],
};
