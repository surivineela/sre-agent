import { initializeIcons, MessageBar, MessageBarType } from '@fluentui/react';
import { Button, NavDrawer, NavDrawerBody, NavDrawerHeader, NavItem, Spinner } from '@fluentui/react-components';
import {
    ChartMultiple24Filled,
    ChartMultiple24Regular,
    ClipboardTaskList16Filled,
    ClipboardTaskList16Regular,
    LinkSettings24Filled,
    LinkSettings24Regular,
    PanelLeftContractRegular,
    PanelLeftExpandRegular,
    Warning24Filled,
    Warning24Regular,
} from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { IncidentManagementPlatform } from '../Contracts/IncidentManagement';
import { getIncidentManagementPlatform } from '../Settings/Hooks/useIncidentManagementSettings';
import IncidentManagementSettings from '../Settings/IncidentManagementSettings';
import { useIncidentManagementStyles, useNavStyles } from '../Styles/IncidentManagement.styles';
import Analysis from './Analysis';
import { IncidentManagementMenuKeys } from './CreateIncidentHandler/Contracts';
import HandlersOverview from './HandlersOverview';
import IncidentsOverview from './IncidentsOverview/IncidentsOverview';

const IncidentManagement: FC = () => {
    const { menuItem } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const intl = useIntl();

    const { agentObj, agentLoading, agentLoadFailure } = useContext(SreAgentContext);
    const { logAmplitudeNavigationEvent } = useAzPortalContext();

    const styles = useIncidentManagementStyles();
    const navigationStyles = useNavStyles();

    const showWatchtower = useConfigSetting(SettingNames.ShowWatchtower);

    const [disableOverviewAndHandlers, setDisableOverviewAndHandlers] = useState(false);
    const [iconsInitialized, setIconsInitialized] = useState(false);
    const [navigationHidden, setNavigationHidden] = useState<boolean>(false);
    const [navigationCollapsed, setNavigationCollapsed] = useState<boolean>(false);

    const selectedKey = useMemo(() => {
        return (
            Object.values(IncidentManagementMenuKeys).find(
                settingsKey => settingsKey.toLocaleLowerCase() === menuItem?.toLocaleLowerCase()
            ) || IncidentManagementMenuKeys.IncidentOverview
        );
    }, [menuItem]);

    const navItems = useMemo(() => {
        const items = [
            {
                key: IncidentManagementMenuKeys.IncidentOverview,
                label: intl.formatMessage(SreAgentResources.incidents),
                disabled: disableOverviewAndHandlers,
            },
        ];

        if (showWatchtower) {
            items.push({
                key: IncidentManagementMenuKeys.Analysis,
                label: intl.formatMessage(IncidentManagementResources.analysis),
                disabled: disableOverviewAndHandlers,
            });
        }

        items.push(
            {
                key: IncidentManagementMenuKeys.HandlerConfiguration,
                label: intl.formatMessage(IncidentManagementResources.responsePlans),
                disabled: disableOverviewAndHandlers,
            },
            {
                key: IncidentManagementMenuKeys.IncidentPlatform,
                label: intl.formatMessage(IncidentManagementResources.incidentPlatform),
                disabled: false,
            }
        );

        return items;
    }, [intl, disableOverviewAndHandlers, showWatchtower]);

    const renderNavIcon = useCallback(
        (key: IncidentManagementMenuKeys) => {
            const isSelected = key === selectedKey;
            switch (key) {
                case IncidentManagementMenuKeys.IncidentOverview:
                    return isSelected ? (
                        <Warning24Filled className={navigationStyles.itemIcon} />
                    ) : (
                        <Warning24Regular className={navigationStyles.itemIcon} />
                    );
                case IncidentManagementMenuKeys.HandlerConfiguration:
                    return isSelected ? (
                        <ClipboardTaskList16Filled className={navigationStyles.itemIcon} />
                    ) : (
                        <ClipboardTaskList16Regular className={navigationStyles.itemIcon} />
                    );
                case IncidentManagementMenuKeys.Analysis:
                    return isSelected ? (
                        <ChartMultiple24Filled className={navigationStyles.itemIcon} />
                    ) : (
                        <ChartMultiple24Regular className={navigationStyles.itemIcon} />
                    );
                case IncidentManagementMenuKeys.IncidentPlatform:
                    return isSelected ? (
                        <LinkSettings24Filled className={navigationStyles.itemIcon} />
                    ) : (
                        <LinkSettings24Regular className={navigationStyles.itemIcon} />
                    );
                default:
                    return null;
            }
        },
        [selectedKey, navigationStyles.itemIcon]
    );

    const onNavigationClick = useCallback(
        (navKey: string) => {
            if (
                navKey &&
                Object.values(IncidentManagementMenuKeys).includes(navKey as IncidentManagementMenuKeys) &&
                navKey !== selectedKey
            ) {
                logAmplitudeNavigationEvent({
                    targetType: 'tab',
                    targetAction: 'tabItem',
                    targetName: navKey,
                    targetFriendlyName: navKey,
                });

                navigate({ ...location, pathname: `/views/incidentmanagement/${navKey}` });
            }
        },
        [selectedKey, logAmplitudeNavigationEvent, navigate, location]
    );

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    useEffect(() => {
        if (agentObj) {
            const incidentManagementPlatform = getIncidentManagementPlatform(agentObj);
            if (
                incidentManagementPlatform === IncidentManagementPlatform.Disconnected ||
                incidentManagementPlatform === IncidentManagementPlatform.AzMonitor
            ) {
                setDisableOverviewAndHandlers(true);
                navigate({ ...location, pathname: `/views/incidentmanagement/${IncidentManagementMenuKeys.IncidentPlatform}` });
            }
        }
    }, [agentObj]);

    return (
        iconsInitialized && (
            <div className={styles.root}>
                {agentLoading || !iconsInitialized ? (
                    <div className={styles.spinner}>
                        <Spinner size="huge" />
                    </div>
                ) : agentLoadFailure ? (
                    <MessageBar messageBarType={MessageBarType.error}>
                        {intl.formatMessage(IncidentManagementResources.incidentManagementLoadFailure, { errorMessage: agentLoadFailure })}
                    </MessageBar>
                ) : (
                    <>
                        <NavDrawer
                            defaultSelectedValue={selectedKey || IncidentManagementMenuKeys.IncidentOverview}
                            defaultSelectedCategoryValue=""
                            open={!navigationHidden}
                            type="inline"
                            className={navigationCollapsed ? navigationStyles.drawerCollapsed : navigationStyles.drawer}
                        >
                            <NavDrawerHeader className={navigationStyles.drawerHeader}>
                                <Button
                                    icon={
                                        navigationCollapsed ? (
                                            <PanelLeftExpandRegular className={navigationStyles.itemIcon} />
                                        ) : (
                                            <PanelLeftContractRegular className={navigationStyles.itemIcon} />
                                        )
                                    }
                                    onClick={() => setNavigationCollapsed(!navigationCollapsed)}
                                    aria-label={intl.formatMessage(
                                        navigationCollapsed
                                            ? IncidentManagementResources.expandNavigation
                                            : IncidentManagementResources.collapseNavigation
                                    )}
                                    className={navigationStyles.headerButton}
                                    appearance="transparent"
                                />
                            </NavDrawerHeader>
                            <NavDrawerBody className={navigationStyles.drawerBody}>
                                {navItems.map(navItem => (
                                    <NavItem
                                        icon={renderNavIcon(navItem.key)}
                                        aria-label={navItem.label}
                                        key={navItem.key}
                                        value={navItem.key}
                                        href=""
                                        onClick={() => onNavigationClick(navItem.key)}
                                        className={navigationStyles.item}
                                        disabled={navItem.disabled}
                                    >
                                        {!navigationCollapsed && <span className={navigationStyles.itemText}>{navItem.label}</span>}
                                    </NavItem>
                                ))}
                            </NavDrawerBody>
                        </NavDrawer>
                        {selectedKey === IncidentManagementMenuKeys.IncidentOverview && <IncidentsOverview />}
                        {selectedKey === IncidentManagementMenuKeys.HandlerConfiguration && (
                            <HandlersOverview setNavigationHidden={setNavigationHidden} useConsolidatedCreate={true} />
                        )}
                        {showWatchtower && selectedKey === IncidentManagementMenuKeys.Analysis && <Analysis />}
                        {selectedKey === IncidentManagementMenuKeys.IncidentPlatform && <IncidentManagementSettings />}
                    </>
                )}
            </div>
        )
    );
};

export default IncidentManagement;
