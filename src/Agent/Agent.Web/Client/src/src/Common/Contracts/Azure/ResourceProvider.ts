export interface ResourceProvider {
    id: string;
    namespace: string;
    resourceTypes: Array<{
        resourceType: string;
        locations: string[];
        apiVersions: string[];
    }>;
}
