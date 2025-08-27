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
    isDailyReport?: boolean;
    changeDiff?: ChangeDiffViewer;
}

export interface Message extends MessageMetaData {
    text: string;
    approval?: Approval;
    azCliExecution?: AzCliExecution;
    kubectlExecution?: KubectlExecution;
    isDailyReport?: boolean;
    changeDiff?: ChangeDiffViewer;
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
