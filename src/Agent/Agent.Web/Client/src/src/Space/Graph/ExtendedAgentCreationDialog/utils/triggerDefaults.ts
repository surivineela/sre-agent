import { IntlShape } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { IncidentPriority, IncidentType, TriggerDefaults } from '../types';
import { getDefaultIncidentTypeForPlatform, getDefaultPriorityForPlatform } from './incidentPlatforms';
import { DEFAULT_SCHEDULE_PRESET, SCHEDULE_PRESETS } from './schedule';

const getFallbackAgentDisplayName = (intl: IntlShape) => intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentFallbackName);

const getIncidentDefaultName = (intl: IntlShape, agentDisplayName: string) =>
    intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentDefaultName, { agentName: agentDisplayName });

const getIncidentDefaultInstructions = (intl: IntlShape, agentDisplayName: string) =>
    intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentDefaultInstructions, { agentName: agentDisplayName });

const getScheduledDefaultName = (intl: IntlShape, agentDisplayName: string) =>
    intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduledDefaultName, { agentName: agentDisplayName });

const getScheduledDefaultDescription = (intl: IntlShape, agentDisplayName: string) =>
    intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduledDefaultDescription, { agentName: agentDisplayName });

const getScheduledDefaultPrompt = (intl: IntlShape, agentDisplayName: string) =>
    intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduledDefaultPrompt, { agentName: agentDisplayName });

export const buildTriggerDefaults = (intl: IntlShape, agentDisplayName?: string, incidentPlatformType?: string): TriggerDefaults => {
    const fallbackName = agentDisplayName?.trim() || getFallbackAgentDisplayName(intl);

    return {
        mode: 'incident',
        strategy: 'quick',
        name: getIncidentDefaultName(intl, fallbackName),
        description: getScheduledDefaultDescription(intl, fallbackName),
        instructions: getIncidentDefaultInstructions(intl, fallbackName),
        incidentPriority: getDefaultPriorityForPlatform(incidentPlatformType),
        incidentType: getDefaultIncidentTypeForPlatform(incidentPlatformType),
        schedule: {
            preset: DEFAULT_SCHEDULE_PRESET,
            cronExpression: SCHEDULE_PRESETS[DEFAULT_SCHEDULE_PRESET].cron,
            naturalText: '',
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'UTC',
        },
    };
};

export const refreshScheduledDefaults = (intl: IntlShape, agentDisplayName?: string) => ({
    name: getScheduledDefaultName(intl, agentDisplayName || getFallbackAgentDisplayName(intl)),
    description: getScheduledDefaultDescription(intl, agentDisplayName || getFallbackAgentDisplayName(intl)),
    instructions: getScheduledDefaultPrompt(intl, agentDisplayName || getFallbackAgentDisplayName(intl)),
});

export const getIncidentDefaults = (intl: IntlShape, agentDisplayName?: string) => ({
    name: getIncidentDefaultName(intl, agentDisplayName || getFallbackAgentDisplayName(intl)),
    instructions: getIncidentDefaultInstructions(intl, agentDisplayName || getFallbackAgentDisplayName(intl)),
});

export const getIncidentDefaultsMeta = (incidentPlatformType?: string): { priority: IncidentPriority; type: IncidentType } => ({
    priority: getDefaultPriorityForPlatform(incidentPlatformType),
    type: getDefaultIncidentTypeForPlatform(incidentPlatformType),
});
