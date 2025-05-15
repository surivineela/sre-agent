import { createContext } from 'react';

export type Subscription = {
    id: string;
    name: string;
};

export type ResourceExtended = {
    id: string; // the string has underscore
    name: string;
    type: string;
    dashboardUrl: string;
    appHealthInfo?: string[]; // Convert the json string to ScoreCardObject
    sourceCodeLinkageStatus?: {
        status: 'Linked' | 'RequiresAuth';
        repositoryUrl: string;
        loginCallbackUrl: string | null;
    };
    properties: {
        dashboardUrl: string[];
        resourceType: string[];
        resourceName: string[];
        resourceId: string[];
        subscriptionId: string[];
        resourceGroupName: string[];
        location: string[];
        runningStatus: string;
        remarks: string[];
        appHealthInfo?: string[]; // Convert the json string to ScoreCardObject
    };
};

export type Resource = {
    name: string;
    type: string;
    dashboardUrl: string;
    resourceId: string; // the string has underscore
    appHealthInfo?: string[]; // Convert the json string to ScoreCardObject
    subItems?: Resource[];
    relationToParent?: string;
    isRelationReversed?: boolean;
};

export type ScoreCardObject = {
    Costs: number; // 7 day window
    Availability?: number | null; // percentage
    Health: 'healthy' | 'unhealthy' | 'unknown' | 'degraded';
    Transactions: number;
    AvgLatencyInMs?: number | null;
    AvgMemoryUsage?: number | null; // bytes
    AvgCpuUsage?: number | null; // percentage
    LastDataCaptureTimeStampInUTC?: string | Date;
    IsActive: boolean;
};

export type GraphNode = {
    id: string;
    name: string;
    subscriptionId: string;
    properties?: Resource;
};

export type GraphEdge = {
    label?: string;
};

interface GraphContextProps {
    selectedNode?: GraphNode;
    setSelectedNode: (_?: GraphNode) => void;
    hoverNode: (nodeId: string) => void;
    unHoverNode: () => void;
    nodesToHightlight: string[];
    edgesToHightlight: string[];
    selectedAppGroupId?: string;
}

export const GraphContext = createContext<GraphContextProps>({
    setSelectedNode: (_?: GraphNode) => {},
    hoverNode: () => {},
    unHoverNode: () => {},
    nodesToHightlight: [],
    edgesToHightlight: [],
});

export class NodeSize {
    static readonly width = 300;
    static readonly height = 100;
}

export enum NodeRelations {
    Contains = 'CONTAINS',
    Linked = 'LINKED',
    SqlConnected = 'SQL_CONNECTED',
    RedisConnected = 'REDIS_CONNECTED',
    UsesRedis = 'USES_REDIS',
    HasRole = 'HAS_ROLE',
    HasIdentity = 'HAS_IDENTITY',
    Connected = 'CONNECTED',
    Hosts = 'HOSTS',
    HostedOn = 'HOSTED_ON',
    ServesCode = 'SERVES_CODE',
    References = 'REFERENCES',
    BackedBy = 'BACKED_BY',
    RevisionOf = 'REVISION_OF',
    OwnedBy = 'OWNED_BY',
    RelatedToIncident = 'RELATED_TO_INCIDENT',
    MonitoredBy = 'MONITORED_BY',
    IsPartOF = 'IS_PART_OF',
}

export type HandlePosition = 'T' | 'B' | 'L' | 'R';

export const GRAPH_CARD_TYPE = 'GraphCard';
export const CUSTOM_EDGE_TYPE = 'CustomEdge';
export const DEFAULT_MARKER_COLOR = '#b1b1b7';
