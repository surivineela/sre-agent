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
    favorite?: boolean;
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
    agent = 'Agent',
    portal = 'Portal', // legacy
    dailyReport = 'DailyReport',
}
