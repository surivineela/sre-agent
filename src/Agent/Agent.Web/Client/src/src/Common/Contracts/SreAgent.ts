export interface Agent {
    provisioningState: string;
    agentEndpoint: string;
    runningState: string;
    vnetConfiguration?: VnetConfiguration;
    knowledgeGraphConfiguration?: KnowledgeGraphConfiguration;
    outboundConnectionConfiguration?: OutboundConnectionConfiguration;
    mcpServers?: string[];
    logConfiguration?: {
      logAnalyticsConfiguration: {
        workspaceId: string;
        sharedKey: string;
      };
    };
  }
  
  export interface VnetConfiguration {
    subnetResourceId?: string;
    vNetGuid?: string;
  }
  
  export interface KnowledgeGraphConfiguration {
    identity?: string;
    managedResources?: string[];
  }
  
  export interface OutboundConnectionConfiguration {
    azureBotConfiguration?: {
      identity: string;
    };
  }

  export enum IncidentStatus {
    error = 'error',
    warning = 'warning',
    success = 'success',
  }
  
  export interface Thread {
    id: string;
    title: string;
    startMessage: Message;
    createdTimestamp: string;
    modifiedTimestamp: string;
    incidentStatus?: IncidentStatus;
  }
  
  export interface Message {
    id: string;
    timestamp: string;
    author: MessageAuthor;
    text: string;
  }
  
  export interface MessageAuthor {
    role: 'SREAgent' | 'User';
    userId: string;
    displayName: string;
  }
  
  export enum ActionStatus {
    Pending = 'Pending',
    InProgress = 'InProgress',
    Completed = 'Completed',
    Failed = 'Failed',
    All = 'All',
  }
  
  export interface Action {
    id: string;
    title: string;
    timeStamp: Date;
    status: ActionStatus;
  }
  