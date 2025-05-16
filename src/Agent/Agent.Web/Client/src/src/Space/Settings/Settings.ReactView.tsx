import { INavLinkGroup, initializeIcons, Nav } from '@fluentui/react';
import { FC, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router';
import GrafanaDashboard from '../../GrafanaDashboard/GrafanaDashboard.ReactView';
import { SettingsTabResources } from '../../Strings/SREAgentResources';
import AccessControl from './AccessControl.ReactView';
import Basics from './Basics.ReactView';
import IncidentManagement from './IncidentManagement.ReactView';
import ManagedResources from './ManagedResources.ReactView';
import { navStyles, useSettingsStyles } from './Styles/Settings.styles';

enum SettingsKeys {
    IncidentManagement = 'incidentManagement',
    AccessControl = 'accessControl',
    Basics = 'basics',
    GrafanaDashboard = 'grafanaDashboard',
    managedResources = 'managedResourcesGroups',
}

const Settings: FC = () => {
    const styles = useSettingsStyles();
    const intl = useIntl();
    const { menuItem } = useParams();
    const location = useLocation();
    const navigate = useNavigate();

    const [iconsInitialized, setIconsInitialized] = useState(false);
    const selectedKey = useMemo(() => {
        return (
            Object.values(SettingsKeys).find(settingsKey => settingsKey.toLocaleLowerCase() === menuItem?.toLocaleLowerCase()) ||
            SettingsKeys.Basics
        );
    }, [menuItem]);

    const navLinkGroups = useMemo<INavLinkGroup[]>(
        () => [
            {
                links: [
                    {
                        name: intl.formatMessage(SettingsTabResources.basics),
                        url: '',
                        key: SettingsKeys.Basics,
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
                        name: intl.formatMessage(SettingsTabResources.grafanaDashboard),
                        url: '',
                        key: SettingsKeys.GrafanaDashboard,
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
            <div style={styles.navContainerStyles}>
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
                    {selectedKey === SettingsKeys.Basics && <Basics />}
                    {selectedKey === SettingsKeys.managedResources && <ManagedResources />}
                    {selectedKey === SettingsKeys.IncidentManagement && <IncidentManagement />}
                    {selectedKey === SettingsKeys.GrafanaDashboard && <GrafanaDashboard />}
                    {selectedKey === SettingsKeys.AccessControl && <AccessControl />}
                </div>
            </div>
        )
    );
};

export default Settings;
