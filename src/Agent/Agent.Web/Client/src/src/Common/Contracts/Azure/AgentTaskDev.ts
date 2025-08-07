// Agent Task related types and interfaces (Dev version)
export interface TaskProgressUpdate {
    taskId: string;
    phase: string; // "initial_investigation", "forming_hypothesis", "conclusion"
    status: string; // "started", "in_progress", "completed", "failed"
    message: string;
    timestamp: string;
    summary?: string;
    conclusion?: any;
    hypothesisUpdate?: HypothesisTreeItem;
    hypothesisAction?: string; // "add", "update", "validate"
}

export enum AgentTaskType {
    IncidentInvestigation = 'IncidentInvestigation',
}

export enum AgentTaskStatus {
    InProgress = 'InProgress',
    Complete = 'Complete',
    Failed = 'Failed',
    Cancelled = 'Cancelled',
}

export interface AgentTaskStep {
    title: string;
    summary: string;
}

export interface AgentTaskShort {
    id: string;
    title: string;
    type: AgentTaskType;
    status: AgentTaskStatus;
}

export enum InitialInvestigationStatus {
    NotStarted = 'NotStarted',
    InProgress = 'InProgress',
    Complete = 'Complete',
}

export enum FormingHypothesisStatus {
    NotStarted = 'NotStarted',
    InProgress = 'InProgress',
    Complete = 'Complete',
}

export enum HypothesisStatus {
    Pending = 'Pending',
    Validating = 'Validating',
    Validated = 'Validated',
    Invalidated = 'Invalidated',
    Inconclusive = 'Inconclusive',
}

export interface InitialInvestigationStep {
    title: string;
    summary: string;
    status: InitialInvestigationStatus;
}

export interface GatheringContextProperties {
    steps: InitialInvestigationStep[];
    status: InitialInvestigationStatus;
}

export interface InitialInvestigationProperties {
    gatheringContext: GatheringContextProperties;
    summary: string;
    status: InitialInvestigationStatus;
    statusMessage: string;
}

export interface HypothesisStep {
    summary: string;
    details: string;
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

export interface FormingHypothesisProperties {
    hypotheses: HypothesisTreeItem[];
    status: FormingHypothesisStatus;
    statusMessage: string;
}

export interface ConclusionProperties {
    title: string;
    summary: string;
}

export interface IncidentInvestigationTaskProperties {
    initialInvestigation: InitialInvestigationProperties;
    formingHypothesis: FormingHypothesisProperties;
    conclusion: ConclusionProperties;
}

export interface AgentTask {
    id: string;
    title: string;
    steps?: AgentTaskStep[];
    properties?: IncidentInvestigationTaskProperties;
    type: AgentTaskType;
    status: AgentTaskStatus;
    threadId: string;
}
