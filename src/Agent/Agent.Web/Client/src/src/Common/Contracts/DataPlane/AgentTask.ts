export interface AgentTaskMetaData {
    id: string;
    status: AgentTaskStatus;
    title?: string;
    type: AgentTaskType;
    lastModified?: string;
}

export interface AgentTaskStepCommon {
    title: string;
    summary: string;

    // Remove them once the backend sends us proper data
    Title?: string; // For compatibility with older updates
    Summary?: string; // For compatibility with older updates
}

export interface AgentTask extends AgentTaskMetaData {
    steps?: AgentTaskStepCommon[];
    properties?: IncidentInvestigationTaskProperties;
    threadId: string;
}

export interface IncidentInvestigationTaskProperties {
    initialInvestigation: InitialInvestigationProperties;
    formingHypothesis: FormingHypothesisProperties;
    conclusion: AgentTaskStepCommon;
}

export interface InitialInvestigationProperties {
    gatheringContext: GatheringContextProperties;
    summary: string;
    status: InvestigationStatusCommon;
    statusMessage: string;
}

export interface FormingHypothesisProperties {
    hypotheses: HypothesisTreeItem[];
    status: InvestigationStatusCommon;
    statusMessage: string;
}

export interface GatheringContextProperties {
    steps: InitialInvestigationStep[];
    status: InvestigationStatusCommon;
}

export interface InitialInvestigationStep {
    title: string;
    summary: string;
    status: InvestigationStatusCommon;
}

export interface HypothesisTreeItem {
    id: string;
    title: string;
    description: string;
    children: HypothesisTreeItem[];
    status: HypothesisStatus;
    steps: HypothesisStep[];
    parentHypothesisDescription: string;
    statusMessage: string;

    // Remove them once the backend sends us proper data
    Id?: string; // For compatibility with older updates
    Title?: string; // For compatibility with older updates
    Description?: string; // For compatibility with older updates
    Status?: HypothesisStatus; // For compatibility with older updates
    Steps?: HypothesisStep[]; // For compatibility with older updates
    ParentHypothesisDescription?: string; // For compatibility with older updates
    StatusMessage?: string; // For compatibility with older updates
}

export enum AgentTaskStatus {
    InProgress = 'inprogress',
    Complete = 'complete',
    Failed = 'failed',
    Canceled = 'canceled',
}

export enum InvestigationStatusCommon {
    NotStarted = 'notstarted',
    InProgress = 'inprogress',
    Complete = 'complete',
}

export enum HypothesisStatus {
    Pending = 'pending',
    Validating = 'validating',
    Validated = 'validated',
    Invalidated = 'invalidated',
    Inconclusive = 'inconclusive',
}

export interface HypothesisStep {
    summary: string;
    details: string;
}

export enum AgentTaskType {
    IncidentInvestigation = 'IncidentInvestigation',
}

export enum TaskProgressStatus {
    Started = 'started',
    InProgress = 'in_progress',
    Completed = 'completed',
    Failed = 'failed',
}

export enum TreeNodeType {
    HypothesisRootGroup = 'hypothesisRootGroup',
    NodeGroup = 'nodeGroup',
    InitialInvestigation = 'initialInvestigation',
    Conclusion = 'conclusion',
    Hypothesis = 'hypothesis',
}

export type InvestigationTreeNodeStatus = InvestigationStatusCommon | TaskProgressStatus | HypothesisStatus | string;

export type InvestigationTreeNode = {
    id: string;
    title: string;
    description: string;
    status: InvestigationTreeNodeStatus; // More flexible to support both hypothesis and task progress statuses
    parentHypothesisDescription?: string;
    childrenIds: string[];
    expanded: boolean;
    isValidating: boolean;
    isLoading: boolean;
    parentId?: string;
    nodeType?: TreeNodeType; // To distinguish between different node types
    // Detailed step data for overlay display
    steps?: HypothesisStep[] | InitialInvestigationStep[];
    // For initial investigation phase, store the gathering context steps
    gatheringContextSteps?: InitialInvestigationStep[];
};

export interface InvestigationTreeState {
    nodes: Map<string, InvestigationTreeNode>;
    rootNodeIds: string[];
    phaseNodesStatus: Map<string, TaskProgressStatus>;
    hypothesisNodesStatus: Map<string, HypothesisStatus | string>;
    isVisible: boolean;
    isLoading: boolean;
    lastModified?: string;
}
