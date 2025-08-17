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
    Pending = 'Pending',
    Approved = 'Approved',
    Cancelled = 'Cancelled',
    PendingAuthorization = 'PendingAuthorization',
    Authorized = 'Authorized',
}

export interface AzCliExecution {
    id: string;
    command: string;
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

export interface KubectlExecution {
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
