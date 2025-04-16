import { createContext } from "react";

export type Subscription = {
    id: string;
    name: string;
}

export type ResourceExtended = {
    id: string; // the string has underscore
    name: string;
    type: string;
    dashboardUrl: string;
    appHealthInfo?: string[]; // Convert the json string to ScoreCardObject
    properties: {
        dashboardUrl: string[];
        resourceType: string[];
        resourceName: string[];
        resourceId: string[];
        subscriptionId: string[];
        resourceGroupName: string[];
        location: string[];
        runningStatus: string;
        appHealthInfo?: string[] // Convert the json string to ScoreCardObject
    }
}

export type Resource = {
    name: string;
    type: string;
    dashboardUrl: string;
    resourceId: string; // the string has underscore
    appHealthInfo?: string[]; // Convert the json string to ScoreCardObject
    subItems?: Resource[];
}

export type ScoreCardObject = {
    Costs: number; // 7 day window
    Availability?: number | null; // percentage
    Health: 'healthy' | 'unhealthy' | 'unknown';
    Transactions: number;
    AvgLatencyInMs?: number | null;
    AvgMemoryUsage?: number | null; // bytes
    AvgCpuUsage?: number | null; // percentage
    LastDataCaptureTimeStampInUTC?: string | Date;
    IsActive: boolean;
}

export type GraphNodeProperties = ResourceExtended | Resource;

export type GraphNodeObject = {
    id: string;
    name: string;
    type: 'subscription' | 'appGroup' | 'resource';
    subscriptionId: string;
    properties?: GraphNodeProperties;
}

export type GraphNode = GraphNodeObject & {
    isVisible: boolean;
    childrenIds?: string[];
}

export type GraphEdge = {
    label?: string;
    isVisible: boolean;
}

interface GraphContextProps {
    showSubresources: (parentGraphNode: GraphNode) => void;
    hideSubresources: (parentGraphNode: GraphNode) => void;
    areSubresourcesVisible: (parentGraphNode: GraphNode) => boolean;
    isLoadingSubresources: boolean;
    openPanel: (node: GraphNode) => void;
    closePanel: () => void;
    isPanelOpen: boolean;
    selectedNode?: GraphNode;
    hoverNode: (nodeId: string) => void;
    unHoverNode: () => void;
    nodesToHightlight: string[];
    edgesToHightlight: string[];
}

export const GraphContext = createContext<GraphContextProps>({
    showSubresources: () => { },
    hideSubresources: () => { },
    areSubresourcesVisible: () => false,
    isLoadingSubresources: false,
    openPanel: () => { },
    closePanel: () => { },
    isPanelOpen: false,
    hoverNode: () => { },
    unHoverNode: () => { },
    nodesToHightlight: [],
    edgesToHightlight: [],
});