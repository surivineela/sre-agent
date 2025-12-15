import { initializeIcons, useTheme } from '@fluentui/react';
import { NavDrawer, NavDrawerBody, NavItem, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { useFeatureFlags } from '../../Common/Hooks/useFeatureFlags';
import GrafanaDashboard from '../../GrafanaDashboard/GrafanaDashboard.ReactView';
import { SettingsTabResources } from '../../Strings/SREAgentResources';
import SessionInsights from '../SessionInsights/SessionInsights';
import { useSharedNavDrawerStyles } from '../Styles/Navigation.styles';
import AccessControl from './AccessControl.ReactView';
import Basics from './Basics.ReactView';
import { Connectors } from './Connectors/Connectors';
import Identity from './Identity.ReactView';
import KnowledgeBase from './KnowledgeBaseComponents/KnowledgeBase.ReactView';
import ManagedResources from './ManagedResources.ReactView';
import McpServer from './McpServer';
import { useSettingsStyles } from './Styles/Settings.styles';
import SubAgents from './SubAgents.ReactView';
import Support from './Support';
import Usage from './Usage';
import { CopilotProvider, CopilotTheme } from '@fluentui-copilot/react-copilot';

export enum SettingsKeys {
    AccessControl = 'accessControl',
    Basics = 'basics',
    GrafanaDashboard = 'grafanaDashboard',
    managedResources = 'managedResourcesGroups',
    Connectors = 'connectors',
    Identity = 'identity',
    KnowledgeBase = 'knowledgeBase',
    SubAgents = 'subAgents',
    DataKnowledgeSpace = 'dataKnowledgeSpace',
    McpServers = 'mcpServers',
    Usage = 'usage',
    SessionInsights = 'sessionInsights',
    Support = 'support',
}

// Settings uses shared navigation styles with specific maxWidth
const useSettingsNavStyles = () => useSharedNavDrawerStyles();

const Settings: FC = () => {
    const theme = useTheme();

    const styles = useSettingsStyles();
    const navigationStyles = useSettingsNavStyles();
    const intl = useIntl();
    const { menuItem } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const showConnectors = useConfigSetting(SettingNames.Connectors);
    const showSubAgents = useConfigSetting(SettingNames.ShowSubAgentsItemInSettings);
    const showMcpServer = useConfigSetting(SettingNames.McpServer);

    const { features } = useFeatureFlags();
    const showSessionInsights = features.sessionInsights;

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

        if (showConnectors) {
            items.push({
                name: intl.formatMessage(SettingsTabResources.connectors),
                key: SettingsKeys.Connectors,
            });
        }

        items.push({
            name: intl.formatMessage(SettingsTabResources.knowledgeBase),
            key: SettingsKeys.KnowledgeBase,
        });

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

        if (showMcpServer) {
            items.push({
                name: intl.formatMessage(SettingsTabResources.mcpServers),
                key: SettingsKeys.McpServers,
            });
        }

        items.push({
            name: intl.formatMessage(SettingsTabResources.usage),
            key: SettingsKeys.Usage,
        });

        if (showSessionInsights) {
            items.push({
                name: intl.formatMessage(SettingsTabResources.sessionInsights),
                key: SettingsKeys.SessionInsights,
            });
        }

        items.push({
            name: intl.formatMessage(SettingsTabResources.support),
            key: SettingsKeys.Support,
        });

        return items;
    }, [intl, showConnectors, showMcpServer, showSubAgents, showSessionInsights]);

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
            <CopilotProvider {...CopilotTheme} mode={'canvas'} theme={theme.isInverted ? webDarkTheme : webLightTheme} style={styles.navContainerStyles}>
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
                    {selectedKey === SettingsKeys.Connectors && showConnectors && <Connectors />}
                    {selectedKey === SettingsKeys.KnowledgeBase && <KnowledgeBase />}
                    {selectedKey === SettingsKeys.AccessControl && <AccessControl />}
                    {selectedKey === SettingsKeys.Identity && <Identity />}
                    {selectedKey === SettingsKeys.SubAgents && showSubAgents && <SubAgents />}
                    {selectedKey === SettingsKeys.McpServers && <McpServer />}
                    {selectedKey === SettingsKeys.Usage && <Usage />}
                    {selectedKey === SettingsKeys.SessionInsights && <SessionInsights />}
                    {selectedKey === SettingsKeys.Support && <Support />}
                </div>
            </CopilotProvider>
        )
    );
};

export default Settings;
