import { INavLinkGroup, initializeIcons, Nav, ThemeContext } from '@fluentui/react';
import type { Theme } from '@fluentui/theme';
import { FC, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router';
import GrafanaDashboard from '../../GrafanaDashboard/GrafanaDashboard.ReactView';
import { SettingsTabResources } from '../../Strings/SREAgentResources';
import AccessControl from './AccessControl.ReactView';
import AgentDetails from './AgentDetails.ReactView';
import IncidentManagement from './IncidentManagement.ReactView';
import ManagedResources from './ManagedResources.ReactView';
import { navStyles, useSettingsStyles } from './Styles/Settings.styles';

enum SettingsKeys {
    IncidentManagement = 'incidentManagement',
    AccessControl = 'accessControl',
    AgentDetails = 'agentDetails',
    GrafanaInsights = 'grafanaInsights',
    managedResources = 'managedResourcesGroups',
}

const Settings: FC = () => {
    const styles = useSettingsStyles();
    const theme = useContext(ThemeContext);
    const intl = useIntl();
    const { menuItem } = useParams();
    const location = useLocation();
    const navigate = useNavigate();

    const [iconsInitialized, setIconsInitialized] = useState(false);
    const selectedKey = useMemo(() => {
        return (
            Object.values(SettingsKeys).find(settingsKey => settingsKey.toLocaleLowerCase() === menuItem?.toLocaleLowerCase()) ||
            SettingsKeys.AgentDetails
        );
    }, [menuItem]);

    const navLinkGroups = useMemo<INavLinkGroup[]>(
        () => [
            {
                links: [
                    {
                        name: intl.formatMessage(SettingsTabResources.agentDetails),
                        url: '',
                        key: SettingsKeys.AgentDetails,
                    },
                    {
                        name: intl.formatMessage(SettingsTabResources.managedResources),
                        url: '',
                        key: SettingsKeys.managedResources,
                    },
                    {
                        name: intl.formatMessage(SettingsTabResources.incidentManagement),
                        url: '',
                        key: SettingsKeys.IncidentManagement,
                    },
                    {
                        name: intl.formatMessage(SettingsTabResources.grafanaInsights),
                        url: '',
                        key: SettingsKeys.GrafanaInsights,
                    },
                    {
                        name: intl.formatMessage(SettingsTabResources.accessControl),
                        url: '',
                        key: SettingsKeys.AccessControl,
                    },
                ],
            },
        ],
        [intl]
    );

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    return (
        iconsInitialized && (
            <div style={styles.getNavContainerStyles(theme as Theme)}>
                <Nav
                    groups={navLinkGroups}
                    styles={navStyles}
                    selectedKey={selectedKey}
                    onLinkClick={(_, item) => {
                        if (item?.key && Object.values(SettingsKeys).includes(item.key as SettingsKeys) && item.key !== selectedKey) {
                            navigate({ ...location, pathname: `/views/settings/${item.key}` });
                        }
                    }}
                />
                <div style={styles.navPivotContainer}>
                    {selectedKey === SettingsKeys.AgentDetails && <AgentDetails />}
                    {selectedKey === SettingsKeys.managedResources && <ManagedResources />}
                    {selectedKey === SettingsKeys.IncidentManagement && <IncidentManagement />}
                    {selectedKey === SettingsKeys.GrafanaInsights && <GrafanaDashboard />}
                    {selectedKey === SettingsKeys.AccessControl && <AccessControl />}
                </div>
            </div>
        )
    );
};

export default Settings;
