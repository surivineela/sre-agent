export interface ResourceType {
    resourceType: string;
    locations: string[];
    apiVersions: string[];
}

export interface LocForResTypes {
    id: string;
    namespace: string;
    resourceTypes: ResourceType[];
    registrationState: string;
}

export interface SupportedModel {
    default: boolean;
    providerName: string;
    providerDisplayName: string;
    modelName: string;
    modelDisplayName: string;
    multiplier: string;
}
