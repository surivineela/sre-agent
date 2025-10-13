import { initializeIcons } from '@fluentui/react';
import { NavDrawer, NavDrawerBody, NavItem } from '@fluentui/react-components';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { useKnowledgeBaseConfig } from '../../Common/Hooks/UseKnowledgeBaseConfig';
import GrafanaDashboard from '../../GrafanaDashboard/GrafanaDashboard.ReactView';
import { SettingsTabResources } from '../../Strings/SREAgentResources';
import { useSharedNavDrawerStyles } from '../Styles/Navigation.styles';
import AccessControl from './AccessControl.ReactView';
import Basics from './Basics.ReactView';
import DataConnectors from './DataConnectors.ReactView';
import Identity from './Identity.ReactView';
import KnowledgeBase from './KnowledgeBase.ReactView';
import ManagedResources from './ManagedResources.ReactView';
import { useSettingsStyles } from './Styles/Settings.styles';
import SubAgents from './SubAgents.ReactView';

export enum SettingsKeys {
    AccessControl = 'accessControl',
    Basics = 'basics',
    GrafanaDashboard = 'grafanaDashboard',
    managedResources = 'managedResourcesGroups',
    DataConnectors = 'dataConnectors',
    Identity = 'identity',
    KnowledgeBase = 'knowledgeBase',
    SubAgents = 'subAgents',
}

// Settings uses shared navigation styles with specific maxWidth
const useSettingsNavStyles = () => useSharedNavDrawerStyles();

const Settings: FC = () => {
    const styles = useSettingsStyles();
    const navigationStyles = useSettingsNavStyles();
    const intl = useIntl();
    const { menuItem } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const showDataConnectors = useConfigSetting(SettingNames.DataConnectors);
    const showKnowledgeBase = useKnowledgeBaseConfig();
    const showSubAgents = useConfigSetting(SettingNames.ShowSubAgentsItemInSettings);

    const { logAmplitudeNavigationEvent } = useAzPortalContext();

    const [iconsInitialized, setIconsInitialized] = useState(false);
    const selectedKey = useMemo(() => {
        return (
            Object.values(SettingsKeys).find(settingsKey => settingsKey.toLocaleLowerCase() === menuItem?.toLocaleLowerCase()) ||
            SettingsKeys.Basics
        );
    }, [menuItem]);

    const navItems = useMemo(() => {
        const items = [
            {
                name: intl.formatMessage(SettingsTabResources.basics),
                key: SettingsKeys.Basics,
            },
            {
                name: intl.formatMessage(SettingsTabResources.managedResources),
                key: SettingsKeys.managedResources,
            },
            {
                name: intl.formatMessage(SettingsTabResources.grafanaDashboard),
                key: SettingsKeys.GrafanaDashboard,
            },
        ];

        if (showDataConnectors) {
            items.push({
                name: intl.formatMessage(SettingsTabResources.dataConnectors),
                key: SettingsKeys.DataConnectors,
            });
        }

        if (showKnowledgeBase) {
            items.push({
                name: intl.formatMessage(SettingsTabResources.knowledgeBase),
                key: SettingsKeys.KnowledgeBase,
            });
        }

        items.push(
            {
                name: intl.formatMessage(SettingsTabResources.accessControl),
                key: SettingsKeys.AccessControl,
            },
            {
                name: intl.formatMessage(SettingsTabResources.identity),
                key: SettingsKeys.Identity,
            }
        );

        if (showSubAgents) {
            items.push({
                name: intl.formatMessage(SettingsTabResources.subAgents),
                key: SettingsKeys.SubAgents,
            });
        }

        return items;
    }, [intl, showDataConnectors, showKnowledgeBase, showSubAgents]);

    const onNavigationClick = useCallback(
        (navKey: string) => {
            if (navKey && Object.values(SettingsKeys).includes(navKey as SettingsKeys) && navKey !== selectedKey) {
                logAmplitudeNavigationEvent({
                    targetType: 'tab',
                    targetAction: 'tabItem',
                    targetName: navKey,
                    targetFriendlyName: navKey,
                });

                navigate({ ...location, pathname: `/views/settings/${navKey}` });
            }
        },
        [selectedKey, location, navigate, logAmplitudeNavigationEvent]
    );

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    return (
        iconsInitialized && (
            <div style={styles.navContainerStyles}>
                <NavDrawer
                    defaultSelectedValue={selectedKey || SettingsKeys.Basics}
                    defaultSelectedCategoryValue=""
                    open={true}
                    type="inline"
                    className={navigationStyles.drawer}
                    style={{ paddingRight: '16px' }}
                >
                    <NavDrawerBody className={navigationStyles.drawerBody}>
                        {navItems.map(navItem => (
                            <NavItem
                                key={navItem.key}
                                value={navItem.key}
                                href=""
                                onClick={() => onNavigationClick(navItem.key)}
                                className={navigationStyles.item}
                            >
                                <span className={navigationStyles.itemText}>{navItem.name}</span>
                            </NavItem>
                        ))}
                    </NavDrawerBody>
                </NavDrawer>
                <div style={styles.navPivotContainer}>
                    {selectedKey === SettingsKeys.Basics && <Basics />}
                    {selectedKey === SettingsKeys.managedResources && <ManagedResources />}
                    {selectedKey === SettingsKeys.GrafanaDashboard && <GrafanaDashboard />}
                    {selectedKey === SettingsKeys.DataConnectors && showDataConnectors && <DataConnectors />}
                    {selectedKey === SettingsKeys.KnowledgeBase && showKnowledgeBase && <KnowledgeBase />}
                    {selectedKey === SettingsKeys.AccessControl && <AccessControl />}
                    {selectedKey === SettingsKeys.Identity && <Identity />}
                    {selectedKey === SettingsKeys.SubAgents && showSubAgents && <SubAgents />}
                </div>
            </div>
        )
    );
};

export default Settings;
