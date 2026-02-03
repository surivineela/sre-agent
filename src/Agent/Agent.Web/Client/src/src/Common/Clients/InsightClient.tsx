import axios from 'axios';
import { InsightsResponseContent } from '../Contracts/DataPlane/Insight.ts';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient.ts';

export interface InsightsGetOptions {
    skip: number;
    top: number;
}

export const getInsightsGetUrlPath = (options: InsightsGetOptions): string => {
    const { skip, top } = options;

    return `/api/v1/insights?skip=${skip}&top=${top}`;
};

export class InsightClient extends DataPlaneClient {
    private static _instance: InsightClient;

    public static getInstance(sreAgentEndpoint: string): InsightClient {
        if (!InsightClient._instance) {
            InsightClient._instance = new InsightClient(sreAgentEndpoint);
        }
        return InsightClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public getInsights = async (options: InsightsGetOptions): Promise<Response<InsightsResponseContent>> => {
        try {
            const path = getInsightsGetUrlPath(options);

            const url = this.getRequestUrl(path);

            const { data } = await axios.get(url, {
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
            };
        }
    };
}
