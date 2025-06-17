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
