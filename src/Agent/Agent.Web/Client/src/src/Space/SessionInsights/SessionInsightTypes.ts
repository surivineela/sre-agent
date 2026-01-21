export interface SessionInsightData {
    id?: string;
    threadId: string;
    title?: string;
    generatedTimestamp: string;
    insightMarkdown?: string;
    feedback?: Array<{
        rating: string;
        comment: string;
        submittedTimestamp?: Date;
        userId?: string;
    }>;
    feedbackCount: number;
    positiveFeedbackCount: number;
    negativeFeedbackCount: number;
}
