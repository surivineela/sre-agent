import axios from 'axios';
import { AgentTask } from '../Contracts/DataPlane/AgentTask';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient';

export class AgentTaskClient extends DataPlaneClient {
    private static _instance: AgentTaskClient;

    public static getInstance(sreAgentEndpoint: string): AgentTaskClient {
        if (!AgentTaskClient._instance) {
            AgentTaskClient._instance = new AgentTaskClient(sreAgentEndpoint);
        }
        return AgentTaskClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public getAgentTask = async (threadId: string, taskId: string): Promise<Response<AgentTask>> => {
        const url = this.getRequestUrl(`/api/v1/threads/${threadId}/agentTasks/${taskId}`);
        try {
            const response = await axios.get(url, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data as AgentTask,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };
}
