export interface ArmId {
    /** The kind/type of ARM resource ID */
    readonly kind: ArmIdKind;
    /** Subscription ID */
    readonly subscription?: string;
    /** Resource group name */
    readonly resourceGroup?: string;
    /** Location/region */
    readonly location?: string;
    /** Resource provider (e.g., "Microsoft.Compute") */
    readonly provider?: string;
    /** Full resource type including provider (e.g., "Microsoft.Compute/virtualMachines") */
    readonly resourceType?: string;
    readonly resourceName?: string;
    /** (for tag IDs) */
    readonly tagName?: string;
    /** (for tag IDs) */
    readonly tagValue?: string;
    /** Reason for invalid parse (if kind === Invalid) */
    readonly reason?: string;
}

export enum ArmIdKind {
    Invalid = "Invalid",
    Subscription = "Subscription",
    ResourceGroup = "ResourceGroup",
    Resource = "Resource",
    SubscriptionResource = "SubscriptionResource",
    TenantResource = "TenantResource",
    Location = "Location",
    Provider = "Provider",
    SubscriptionProvider = "SubscriptionProvider",
    TenantProvider = "TenantProvider",
    SubscriptionTagName = "SubscriptionTagName",
    SubscriptionTagValue = "SubscriptionTagValue",
}

enum MachineState {
    Start = "Start",
    Subscriptions = "Subscriptions",
    SubscriptionId = "SubscriptionId",
    ResourceGroups = "ResourceGroups",
    ResourceGroupId = "ResourceGroupId",
    Providers = "Providers",
    ProviderId = "ProviderId",
    ResourceType = "ResourceType",
    ResourceId = "ResourceId",
    Locations = "Locations",
    LocationId = "LocationId",
    TagNames = "TagNames",
    TagNameId = "TagNameId",
    TagValues = "TagValues",
    TagValueId = "TagValueId",
}

interface MachineData {
    subscription?: string;
    resourceGroup?: string;
    location?: string;
    provider?: string;
    tagName?: string;
    tagValue?: string;
    resourceTypes: string[];
    resourceNames: string[];
    kind: ArmIdKind;
}

type ProcessorFn = (data: MachineData, token: string, value: string) => MachineState;

const invalidResult: ArmId = {
    kind: ArmIdKind.Invalid,
    reason: "Invalid ARM ID",
};

/**
 * Parses an ARM resource ID string into its components
 *
 * Usage:
 *   const armId = ArmId.parse("/subscriptions/sub-123/resourceGroups/rg-name/providers/Microsoft.Compute/virtualMachines/vm-name");
 *   console.log(armId.subscription);    // "sub-123"
 *   console.log(armId.resourceGroup);   // "rg-name"
 *   console.log(armId.resourceType);    // "Microsoft.Compute/virtualMachines"
 *   console.log(armId.resourceName);    // "vm-name"
 *
 * @param id The ARM resource ID to parse
 * @returns Parsed ARM ID object with extracted components
 */
export const parseArmId = (id: string): ArmId => {
    // Validate input
    if (typeof id !== "string") {
        return { ...invalidResult, reason: "not a string" };
    }

    // Remove query string if present
    id = (id || "").split("?")[0] || "";

    // Split into segments
    const segments = id.split("/");

    // ARM IDs must start with "/" and have odd number of segments
    if (segments.length === 1 || segments.length % 2 !== 1 || segments[0].length !== 0) {
        return { ...invalidResult, reason: "invalid number of segments" };
    }

    // Initialize state machine
    const data: MachineData = {
        resourceTypes: [],
        resourceNames: [],
        kind: ArmIdKind.Invalid,
    };

    let state = MachineState.Start;

    // Process each token/value pair
    for (let i = 1; i < segments.length; i += 2) {
        const token = segments[i];
        const value = segments[i + 1];

        if (!token || !value) {
            return { ...invalidResult, reason: "empty segment" };
        }

        const processor = getProcessor(state, token.toLowerCase());
        if (!processor) {
            return { ...invalidResult, reason: `invalid token: ${token}` };
        }

        state = processor(data, token, value);
        data.kind = getKindForState(state);
    }

    // Build result
    if (data.kind === ArmIdKind.Invalid) {
        return { ...invalidResult, reason: "invalid at conclusion" };
    }

    const result: ArmId = {
        kind: data.kind,
        subscription: data.subscription,
        resourceGroup: data.resourceGroup,
        location: data.location,
        provider: data.provider,
        tagName: data.tagName,
        tagValue: data.tagValue,
        resourceType: buildResourceType(data),
        resourceName: data.resourceNames.join("/"),
    };

    return result;
};

/**
 * Converts an ARM ID object back to a string.
 *
 * @param armId The ARM ID object to stringify
 * @returns The ARM ID as a string
 */
export const stringifyArmId = (armId: ArmId): string => {
    if (!armId || armId.kind === ArmIdKind.Invalid) {
        return "";
    }

    const parts: string[] = [];

    // Build subscription part
    if (armId.subscription) {
        parts.push(`/subscriptions/${armId.subscription}`);
    }

    // Build resource group part
    if (armId.resourceGroup) {
        parts.push(`/resourceGroups/${armId.resourceGroup}`);
    }

    // Build location part
    if (armId.location && armId.kind === ArmIdKind.Location) {
        parts.push(`/locations/${armId.location}`);
    }

    // Build provider and resource parts
    if (armId.provider) {
        parts.push(`/providers/${armId.provider}`);

        if (armId.resourceType && armId.resourceName) {
            const resourceTypes = armId.resourceType.split("/").slice(1); // Skip provider
            const resourceNames = armId.resourceName.split("/");

            for (let i = 0; i < resourceTypes.length; i++) {
                parts.push(`/${resourceTypes[i]}/${resourceNames[i] || ""}`);
            }
        }
    }

    // Build tag parts
    if (armId.tagName) {
        parts.push(`/tagNames/${armId.tagName}`);
        if (armId.tagValue) {
            parts.push(`/tagValues/${armId.tagValue}`);
        }
    }

    return parts.join("");
};

