export const suggestedWelcomePrompts = [
    'List all resource groups managed by you across all subscriptions',
    'List container apps managed by you across all subscriptions',
    'Show me all apps with public ingress enabled',
    'Which apps have autoscaling enabled?',
    'What are some best practices I can apply to my apps?',
];

export type SourceCodeLinkStatus = 'Linked' | 'RequiresAuth' | 'NotLinked' | 'RequiresReAuth';
export type ResourceHealth = 'Healthy' | 'Warning' | 'Critical' | 'Unknown';

export interface SourceCodeLinkageStatus {
    status: SourceCodeLinkStatus;
    repositoryUrl?: string | null;
    linkedTimestamp?: string | null;
    loginCallbackUrl?: string | null;
}

export interface LogicalApplication {
    resourceId: string;
    name?: string;
    subType: string;
    properties?: {
        type?: string;
        health?: ResourceHealth;
    };
    sourceCodeLinkageStatus: SourceCodeLinkageStatus;
    additionalInfo: {
        namespace?: string;
    };
}

export interface IntegrationStatus {
    name: string;
    isActive: boolean;
    details: string;
}

export interface KnowledgeGraphStatus {
    status: string;
    crawlProgress: {
        crawled: number;
        totalResources: number;
        finishedInitialCrawl: boolean;
    };
    crawlProgressByResourceType: Record<
        string,
        {
            crawled: number;
            totalResources: number;
        }
    >;
}

export interface WelcomeMessageResponse {
    knowledgeGraphStatus?: KnowledgeGraphStatus;
    logicalApplications?: LogicalApplication[];
    integrations?: IntegrationStatus[];
}

export interface LogicalAppGridItem {
    rscName: string;
    rscType: string;
    rscSubType: string;
}

export enum LogicalAppGridKey {
    CoreApplicationGroup = 'Core application group',
    PrimaryResourceType = 'Primary resource type',
    ResourceMap = 'Resource map',
}

export interface ResourceGroupGridItem {
    name: string;
    subscription: string;
    region: string;
}

export enum ResourceGroupGridKey {
    ResourceGroup = 'Resource group',
    Subscription = 'Subscription',
    Region = 'Region',
}
