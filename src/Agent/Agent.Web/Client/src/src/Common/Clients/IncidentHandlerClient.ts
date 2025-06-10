import axios from 'axios';
import {
    IIncidentDocument,
    IncidentQueryRequest,
    InstructionGenerationRequest,
    InstructionGenerationResponse,
    ToolInfo,
} from '../Contracts/Azure/IncidentHandler.ts';
import { getAgentHeaders } from '../Helpers/headers.ts';
import { DataPlaneClient, Response } from './DataPlaneClient.ts';

export class IncidentHandlerClient extends DataPlaneClient {
    private static _instance: IncidentHandlerClient;
    private readonly _apiPathPrefix = '/api/v1/incidentplayground';

    public static getInstance(sreAgentEndpoint: string): IncidentHandlerClient {
        if (!IncidentHandlerClient._instance) {
            IncidentHandlerClient._instance = new IncidentHandlerClient(sreAgentEndpoint);
        }
        return IncidentHandlerClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public generateInstructions = async (request: InstructionGenerationRequest): Promise<Response<InstructionGenerationResponse>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/generateInstructions`);
        try {
            const { data } = await axios.post<InstructionGenerationResponse>(url, request, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public queryIncidents = async (request: IncidentQueryRequest): Promise<Response<IIncidentDocument[]>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/queryIncidents`);
        try {
            const { data } = await axios.post<IIncidentDocument[]>(url, request, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public listTools = async (searchString: string = ''): Promise<Response<ToolInfo[]>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/listTools?searchString=${encodeURIComponent(searchString)}`);
        try {
            const { data } = await axios.get<ToolInfo[]>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };
}
