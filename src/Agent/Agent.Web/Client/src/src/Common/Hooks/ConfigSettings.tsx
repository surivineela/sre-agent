import { useMemo } from 'react';
import { Location, useLocation } from 'react-router-dom';
import Url from '../Helpers/Url';

export enum SettingNames {
    ShowAgentModeForThread = 'showAgentModeForThread',
    ConsolidatedCreate = 'consolidatedCreate',
    DataConnectors = 'dataConnectors',
    Connectors = 'connectors',
    ShowScheduledTasksTab = 'showScheduledTasksTab',
    /** Only used by unit tests */
    ForUnitTests = 'forUnitTests',
    ShowSubAgentsItemInSettings = 'showSubAgentsItemInSettings',
    McpServer = 'McpServer',
    ShowThreadTraceUI = 'showThreadTraceUI',
    AllowMetaAgentOverride = 'allowMetaAgentOverride',
    ShowPermissionsInSettings = 'showPermissionsInSettings',
    ShowIncidentTriggerWithLearnings = 'showIncidentTriggerWithLearnings',
    ShowOnboardingWizard = 'showOnboardingWizard',
    /** Use for onboarding experience testing. This will be removed when the feature is ready */
    Overview = 'overview',
    ShowDefaultModelPicker = 'showDefaultModelPicker',
    KnowledgeSettings = 'knowledgeSettings',
}

const configSettings: Record<string, Partial<Record<SettingNames, any>>> = {
    default: {
        [SettingNames.ConsolidatedCreate]: true,
        [SettingNames.Connectors]: true,
    },
    'portal.azure.com': {},
    'ms.portal.azure.com': {
        [SettingNames.ShowThreadTraceUI]: true,
        [SettingNames.Connectors]: true,
        [SettingNames.AllowMetaAgentOverride]: true,
        [SettingNames.ShowPermissionsInSettings]: true,
        [SettingNames.ShowIncidentTriggerWithLearnings]: true,
    },
    'sre.azure.com': {
        [SettingNames.ShowThreadTraceUI]: true,
        [SettingNames.AllowMetaAgentOverride]: true,
    },
    localhost: {
        [SettingNames.ShowAgentModeForThread]: true,
        [SettingNames.ForUnitTests]: true,
        [SettingNames.ShowThreadTraceUI]: true,
        [SettingNames.AllowMetaAgentOverride]: true,
        [SettingNames.ShowPermissionsInSettings]: true,
        [SettingNames.Connectors]: true,
        [SettingNames.ShowIncidentTriggerWithLearnings]: true,
        [SettingNames.ShowOnboardingWizard]: true,
        [SettingNames.Overview]: true,
        [SettingNames.KnowledgeSettings]: true,
    },
};

const siteHostName = Url.getHostName(decodeURI(window.location.href)).toLowerCase();
const shellHostName = Url.getHostName(Url.getParameterByName(decodeURI(window.location.href), 'shellUrl')?.toLowerCase()) || '';

let mergedConfig = {
    ...configSettings['default'],
};

if (siteHostName.startsWith('localhost')) {
    mergedConfig = {
        ...mergedConfig,
        ...configSettings['localhost'],
    };
} else if (configSettings[shellHostName]) {
    mergedConfig = {
        ...mergedConfig,
        ...configSettings[shellHostName],
    };
}

const getFeatureFlag = (settingName: SettingNames, location: Location<any>) => {
    const query = new URLSearchParams(location.search.toLowerCase() || window.location.search.toLowerCase());
    if (settingName) {
        if (query.get(settingName.toLowerCase()) === 'true') {
            return true;
        } else if (query.get(settingName.toLowerCase()) === 'false') {
            return false;
        } else {
            return null;
        }
    }
};

// Non-hook version that can be used outside React Router context
export const getConfigSetting = (settingName: SettingNames): boolean | null => {
    // Try to get from URL parameters first
    if (typeof window !== 'undefined' && window.location) {
        const urlParams = new URLSearchParams(window.location.search);
        const paramValue = urlParams.get(settingName.toLowerCase());
        if (paramValue === 'true') {
            return true;
        } else if (paramValue === 'false') {
            return false;
        }
    }

    // Fallback to merged config
    return mergedConfig[settingName] !== undefined ? mergedConfig[settingName] : null;
};

export const useConfigSetting = (settingName: SettingNames) => {
    const location = useLocation();

    const configSetting = useMemo(() => {
        const featureFlag = getFeatureFlag(settingName, location);

        if (featureFlag !== null && featureFlag !== undefined) {
            return featureFlag;
        }

        return mergedConfig[settingName] !== undefined ? mergedConfig[settingName] : null;
    }, [location, settingName]);

    return configSetting;
};
