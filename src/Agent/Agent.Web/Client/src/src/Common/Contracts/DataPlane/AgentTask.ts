export interface AgentTaskMetaData {
    id: string;
    status: AgentTaskStatus;
    title?: string;
    type: AgentTaskType;
}

export interface AgentTaskStepCommon {
    title: string;
    summary: string;
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

export interface ConclusionProperties {
    title: string;
    summary: string;
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
    timestamp: string;
    summary?: string;
    conclusion?: any;
    hypothesisUpdate?: HypothesisTreeItem;
    hypothesisAction?: HypothesisAction;
}

export enum TaskProgressPhase {
    InitialInvestigation = 'initial_investigation',
    FormingHypothesis = 'forming_hypothesis',
    Conclusion = 'conclusion',
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