// Helper functions

const buildResourceType = (data: MachineData): string => {
    if (data.kind === ArmIdKind.Subscription) {
        return "Microsoft.Resources/subscriptions";
    }
    if (data.kind === ArmIdKind.ResourceGroup) {
        return "Microsoft.Resources/resourceGroups";
    }
    if (data.provider && data.resourceTypes.length > 0) {
        return `${data.provider}/${data.resourceTypes.join("/")}`;
    }
    return "";
};

const getProcessor = (state: MachineState, token: string): ProcessorFn | null => {
    const processors: Record<MachineState, Record<string, ProcessorFn>> = {
        [MachineState.Start]: {
            subscriptions: (data, _, value) => {
                data.subscription = value;
                return MachineState.SubscriptionId;
            },
            providers: (data, _, value) => {
                data.provider = value;
                return MachineState.ProviderId;
            },
        },
        [MachineState.SubscriptionId]: {
            resourcegroups: (data, _, value) => {
                data.resourceGroup = value;
                return MachineState.ResourceGroupId;
            },
            locations: (data, _, value) => {
                data.location = value;
                return MachineState.LocationId;
            },
            providers: (data, _, value) => {
                data.provider = value;
                return MachineState.ProviderId;
            },
            tagnames: (data, _, value) => {
                data.tagName = value;
                return MachineState.TagNameId;
            },
        },
        [MachineState.ResourceGroupId]: {
            providers: (data, _, value) => {
                data.provider = value;
                return MachineState.ProviderId;
            },
        },
        [MachineState.ProviderId]: {
            "": (data, token, value) => {
                data.resourceTypes.push(token);
                data.resourceNames.push(value);
                return MachineState.ResourceId;
            },
        },
        [MachineState.ResourceId]: {
            "": (data, token, value) => {
                data.resourceTypes.push(token);
                data.resourceNames.push(value);
                return MachineState.ResourceId;
            },
        },
        [MachineState.LocationId]: {},
        [MachineState.TagNameId]: {
            tagvalues: (data, _, value) => {
                data.tagValue = value;
                return MachineState.TagValueId;
            },
        },
        [MachineState.TagValueId]: {},
        [MachineState.Subscriptions]: {},
        [MachineState.ResourceGroups]: {},
        [MachineState.Providers]: {},
        [MachineState.ResourceType]: {},
        [MachineState.Locations]: {},
        [MachineState.TagNames]: {},
        [MachineState.TagValues]: {},
    };

    const stateProcessors = processors[state];
    if (!stateProcessors) return null;

    return stateProcessors[token] || stateProcessors[""];
};

const getKindForState = (state: MachineState): ArmIdKind => {
    const kindMap: Record<MachineState, ArmIdKind> = {
        [MachineState.Start]: ArmIdKind.Invalid,
        [MachineState.Subscriptions]: ArmIdKind.Invalid,
        [MachineState.SubscriptionId]: ArmIdKind.Subscription,
        [MachineState.ResourceGroups]: ArmIdKind.Invalid,
        [MachineState.ResourceGroupId]: ArmIdKind.ResourceGroup,
        [MachineState.Providers]: ArmIdKind.Invalid,
        [MachineState.ProviderId]: ArmIdKind.Provider,
        [MachineState.ResourceType]: ArmIdKind.Invalid,
        [MachineState.ResourceId]: ArmIdKind.Resource,
        [MachineState.Locations]: ArmIdKind.Invalid,
        [MachineState.LocationId]: ArmIdKind.Location,
        [MachineState.TagNames]: ArmIdKind.Invalid,
        [MachineState.TagNameId]: ArmIdKind.SubscriptionTagName,
        [MachineState.TagValues]: ArmIdKind.Invalid,
        [MachineState.TagValueId]: ArmIdKind.SubscriptionTagValue,
    };

    return kindMap[state] || ArmIdKind.Invalid;
};

/**
 * Extracts the subscription ID from an ARM resource ID.
 *
 * @param resourceId The ARM resource ID
 * @returns The subscription ID or empty string if not found
 */
export const getSubscriptionId = (resourceId: string): string => {
    const armId = parseArmId(resourceId);
    return armId.subscription || "";
};

/**
 * Extracts the resource group name from an ARM resource ID.
 *
 * @param resourceId The ARM resource ID
 * @returns The resource group name or empty string if not found
 */
export const getResourceGroup = (resourceId: string): string => {
    const armId = parseArmId(resourceId);
    return armId.resourceGroup || "";
};

/**
 * Checks if the given ID is a valid ARM resource ID.
 *
 * @param id The ID to validate
 * @returns True if valid, false otherwise
 */
export const isValidArmId = (id: string): boolean => {
    const armId = parseArmId(id);
    return armId.kind !== ArmIdKind.Invalid;
};
