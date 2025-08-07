import React from 'react';
import { AgentTask, HypothesisStep, InitialInvestigationStep, TaskProgressUpdate } from '../../Common/Contracts/Azure/AgentTaskDevTypes';

export interface InvestigationTreeNode {
    id: string;
    title: string;
    description: string;
    status: string; // More flexible to support both hypothesis and task progress statuses
    parentHypothesisDescription?: string;
    children: InvestigationTreeNode[];
    expanded: boolean;
    isValidating: boolean;
    isLoading: boolean;
    parentId?: string;
    nodeType?: 'phase' | 'hypothesis'; // To distinguish between different node types
    // Detailed step data for overlay display
    steps?: HypothesisStep[] | InitialInvestigationStep[];
    // For initial investigation phase, store the gathering context steps
    gatheringContextSteps?: InitialInvestigationStep[];
}

export interface InvestigationTreeState {
    nodes: Map<string, InvestigationTreeNode>;
    rootNodes: InvestigationTreeNode[];
    currentTask?: {
        taskId: string;
        phase: string;
        status: string;
        message: string;
    };
    isVisible: boolean;
    isLoading: boolean;
}

export interface InvestigationTreeContextProps {
    treeState: InvestigationTreeState;
    updateFromTaskProgress: (update: TaskProgressUpdate) => void;
    updateFromTaskUpdate: (task: AgentTask) => void;
    loadSavedAgentTasks: (threadId: string) => Promise<void>;
    toggleNodeExpanded: (nodeId: string) => void;
    clearTree: () => void;
    showTree: () => void;
    hideTree: () => void;
    resetTree: () => void;
    forceRefresh: () => void;
}

const defaultTreeState: InvestigationTreeState = {
    nodes: new Map(),
    rootNodes: [],
    currentTask: undefined,
    isVisible: false,
    isLoading: false,
};

export const InvestigationTreeContext = React.createContext<InvestigationTreeContextProps>({
    treeState: defaultTreeState,
    updateFromTaskProgress: () => {},
    updateFromTaskUpdate: () => {},
    loadSavedAgentTasks: async () => {},
    toggleNodeExpanded: () => {},
    clearTree: () => {},
    showTree: () => {},
    hideTree: () => {},
    resetTree: () => {},
    forceRefresh: () => {},
});
