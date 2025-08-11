export interface AgentTaskMetaData {
    id: string;
    status: AgentTaskStatus;
    title?: string;
    type: AgentTaskType;
    timestamp?: string;
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

export interface TaskProgressUpdate {
    taskId: string;
    phase: TaskProgressPhase;
    status: TaskProgressStatus;
    message: string;
    summary?: string;
    conclusion?: AgentTaskStepCommon;
    hypothesisUpdate?: HypothesisTreeItem;
    hypothesisAction?: HypothesisAction;
    timestamp?: string;

    // Remove them once the backend sends us proper data
    TaskId?: string; // For compatibility with older updates
    Phase?: TaskProgressPhase; // For compatibility with older updates
    Status?: TaskProgressStatus; // For compatibility with older updates
    Message?: string; // For compatibility with older updates
    Summary?: string; // For compatibility with older updates
    Conclusion?: AgentTaskStepCommon; // For compatibility with older updates
    HypothesisUpdate?: HypothesisTreeItem; // For compatibility with older updates
    HypothesisAction?: HypothesisAction; // For compatibility with older updates
    Timestamp?: string; // For compatibility with older updates
}

export enum TaskProgressPhase {
    InitialInvestigation = 'initial_investigation',
    FormingHypothesis = 'forming_hypothesis',
    Conclusion = 'conclusion',
    ValidatingHypothesis = 'validating_hypothesis',
}

export enum TaskProgressStatus {
    Started = 'started',
    InProgress = 'in_progress',
    Completed = 'completed',
    Failed = 'failed',
}

export enum HypothesisAction {
    Add = 'add',
    Update = 'update',
    Validate = 'validate',
}

export enum TreeNodeType {
    Phase = 'phase',
    Hypothesis = 'hypothesis',
}

export interface InvestigationTreeNode {
    id: string;
    title: string;
    description: string;
    status: InvestigationStatusCommon | TaskProgressStatus | HypothesisStatus | string; // More flexible to support both hypothesis and task progress statuses
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
}

export interface InvestigationTreeState {
    nodes: Map<string, InvestigationTreeNode>;
    rootNodeIds: string[];
    phaseNodesStatus: Map<string, TaskProgressStatus>;
    hypothesisNodesStatus: Map<string, HypothesisStatus | string>;
    isVisible: boolean;
    isLoading: boolean;
    timestamp?: string;
}
