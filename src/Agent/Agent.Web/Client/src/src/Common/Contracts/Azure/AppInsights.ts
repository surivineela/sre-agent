export const AppInsightsEndpoints = {
    public: 'https://api.applicationinsights.io/v1/apps',
    fairfax: 'https://api.applicationinsights.us/v1/apps',
    mooncake: 'https://api.applicationinsights.azure.cn/v1/apps',
    usSec: 'https://api.applicationinsights.azure.microsoft.scloud/v1/apps',
    usNat: 'https://api.applicationinsights.azure.eaglex.ic.gov/v1/apps',
};

export type AppInsights = {
    Ver: string;
    ApplicationId: string;
    AppId: string;
    Application_type: string;
    Flow_type: string;
    Request_source: string;
    InstrumentationKey: string;
    ConnectionString: string;
    Name: string;
    CreationDate: string;
    PackageId: string;
    TenantId: string;
    HockeyAppId: string;
    HockeyAppToken: string;
    ProvisioningState: string;
    SamplingPercentage: string;
    CustomMetricOptedInType: string;
    Retention: string;
    DisableLocalAuth: boolean;
};

export type AppInsightsQueryBody = {
    query: string;
    /**
     * ISO8601 time period value (e.g. P7D, PT24H, etc.)
     */
    timespan?: string;
    /** Application IDs for cross-application queries */
    applications?: string[];
};

export type AppInsightsQueryResult = {
    tables: AppInsightsQueryResultTable[];
};

type AppInsightsQueryResultTable = {
    name: string;
    columns: AppInsightsQueryResultTableColumn[];
    rows: any[][];
};

type AppInsightsQueryResultTableColumn = {
    name: string;
    type: string;
};

export interface AppInsightsMonthlySummary {
    successCount: number;
    failedCount: number;
}
export interface AppInsightsInvocationTrace {
    timestamp: string;
    id: string;
    name: string;
    success: boolean;
    resultCode: string;
    duration: number;
    operationId: string;
    invocationId: string;
}
export interface AppInsightsInvocationTraceDetail {
    rowId: number;
    timestamp: string;
    message: string;
    logLevel: string;
    severityLevel: number;
}

export interface AppInsightsEntityTrace {
    timestamp: string;
    id: string;
    name: string;
    DurableFunctionsInstanceId: string;
    DurableFunctionsRuntimeStatus: string;
    DurableFunctionsType: number;
}
export interface AppInsightsEntityTraceDetail {
    rowId: number;
    timestamp: string;
    message: string;
    state: string;
}

export interface AppInsightsOrchestrationTrace {
    timestamp: string;
    DurableFunctionsInstanceId: string;
    DurableFunctionsRuntimeStatus: string;
}

export interface AppInsightsOrchestrationTraceDetail {
    rowId: number;
    timestamp: string;
    message: string;
    state: string;
}

export enum SeverityLevel {
    Verbose = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
}
