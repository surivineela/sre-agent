import axios from 'axios';
import { CreateScheduledTaskRequest, ScheduledTask } from '../../Space/Contracts/ScheduledTasks';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient';
import { LogFunction } from './IncidentHandlerClient';

export class ScheduledTasksClient extends DataPlaneClient {
    private static _instance: ScheduledTasksClient;
    private _log?: LogFunction;

    public static getInstance(sreAgentEndpoint: string, log: LogFunction): ScheduledTasksClient {
        if (!ScheduledTasksClient._instance) {
            ScheduledTasksClient._instance = new ScheduledTasksClient(sreAgentEndpoint, log);
        }
        return ScheduledTasksClient._instance;
    }

    constructor(sreAgentEndpoint: string, log: LogFunction) {
        super(sreAgentEndpoint);
        this._log = log;
    }

    public async getScheduledTasks(): Promise<Response<ScheduledTask[]>> {
        const url = this.getRequestUrl('/api/v1/scheduledtasks');
        try {
            const response = await axios.get(url, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data as ScheduledTask[],
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'getScheduledTasks',
                actionModifier: 'failed',
                data: `Failed to get scheduled tasks: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }

    public async createScheduledTask(task: CreateScheduledTaskRequest): Promise<Response<{ taskId: string }>> {
        const url = this.getRequestUrl('/api/v1/scheduledtasks');
        try {
            const response = await axios.post(url, task, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data as { taskId: string },
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'createScheduledTask',
                actionModifier: 'failed',
                data: `Failed to create scheduled task: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }

    public async updateScheduledTask(id: string, updates: Partial<ScheduledTask>): Promise<Response<void>> {
        const url = this.getRequestUrl(`/api/v1/scheduledtasks/${id}`);
        try {
            const response = await axios.put(url, updates, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'updateScheduledTask',
                actionModifier: 'failed',
                data: `Failed to update scheduled task: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }

    public async deleteScheduledTask(id: string): Promise<Response<void>> {
        const url = this.getRequestUrl(`/api/v1/scheduledtasks/${id}`);
        try {
            const response = await axios.delete(url, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'deleteScheduledTask',
                actionModifier: 'failed',
                data: `Failed to delete scheduled task: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }

    public async pauseScheduledTask(id: string): Promise<Response<void>> {
        const url = this.getRequestUrl(`/api/v1/scheduledtasks/${id}/pause`);
        try {
            const response = await axios.post(url, undefined, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'pauseScheduledTask',
                actionModifier: 'failed',
                data: `Failed to pause scheduled task: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }

    public async resumeScheduledTask(id: string): Promise<Response<void>> {
        const url = this.getRequestUrl(`/api/v1/scheduledtasks/${id}/resume`);
        try {
            const response = await axios.post(url, undefined, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'resumeScheduledTask',
                actionModifier: 'failed',
                data: `Failed to resume scheduled task: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }

    public async runScheduledTask(id: string): Promise<Response<void>> {
        const url = this.getRequestUrl(`/api/v1/scheduledtasks/${id}/execute`);
        try {
            const response = await axios.post(url, undefined, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'runScheduledTask',
                actionModifier: 'failed',
                data: `Failed to run scheduled task: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }

    public async getScheduledTaskExecutions(id: string): Promise<Response<ScheduledTask[]>> {
        const url = this.getRequestUrl(`/api/v1/scheduledtasks/${id}/executions`);
        try {
            const response = await axios.get(url, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data as ScheduledTask[],
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            this._log?.({
                logLevel: 'error',
                action: 'getScheduledTaskExecutions',
                actionModifier: 'failed',
                data: `Failed to get scheduled task executions: ${errorMessage}`,
            });
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }
}
