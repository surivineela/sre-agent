export type AzureMonitorWorkspace = {
    metrics: {
        prometheusQueryEndpoint: string;
    };
    defaultIngestionSettings: {
        dataCollectionEndpointResourceId: string;
        dataCollectionRuleResourceId: string;
    };
};
