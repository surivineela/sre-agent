import { DataPlaneClient } from './DataPlaneClient.ts';

export class IncidentHandlerClient extends DataPlaneClient {
    private static _instance: IncidentHandlerClient;

    public static getInstance(sreAgentEndpoint: string): IncidentHandlerClient {
        if (!IncidentHandlerClient._instance) {
            IncidentHandlerClient._instance = new IncidentHandlerClient(sreAgentEndpoint);
        }
        return IncidentHandlerClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }
}
