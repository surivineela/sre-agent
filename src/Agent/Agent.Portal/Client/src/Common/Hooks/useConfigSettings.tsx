import { useMemo } from 'react';
import { Location, useLocation } from 'react-router-dom';

export enum SettingNames {
    /** 1 Error -> 5 Verbose */
    LogLevel = 'loglevel',
    /** Comma-separated TelemetrySources */
    LogSource = 'logsource',
    /** For AgentIFrame feature flags */
    Ext = 'ext',
    /** For agent site deep link */
    SreLink = 'srelink',
    /** Use local agent site UX and backend */
    SreLocal = 'sre_local',
    /** Use local agent site UX with prod backend */
    SreUxLocal = 'sre_ux_local',
    /** Only used by unit tests */
    ForUnitTests = 'forUnitTests',
}

const configSettings: Record<string, Partial<Record<SettingNames, any>>> = {
    default: {},
    'sre.azure.com': {},
    'int.sre.azure.com': {},
    localhost: {
        [SettingNames.ForUnitTests]: true,
    },
};

const siteHostName = window.location.hostname.toLowerCase();

let mergedConfig = {
    ...configSettings['default'],
};

if (siteHostName.startsWith('localhost')) {
    mergedConfig = {
        ...mergedConfig,
        ...configSettings['localhost'],
    };
} else if (configSettings[siteHostName]) {
    mergedConfig = {
        ...mergedConfig,
        ...configSettings[siteHostName],
    };
}

export const getFeatureFlag = (settingName: SettingNames, location?: Location<any>): string | boolean | null => {
    const defaultedLocation = location ?? window.location;
    const query = new URLSearchParams(defaultedLocation.search.toLowerCase());
    const queryValue = query.get(settingName.toLowerCase());

    if (queryValue === 'true') {
        return true;
    } else if (queryValue === 'false') {
        return false;
    } else {
        return queryValue;
    }
};

/** Non-hook version that can be used outside React Router context */
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
