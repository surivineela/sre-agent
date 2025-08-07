import { AgentTaskMetaData } from './AgentTask';
import { Message } from './Message';

export interface Thread {
    id: string;
    title: string;
    startMessage: Message;
    createdTimestamp: string;
    modifiedTimestamp: string;
    lastMessage: Message;
    status?: AgentStatus;
    incidentSource?: any;
    source?: ThreadSource;
    lastReadTime?: string;
    agentMode?: string;
    agentTasks?: AgentTaskMetaData[];
}

export interface AgentStatus {
    actionsStatus?: {
        hasCriticalActions: boolean;
        hasWarningActions: boolean;
    };
    incidentStatus?: {
        incidentId: string;
        status: string;
    };
}

export enum ThreadSource {
    conversation = 'Conversation',
    incident = 'Incident',
    welcomeMessage = 'WelcomeMessage',
    Portal = 'Portal', // legacy
}
