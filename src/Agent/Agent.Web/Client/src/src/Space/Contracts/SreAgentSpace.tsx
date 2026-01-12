import { FluentIcon } from '@fluentui/react-icons';
import { ReactNode, RefObject } from 'react';

export enum PrimaryNavItemValues {
    Activities = 'activities',
    Monitor = 'monitor',
    Builder = 'builder',
    Settings = 'settings',
    Threads = 'thread',
}

export enum SecondaryNavItemValues {
    // activities
    IncidentOverview = 'incidents',
    DailyReports = 'dailyReports',

    // monitoring
    SessionInsights = 'sessionInsights',
    Graphs = 'resourceMapping',
    Metrics = 'metrics',
    Logs = 'logs',

    // builder
    ResponsePlans = 'responsePlans',
    ScheduledTasks = 'scheduledTasks',
    ExtendedAgentsGraph = 'subAgentBuilder',

    // settings
    Basics = 'basics',
    IncidentPlatform = 'incidentPlatform',
    AzureSettings = 'azureSettings',
    GrafanaDashboard = 'grafanaDashboard',
    ManagedResources = 'managedResourcesGroups',
    Connectors = 'connectors',
    KnowledgeBase = 'knowledgeBase',
    Permissions = 'permissions',
    SubAgents = 'subAgents',
    McpServers = 'mcpServers',
    Usage = 'agentConsumption',
}

export enum ThreadCategoryKey {
    Favorites = 'favorites',
    Regular = 'regular',
}

export interface NavItemInput {
    isVisible: boolean;
    disabled: boolean;
    label: ReactNode;
    icon?: FluentIcon;
    onClick?: () => void;
}

export interface CategoryNavItemInput extends NavItemInput {
    value: PrimaryNavItemValues;
    ref?: RefObject<HTMLButtonElement>;
}

export interface SubNavItemInput extends NavItemInput {
    value: SecondaryNavItemValues;
    ref?: RefObject<HTMLDivElement>;
}
