import { IntlShape } from 'react-intl';
import { IncidentHandler } from '../../../Common/Contracts/Azure/IncidentHandler';
import { ExtendedAgent, ExtendedConnector, ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { ScheduledTask } from '../../Contracts/ScheduledTasks';

export type EntityType = 'agent' | 'tool' | 'connector' | 'trigger';
export type Step = 1 | 2 | 3;
export type TriggerMode = 'incident' | 'scheduled';
export type TriggerStrategy = 'quick' | 'existing';
export type SchedulePresetKey = 'hourly' | 'every15m' | 'daily' | 'weekly' | 'monthly' | 'workdays' | 'custom';

export type IncidentPriority = 'Sev0' | 'Sev1' | 'Sev2' | 'Sev3' | 'Sev4';
export type IncidentType = 'LiveSite' | 'Maintenance' | 'Security' | 'Other';

export interface TriggerScheduleState {
    preset: SchedulePresetKey;
    cronExpression: string;
    naturalText: string;
    timezone: string;
    startTime?: string;
}

export interface TriggerState {
    mode: TriggerMode;
    strategy: TriggerStrategy;
    agentName?: string;
    agentDisplayName?: string;
    name: string;
    description: string;
    incidentPriority: IncidentPriority;
    incidentType: IncidentType;
    instructions: string;
    schedule: TriggerScheduleState;
    existingId?: string;
    existingName?: string;
}

export interface TriggerValidationState {
    agent?: string;
    name?: string;
    instructions?: string;
    cron?: string;
    existing?: string;
}

export type TriggerDirtyField = 'name' | 'description' | 'instructions' | 'schedule';
export type TriggerUserPatch = Partial<Omit<TriggerState, 'schedule'>> & { schedule?: Partial<TriggerScheduleState> };

export interface TriggerSubmitRequest {
    mode: TriggerMode;
    strategy: TriggerStrategy;
    agentName: string;
    agentDisplayName?: string;
    name: string;
    description?: string;
    severity?: IncidentPriority;
    incidentType?: IncidentType;
    instructions?: string;
    cronExpression?: string;
    startTime?: string;
    existingId?: string;
    existingName?: string;
    schedule?: {
        kind: 'preset' | 'cron' | 'natural';
        preset?: Exclude<SchedulePresetKey, 'custom'>;
        cron?: string;
        natural?: string;
        timezone: string;
        start?: string;
    };
}

export interface CreationState {
    entityType?: EntityType;
    step: Step;
    agent?: Partial<ExtendedAgent>;
    tool?: Partial<ExtendedTool>;
    connector?: Partial<ExtendedConnector>;
    trigger?: TriggerState;
    triggerValidation?: TriggerValidationState;
    toolTest?: ToolTestState;
}

export interface LinkContext {
    sourceAgentName: string;
    targetType: 'agent' | 'tool';
}

export interface TriggerCardConfig {
    isLoading: boolean;
    incidentHandlersCount: number | null;
    scheduledTasksCount: number | null;
    hasScheduledTasksFeature: boolean;
    hasIncidentHandlersFeature?: boolean;
}

export interface ExtendedAgentCreationDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    onSubmit: (
        data: Partial<ExtendedAgent> | Partial<ExtendedTool> | Partial<ExtendedConnector> | TriggerSubmitRequest,
        type: EntityType
    ) => Promise<void>;
    existingAgents: ExtendedAgent[];
    existingTools: ExtendedTool[];
    existingConnectors: ExtendedConnector[];
    systemTools: SystemTool[];
    existingIncidentHandlers?: IncidentHandler[];
    existingScheduledTasks?: ScheduledTask[];
    initialEntityType?: EntityType;
    contextNotice?: { intent?: 'info' | 'success' | 'warning' | 'error'; message: string };
    linkContext?: LinkContext;
    triggerConfig?: TriggerCardConfig;
    onTriggerSubmit?: (data: TriggerSubmitRequest) => Promise<void>;
    onTriggerNavigate?: (destination: 'incidentManagement' | 'scheduledTasks') => void;
    onConnectorNavigate?: () => void;
}

export interface TriggerDefaults {
    mode: TriggerMode;
    strategy: TriggerStrategy;
    name: string;
    description: string;
    instructions: string;
    incidentPriority: IncidentPriority;
    incidentType: IncidentType;
    schedule: TriggerScheduleState;
}

export interface TriggerStateController {
    trigger: TriggerState;
    validation: TriggerValidationState;
    setTrigger: (updater: (prev: TriggerState) => TriggerState) => void;
    setValidation: (updater: (prev: TriggerValidationState) => TriggerValidationState) => void;
    applyAgentDefaults: (agentName?: string, agentDisplayName?: string) => void;
    reset: (overrides?: Partial<TriggerState>) => void;
    updateFromUser: (patch: TriggerUserPatch, dirtyFields?: TriggerDirtyField[]) => void;
}

export type TriggerDefaultsFactory = (intl: IntlShape, agentDisplayName?: string) => TriggerDefaults;

// Tool Test Types
export type ToolTestStatus = 'idle' | 'running' | 'success' | 'error';

export interface ToolTestState {
    status: ToolTestStatus;
    errorMessage?: string;
    lastRunFingerprint?: string | null;
}
