import axios from 'axios';
import { AgentTask } from '../Contracts/Azure/AgentTaskDevTypes';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient';

export class AgentTaskDevClient extends DataPlaneClient {
    private static instance: AgentTaskDevClient;

    public static getInstance(baseUrl: string): AgentTaskDevClient {
        if (!AgentTaskDevClient.instance) {
            AgentTaskDevClient.instance = new AgentTaskDevClient(baseUrl);
        }
        return AgentTaskDevClient.instance;
    }

    /**
     * Get a specific agent task by thread ID and agent task ID
     */
    public getAgentTask = async (threadId: string, agentTaskId: string): Promise<Response<AgentTask | undefined>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl(`/api/v1/threads/${threadId}/agentTasks/${agentTaskId}`), {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    /**
     * Get all agent tasks for a thread
     */
    public getAgentTasks = async (threadId: string): Promise<Response<AgentTask[]>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl(`/api/v1/threads/${threadId}/agentTasks`), {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data.value ?? [],
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
                content: [],
            };
        }
    };
}
