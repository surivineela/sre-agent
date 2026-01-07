import { AgentTaskMetaData } from './AgentTask';
import { TodoInfo } from './TodoPlan';

export type ChatMessageError = 'PermissionDenied' | 'UnknownError';

export type MessageType =
    | 'chart'
    | 'image'
    | 'mermaid'
    | 'azcli'
    | 'kubectl'
    | 'approval'
    | 'psql'
    | 'deepinvestigation'
    | 'memorysearch'
    | 'knowledgegraph'
    | 'todoplan'
    | 'trajectoryinsight'
    | 'reasoning'
    | null;

export interface Message {
    id: string;
    timeStamp: string;
    author: MessageAuthor;
    text: string;
    title: string | null | undefined;

    // special message type
    approval: Approval | null | undefined;
    azCliExecution: AzCliExecution | null | undefined;
    kubectlExecution: KubectlExecution | null | undefined;
    psqlExecution: PsqlExecution | null | undefined;
    isDailyReport: boolean | null | undefined;
    changeDiff: ChangeDiffViewer | null | undefined;
    agentTaskInfo: AgentTaskMetaData | null | undefined;
    memorySearchResult: MemorySearchResult | null | undefined;
    knowledgeGraphSearchResult: KnowledgeGraphSearchResult | null | undefined;
    todoInfo: TodoInfo | null | undefined;

    isComplete: boolean | null | undefined;
    isImageContent: boolean | null | undefined;
    messageType: MessageType | null | undefined;
    memoryCommand?: 'remember' | 'retrieve' | null | undefined;
}

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

export type MessageRole = 'SREAgent' | 'User';

export interface MessageAuthor {
    role: MessageRole;
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
    requiredScopes?: string;
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
    requiredScopes?: string;
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

export interface KnowledgeGraphSearchResult {
    query: string;
    entities: KnowledgeGraphEntity[];
    relations: KnowledgeGraphRelation[];
    timestamp: string;
    totalEntities: number;
    totalRelations: number;
}

export interface KnowledgeGraphEntity {
    name: string;
    entityType: string;
    observations: string[];
}

export interface KnowledgeGraphRelation {
    from: string;
    to: string;
    relationType: string;
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
