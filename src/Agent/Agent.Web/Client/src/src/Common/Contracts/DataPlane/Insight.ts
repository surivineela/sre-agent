export enum InsightKind {
    Incident = 'Incident',
    Configuration = 'Configuration',
    Repository = 'Repository',
    UsagePattern = 'UsagePattern',
}

export enum InsightPriority {
    High = 'High',
    Medium = 'Medium',
    Low = 'Low',
}

export enum InsightStatus {
    Active = 'Active',
    Dismissed = 'Dismissed',
    Snoozed = 'Snoozed',
    Resolved = 'Resolved',
}

export enum InsightActionType {
    Link = 'Link',
    Prompt = 'Prompt',
    Snooze = 'Snooze',
    Dismiss = 'Dismiss',
    SignIn = 'SignIn',
    ConnectRepo = 'ConnectRepo',
    EnableMcp = 'EnableMcp',
    StoreMemory = 'StoreMemory',
}

export interface InsightAction {
    id: string;
    type: InsightActionType;
    label: string;

    // For Link type actions
    url?: string;
    // For Prompt type actions
    prompt?: string;
    // For SignIn type actions
    connectorName?: string;
    // For ConnectRepo type actions
    repoUrl?: string;
    // For EnableMcp type actions
    mcpServerId?: string;
}

export interface InsightContent {
    title: string;
    message: string;
    prompt: string;
}

export interface Insight {
    id: string;
    createdAt: Date | string;
    updatedAt: Date | string;
    kind: InsightKind;
    priority: InsightPriority;
    status: InsightStatus;
    refCount: number;
    content: InsightContent;
    actions: InsightAction[];
}

export interface InsightsResponseContent {
    insights: Insight[];
    totalCount: number;
    skip: number;
    take: number;
    hasMore: boolean;
}
