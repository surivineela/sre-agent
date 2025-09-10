import { INavLink, INavLinkGroup, initializeIcons, Nav } from '@fluentui/react';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { useKnowledgeBaseConfig } from '../../Common/Hooks/UseKnowledgeBaseConfig';
import GrafanaDashboard from '../../GrafanaDashboard/GrafanaDashboard.ReactView';
import { SettingsTabResources } from '../../Strings/SREAgentResources';
import AccessControl from './AccessControl.ReactView';
import Basics from './Basics.ReactView';
import DataConnectors from './DataConnectors.ReactView';
import Identity from './Identity.ReactView';
import KnowledgeBase from './KnowledgeBase.ReactView';
import ManagedResources from './ManagedResources.ReactView';
import { navStyles, useSettingsStyles } from './Styles/Settings.styles';
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

const Settings: FC = () => {
    const styles = useSettingsStyles();
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
                        name: intl.formatMessage(SettingsTabResources.grafanaDashboard),
                        url: '',
                        key: SettingsKeys.GrafanaDashboard,
                    },
                    ...(showDataConnectors
                        ? [
                              {
                                  name: intl.formatMessage(SettingsTabResources.dataConnectors),
                                  url: '',
                                  key: SettingsKeys.DataConnectors,
                              },
                          ]
                        : []),
                    ...(showKnowledgeBase
                        ? [
                              {
                                  name: intl.formatMessage(SettingsTabResources.knowledgeBase),
                                  url: '',
                                  key: SettingsKeys.KnowledgeBase,
                              },
                          ]
                        : []),
                    {
                        name: intl.formatMessage(SettingsTabResources.accessControl),
                        url: '',
                        key: SettingsKeys.AccessControl,
                    },
                    {
                        name: intl.formatMessage(SettingsTabResources.identity),
                        url: '',
                        key: SettingsKeys.Identity,
                    },
                    ...(showSubAgents
                        ? [
                              {
                                  name: intl.formatMessage(SettingsTabResources.subAgents),
                                  url: '',
                                  key: SettingsKeys.SubAgents,
                              },
                          ]
                        : []),
                ],
            },
        ],
        [intl, showDataConnectors, showKnowledgeBase]
    );

    const onNavLinkClick = useCallback(
        (item?: INavLink) => {
            if (item?.key && Object.values(SettingsKeys).includes(item.key as SettingsKeys) && item.key !== selectedKey) {
                logAmplitudeNavigationEvent({
                    targetType: 'tab',
                    targetAction: 'tabItem',
                    targetName: item.key,
                    targetFriendlyName: item.key,
                });

                navigate({ ...location, pathname: `/views/settings/${item.key}` });
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
                <Nav groups={navLinkGroups} styles={navStyles} selectedKey={selectedKey} onLinkClick={(_, item) => onNavLinkClick(item)} />
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
