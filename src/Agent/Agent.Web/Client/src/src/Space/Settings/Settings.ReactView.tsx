import { INavLinkGroup, initializeIcons, Nav, ThemeContext } from '@fluentui/react';
import type { Theme } from '@fluentui/theme';
import { FC, useContext, useEffect, useState } from 'react';
import GrafanaDashboard from '../../GrafanaDashboard/GrafanaDashboard.ReactView';
import { Settings_Tabs } from '../../Strings/SREResources.resjson';
import AccessControl from './AccessControl.ReactView';
import AgentDetails from './AgentDetails.ReactView';
import IncidentManagement from './IncidentManagement.ReactView';
import { navStyles, useSettingsStyles } from './Styles/Settings.styles';

enum SettingsKeys {
    IncidentManagement = 'incidentManagement',
    AccessControl = 'accessControl',
    AgentDetails = 'agentDetails',
    GrafanaInsights = 'grafanaInsights',
}

const navLinkGroups: INavLinkGroup[] = [
    {
        links: [
            {
                name: Settings_Tabs.incidentManagement,
                url: '',
                key: SettingsKeys.IncidentManagement,
            },
            {
                name: Settings_Tabs.grafanaInsights,
                url: '',
                key: SettingsKeys.GrafanaInsights,
            },
            {
                name: Settings_Tabs.accessControl,
                url: '',
                key: SettingsKeys.AccessControl,
            },
            {
                name: Settings_Tabs.agentDetails,
                url: '',
                key: SettingsKeys.AgentDetails,
            },
        ],
    },
];

const Settings: FC = () => {
    const [iconsInitialized, setIconsInitialized] = useState(false);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    const styles = useSettingsStyles();
    const theme = useContext(ThemeContext);

    const [selectedKey, setSelectedKey] = useState<SettingsKeys>(SettingsKeys.IncidentManagement);

    return (
        iconsInitialized && (
            <div style={styles.getNavContainerStyles(theme as Theme)}>
                <Nav
                    groups={navLinkGroups}
                    styles={navStyles}
                    selectedKey={selectedKey}
                    onLinkClick={(_, item) => {
                        if (item?.key && Object.values(SettingsKeys).includes(item.key as SettingsKeys)) {
                            setSelectedKey(item.key as SettingsKeys);
                        }
                    }}
                />
                <div style={styles.navPivotContainer}>
                    {selectedKey === SettingsKeys.IncidentManagement && <IncidentManagement />}
                    {selectedKey === SettingsKeys.GrafanaInsights && <GrafanaDashboard />}
                    {selectedKey === SettingsKeys.AccessControl && <AccessControl />}
                    {selectedKey === SettingsKeys.AgentDetails && <AgentDetails />}
                </div>
            </div>
        )
    );
};

export default Settings;
