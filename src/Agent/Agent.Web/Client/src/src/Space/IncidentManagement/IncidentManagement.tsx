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
import AzPortalProxy from '../../Common/AzPortalProxy/AzPortalProxy';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { NoAccessError } from '../../Common/Components/NoAccessError';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { useUserPermissions } from '../../Common/Hooks/useUserPermissions';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import IncidentManagementSettings from '../Settings/IncidentManagementSettings';
import { useIncidentManagementStyles, useNavStyles } from '../Styles/IncidentManagement.styles';
import Analysis from './Analysis';
import { IncidentManagementMenuKeys } from './CreateIncidentHandler/Contracts';
import HandlersOverview from './HandlersOverview';
import IncidentsOverview from './IncidentsOverview/IncidentsOverview';

// TODO: Tooltip for disabled NavItems with reason

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

const IncidentManagement: FC = () => {
    const { menuItem } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const intl = useIntl();

    const {
        agentObj,
        agentLoading,
        agentLoadFailure,
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);
    const { logAmplitudeNavigationEvent } = useAzPortalContext();
    const { canReadIncidentManagement } = useUserPermissions();
    const { resourceId, isCrossTenantPortalMode } = useContext(EnvironmentContext);

    const showControlPlaneDependentFeatures = useMemo(() => !inStandaloneMode && !isCrossTenantPortalMode, [isCrossTenantPortalMode]);

    const styles = useIncidentManagementStyles();
    const navigationStyles = useNavStyles();

    const [disableAnalysis, setDisableAnalysis] = useState(false);
    const [iconsInitialized, setIconsInitialized] = useState(false);
    const [navigationHidden, setNavigationHidden] = useState<boolean>(false);
    const [navigationCollapsed, setNavigationCollapsed] = useState<boolean>(false);

    const agentAppInsightsAppId = useMemo<string | undefined>(
        () => agentObj?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId,
        [agentObj]
    );

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
                disabled: false,
            },
        ];

        if (showControlPlaneDependentFeatures) {
            items.push({
                key: IncidentManagementMenuKeys.Metrics,
                label: intl.formatMessage(IncidentManagementResources.metrics),
                disabled: disableAnalysis || !agentAppInsightsAppId,
            });
        }

        items.push({
            key: IncidentManagementMenuKeys.HandlerConfiguration,
            label: intl.formatMessage(IncidentManagementResources.responsePlans),
            disabled: false,
        });

        if (showControlPlaneDependentFeatures) {
            items.push({
                key: IncidentManagementMenuKeys.IncidentPlatform,
                label: intl.formatMessage(IncidentManagementResources.incidentPlatform),
                disabled: false,
            });
        }

        return items;
    }, [intl, disableAnalysis, agentAppInsightsAppId, showControlPlaneDependentFeatures]);

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
                case IncidentManagementMenuKeys.Metrics:
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
        if (incidentPlatformType) {
            if (incidentPlatformType === IncidentManagementType.None) {
                setDisableAnalysis(true);
                navigate({ ...location, pathname: `/views/incidentmanagement/${IncidentManagementMenuKeys.IncidentPlatform}` });
            } else {
                setDisableAnalysis(false);
            }
        }
    }, [incidentPlatformType]);

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
                ) : !canReadIncidentManagement ? (
                    <div
                        style={{
                            display: 'flex',
                            justifyContent: 'center',
                            height: '100%',
                            width: '100%',
                        }}
                    >
                        <NoAccessError requiredPermission={PermissionActions.AgentIncidentManagementRead} resourceId={resourceId} />
                    </div>
                ) : (
                    <>
                        <NavDrawer
                            selectedValue={selectedKey || IncidentManagementMenuKeys.IncidentOverview}
                            selectedCategoryValue=""
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
                                        className={navigationCollapsed ? navigationStyles.itemCollapsed : navigationStyles.item}
                                        disabled={navItem.disabled}
                                    >
                                        {!navigationCollapsed && <span className={navigationStyles.itemText}>{navItem.label}</span>}
                                    </NavItem>
                                ))}
                            </NavDrawerBody>
                        </NavDrawer>
                        {selectedKey === IncidentManagementMenuKeys.IncidentOverview && (
                            <IncidentsOverview
                                agentAppInsightsAppId={agentAppInsightsAppId}
                                showControlPlaneDependentFeatures={showControlPlaneDependentFeatures}
                            />
                        )}
                        {selectedKey === IncidentManagementMenuKeys.HandlerConfiguration && (
                            <HandlersOverview setNavigationHidden={setNavigationHidden} />
                        )}
                        {agentAppInsightsAppId && selectedKey === IncidentManagementMenuKeys.Metrics && (
                            <Analysis agentAppInsightsAppId={agentAppInsightsAppId} />
                        )}
                        {selectedKey === IncidentManagementMenuKeys.IncidentPlatform && <IncidentManagementSettings />}
                    </>
                )}
            </div>
        )
    );
};

export default IncidentManagement;
