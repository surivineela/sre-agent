import axios from 'axios';
import { getAgentHeaders } from '../Helpers/headers.ts';
import { DataPlaneClient, Response } from './DataPlaneClient.ts';

export interface SessionInsightsGetResponse {
    insights?: SessionInsightRawData[];
    Insights?: SessionInsightRawData[];
}

export interface SessionInsightRawData {
    threadId?: string;
    ThreadId?: string;
    title?: string;
    Title?: string;
    generatedTimestamp?: string;
    GeneratedTimestamp?: string;
    insightMarkdown?: string;
    InsightMarkdown?: string;
    feedback?: any[];
    Feedback?: any[];
    feedbackCount?: number;
    FeedbackCount?: number;
    positiveFeedbackCount?: number;
    PositiveFeedbackCount?: number;
    negativeFeedbackCount?: number;
    NegativeFeedbackCount?: number;
}

export interface SessionInsight {
    threadId: string;
    title: string;
    generatedTimestamp: string;
    insightMarkdown: string;
    feedback?: any[];
    feedbackCount: number;
    positiveFeedbackCount: number;
    negativeFeedbackCount: number;
}

export class SessionInsightClient extends DataPlaneClient {
    private static _instance: SessionInsightClient;

    public static getInstance(sreAgentEndpoint: string): SessionInsightClient {
        if (!SessionInsightClient._instance) {
            SessionInsightClient._instance = new SessionInsightClient(sreAgentEndpoint);
        }
        return SessionInsightClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public getInsights = async (): Promise<Response<SessionInsight[]>> => {
        try {
            const { data } = await axios.get<SessionInsightsGetResponse>(this.getRequestUrl(`/api/v1/threads/insights?skip=0&take=1000`), {
                headers: getAgentHeaders(),
            });

            const insightsRawData = data?.insights ?? data?.Insights;

            const insights: SessionInsight[] = Array.isArray(insightsRawData)
                ? insightsRawData.map((item: SessionInsightRawData) => ({
                      threadId: item.threadId ?? item.ThreadId ?? '',
                      title: item.title ?? item.Title ?? '',
                      generatedTimestamp: item.generatedTimestamp ?? item.GeneratedTimestamp ?? '',
                      insightMarkdown: item.insightMarkdown ?? item.InsightMarkdown ?? '',
                      feedback: item.feedback ?? item.Feedback,
                      feedbackCount: item.feedbackCount ?? item.FeedbackCount ?? 0,
                      positiveFeedbackCount: item.positiveFeedbackCount ?? item.PositiveFeedbackCount ?? 0,
                      negativeFeedbackCount: item.negativeFeedbackCount ?? item.NegativeFeedbackCount ?? 0,
                  }))
                : [];

            return {
                isSuccessful: true,
                content: insights,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };
}
