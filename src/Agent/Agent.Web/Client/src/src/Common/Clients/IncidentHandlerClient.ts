import axios from 'axios';
import {
    IIncidentDocument,
    IncidentFilter,
    IncidentFilterPayload,
    IncidentHandler,
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

    public listHandlers = async (): Promise<Response<IncidentHandler[]>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/handlers`);
        try {
            const { data } = await axios.get<IncidentHandler[]>(url, {
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

    public listIncidentFilters = async (): Promise<Response<IncidentFilter[]>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/filters`);
        try {
            const { data } = await axios.get<IncidentFilter[]>(url, {
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

    public deleteIncidentFilter = async (id: string): Promise<Response<IncidentFilter[]>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/filters/${id}`);
        try {
            await axios.delete<IncidentFilter>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public createIncidentFilter = async (body: IncidentFilterPayload): Promise<Response<IncidentFilter>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/filters/${body.Id}`);
        try {
            const { data } = await axios.put<IncidentFilter>(url, body, {
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

    public enableIncidentFilter = async (id: string): Promise<Response<IncidentFilter>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/filters/${id}/enable`);
        try {
            const { data } = await axios.post<IncidentFilter>(url, {
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

    public disableIncidentFilter = async (id: string): Promise<Response<IncidentFilter>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/filters/${id}/disable`);
        try {
            const { data } = await axios.post<IncidentFilter>(url, {
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

    public getFilterFieldOptions = async (): Promise<Response<any>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/filterFieldOptions`);
        try {
            const { data } = await axios.get<any>(url, {
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
            const { data } = await axios.post<{ items: IIncidentDocument[]; totalCount: number }>(
                url,
                { ...request, pageSize: 1000 },
                {
                    headers: getAgentHeaders(),
                }
            );
            return {
                isSuccessful: true,
                content: data.items,
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

    public createHandler = async (request: IncidentHandler): Promise<Response<IncidentHandler>> => {
        const url = this.getRequestUrl(`${this._apiPathPrefix}/handlers/${request.id}`);
        try {
            const { data } = await axios.put<IncidentHandler>(url, request, {
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
