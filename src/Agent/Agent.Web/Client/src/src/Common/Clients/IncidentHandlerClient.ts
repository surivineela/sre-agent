import axios from 'axios';
import { ITelemetryInfo } from '../AzPortalProxy/Models/ITelemetryInfo';
import {
    IncidentDocument,
    IncidentFilter,
    IncidentFilterDocumentPayload,
    IncidentHandler,
    IncidentQueryRequest,
    IncidentQueryResponse,
    InstructionGenerationRequest,
    InstructionGenerationResponse,
    TestHandlerPayload,
    TestHandlerResponse,
    ToolInfo,
} from '../Contracts/Azure/IncidentHandler.ts';
import { getAgentHeaders } from '../Helpers/headers.ts';
import { DataPlaneClient, Response } from './DataPlaneClient.ts';

export type LogFunction = (info: ITelemetryInfo) => void;

export class IncidentHandlerClient extends DataPlaneClient {
    private static _instance: IncidentHandlerClient;
    private readonly _apiBasePathPrefix = '/api/v1';
    private readonly _apiIncidentPlaygroundPathPrefix = `${this._apiBasePathPrefix}/incidentplayground`;
    private _log?: LogFunction;

    public static getInstance(sreAgentEndpoint: string, log: LogFunction): IncidentHandlerClient {
        if (!IncidentHandlerClient._instance) {
            IncidentHandlerClient._instance = new IncidentHandlerClient(sreAgentEndpoint, log);
        }
        return IncidentHandlerClient._instance;
    }

    constructor(sreAgentEndpoint: string, log?: LogFunction) {
        super(sreAgentEndpoint);
        this._log = log;
    }

    public checkConnectivity = async (): Promise<Response<boolean>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/checkConnectivity`);
        try {
            const { data } = await axios.get<boolean>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'checkConnectivity',
                actionModifier: 'failed',
                data: `Failed to check connectivity: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public listHandlers = async (): Promise<Response<IncidentHandler[]>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/handlers`);
        try {
            const { data } = await axios.get<IncidentHandler[]>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'listHandlers',
                actionModifier: 'failed',
                data: `Failed to list handlers: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public listIncidentFilters = async (): Promise<Response<IncidentFilter[]>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/filters`);
        try {
            const { data } = await axios.get<IncidentFilter[]>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'listIncidentFilters',
                actionModifier: 'failed',
                data: `Failed to list incident filters: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public deleteIncidentFilter = async (id: string): Promise<Response<IncidentFilter[]>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/filters/${id}`);
        try {
            await axios.delete<IncidentFilter>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'deleteIncidentFilter',
                actionModifier: 'failed',
                data: `Failed to delete incident filter: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public createIncidentFilter = async (body: IncidentFilterDocumentPayload): Promise<Response<IncidentFilter>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/filters/${body.id}`);
        try {
            const { data } = await axios.put<IncidentFilter>(url, body, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'createIncidentFilter',
                actionModifier: 'failed',
                data: `Failed to create incident filter: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public updateIncidentFilter = async (body: IncidentFilterDocumentPayload): Promise<Response<IncidentFilter>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/filters/${body.id}`);
        try {
            const { data } = await axios.post<IncidentFilter>(url, body, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'updateIncidentFilter',
                actionModifier: 'failed',
                data: `Failed to update incident filter: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public enableIncidentFilter = async (id: string): Promise<Response<IncidentFilter>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/filters/${id}/enable`);
        try {
            const { data } = await axios.post<IncidentFilter>(url, null, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'enableIncidentFilter',
                actionModifier: 'failed',
                data: `Failed to enable incident filter: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public disableIncidentFilter = async (id: string): Promise<Response<IncidentFilter>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/filters/${id}/disable`);
        try {
            const { data } = await axios.post<IncidentFilter>(url, null, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'disableIncidentFilter',
                actionModifier: 'failed',
                data: `Failed to disable incident filter: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public getFilterFieldOptions = async (): Promise<Response<any>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/filterFieldOptions`);
        try {
            const { data } = await axios.get<any>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'getFilterFieldOptions',
                actionModifier: 'failed',
                data: `Failed to get filter field options: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public generateInstructions = async (request: InstructionGenerationRequest): Promise<Response<InstructionGenerationResponse>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/generateInstructions`);
        try {
            const { data } = await axios.post<InstructionGenerationResponse>(url, request, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'generateInstructions',
                actionModifier: 'failed',
                data: `Failed to generate instructions: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public queryIncidents = async (request: IncidentQueryRequest): Promise<Response<IncidentQueryResponse>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/queryIncidents`);
        try {
            const { data } = await axios.post<IncidentQueryResponse>(url, request, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'queryIncidents',
                actionModifier: 'failed',
                data: `Failed to query incidents: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public getIncident = async (incidentId: string): Promise<Response<IncidentDocument>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/getIncident/${incidentId}`);
        try {
            const { data } = await axios.get<IncidentDocument>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'getIncident',
                actionModifier: 'failed',
                data: `Failed to get incident: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public listTools = async (searchString: string = ''): Promise<Response<ToolInfo[]>> => {
        const url = this.getRequestUrl(
            `${this._apiIncidentPlaygroundPathPrefix}/listTools?searchString=${encodeURIComponent(searchString)}`
        );
        try {
            const { data } = await axios.get<ToolInfo[]>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'listTools',
                actionModifier: 'failed',
                data: `Failed to list tools: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public createHandler = async (request: IncidentHandler): Promise<Response<IncidentHandler>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/handlers/${request.id}`);
        try {
            const { data } = await axios.put<IncidentHandler>(url, request, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'createHandler',
                actionModifier: 'failed',
                data: `Failed to create handler: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public updateHandler = async (request: IncidentHandler): Promise<Response<IncidentHandler>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/handlers/${request.id}`);
        try {
            const { data } = await axios.post<IncidentHandler>(url, request, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'updateHandler',
                actionModifier: 'failed',
                data: `Failed to update handler: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public getHandler = async (handlerId: string): Promise<Response<IncidentHandler>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/handlers/${handlerId}`);
        try {
            const { data } = await axios.get<IncidentHandler>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'getHandler',
                actionModifier: 'failed',
                data: `Failed to get handler: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public deleteHandler = async (handlerId: string): Promise<Response<void>> => {
        const url = this.getRequestUrl(`${this._apiIncidentPlaygroundPathPrefix}/handlers/${handlerId}`);
        try {
            await axios.delete<IncidentHandler>(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'deleteHandler',
                actionModifier: 'failed',
                data: `Failed to delete handler: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };

    public testHandler = async (request: TestHandlerPayload): Promise<Response<TestHandlerResponse>> => {
        const url = this.getRequestUrl(`${this._apiBasePathPrefix}/IncidentWebhook/processIncident`);
        try {
            const { data } = await axios.post<TestHandlerResponse>(url, request, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'testHandler',
                actionModifier: 'failed',
                data: `Failed to test handler: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: error,
            };
        }
    };
}
