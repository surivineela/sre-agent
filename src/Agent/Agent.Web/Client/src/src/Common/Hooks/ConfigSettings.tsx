import { useMemo } from 'react';
import { Location, useLocation } from 'react-router-dom';
import Url from '../Helpers/Url';

export enum SettingNames {
    ShowAgentModeForThread = 'showAgentModeForThread',
    Streaming = 'streaming',
    ConsolidatedCreate = 'consolidatedCreate',
    DataConnections = 'dataConnections',
}

const configSettings: Record<string, Partial<Record<SettingNames, any>>> = {
    default: {
        [SettingNames.Streaming]: true,
        [SettingNames.ConsolidatedCreate]: true,
    },
    'portal.azure.com': {},
    'ms.portal.azure.com': {
        [SettingNames.DataConnections]: true,
    },
    localhost: {
        [SettingNames.ShowAgentModeForThread]: true,
        [SettingNames.DataConnections]: true,
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
