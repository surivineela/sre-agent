import { AgentTaskMetaData } from './AgentTask';
import { TodoInfo } from './TodoInfo';

export interface MessageMetaData {
    id: string;
    timeStamp: string;
    author: MessageAuthor;
    title?: string;
}

export type ChatMessageError = 'PermissionDenied' | 'UnknownError';

export interface MessageContent {
    text: string;
    approval?: Approval;
    azCliExecution?: AzCliExecution;
    kubectlExecution?: KubectlExecution;
    psqlExecution?: PsqlExecution;
    isDailyReport?: boolean;
    changeDiff?: ChangeDiffViewer;
    agentTaskInfo?: AgentTaskMetaData;
    memorySearchResult?: MemorySearchResult;
    todoInfo?: TodoInfo;
}

export interface Message extends MessageMetaData, MessageContent {}

export interface Approval {
    id: string;
    title: string;
    description: string;
    status: ApprovalDecision;
    createdTimestamp: string;
    decisionTimestamp?: string;
    decisionUser?: MessageAuthor;
    oboTokenScope?: string;
}

export interface MessageAuthor {
    role: 'SREAgent' | 'User';
    userId: string;
    displayName: string;
}

export enum ApprovalDecision {
    /** Will use agent identity */
    Pending = 'Pending',
    /** Will use user identity temporarily */
    PendingAuthorization = 'PendingAuthorization',
    Approved = 'Approved',
    Authorized = 'Authorized',
    Cancelled = 'Cancelled',
}

export enum ExecutionStatus {
    /** Will use agent identity */
    Pending = 'Pending',
    /** Will use user identity temporarily */
    PendingAuthorization = 'PendingAuthorization',
    Running = 'Running',
    Completed = 'Completed',
    Failed = 'Failed',
    Cancelled = 'Cancelled',
}

export interface AzCliExecution {
    id: string;
    command: string;
    description: string;
    status: ExecutionStatus;
    output?: string;
    error?: string;
    createdTimestamp: string;
    startedTimestamp?: string;
    completedTimestamp?: string;
    executedBy?: {
        displayName: string;
        userId: string;
        role: string;
    };
}

export interface KubectlExecution {
    id: string;
    command: string;
    stdin?: string;
    description: string;
    status: ExecutionStatus;
    output?: string;
    error?: string;
    createdTimestamp: string;
    startedTimestamp?: string;
    completedTimestamp?: string;
    executedBy?: {
        displayName: string;
        userId: string;
        role: string;
    };
}

export interface PsqlExecution {
    id: string;
    command: string;
    stdin?: string;
    description: string;
    status: 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled' | 'PendingAuthorization';
    output?: string;
    error?: string;
    createdTimestamp: string;
    startedTimestamp?: string;
    completedTimestamp?: string;
    executedBy?: {
        displayName: string;
        userId: string;
        role: string;
    };
}

export interface ChangeDiffViewer {
    id: string;
    title: string;
    description: string;
    correlationId: string;
    resourceId: string;
    changes: ChangeDiffItem[];
}

export interface ChangeDiffItem {
    changeTime: string;
    targetResourceId: string;
    changeType: string;
    changedBy: string;
    clientType: string;
    changesJson: string;
    previousSnapshotId?: string;
    newSnapshotId?: string;
}

export interface MemorySearchResult {
    resourceId: string;
    symptoms: string;
    sameResourceTrajectories: TrajectoryResult[];
    similarSymptomsTrajectories: TrajectoryResult[];
    userMemories: string[];
    documents: DocumentResult[];
    timestamp: string;
    totalResults: number;
}

export interface TrajectoryResult {
    id: string;
    title: string;
    initialSymptoms: string;
    symptomsObserved: string;
    stepsFollowed: string;
    rootCause: string;
    pitfalls: string;
}

export interface DocumentResult {
    id: string;
    title: string;
    documentType: string;
    summary: string | null;
    content: string | null;
    url: string | null;
    relevanceScore: number | null;
}
