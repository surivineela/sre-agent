import { initializeIcons, MessageBar, MessageBarType } from '@fluentui/react';
import { mergeClasses, Spinner } from '@fluentui/react-components';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import AzPortalProxy from '../../Common/AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { NoAccessError } from '../../Common/Components/NoAccessError';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import { useUserPermissions } from '../../Common/Hooks/useUserPermissions';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { SreAgentContext, SreAgentSpaceContext } from '../Contracts/Context';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../Hooks/useAgentSiteNavigate';
import IncidentManagementSettings from '../Settings/IncidentManagementSettings';
import { useCommonStyles } from '../Styles/Common.styles';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import Analysis from './Analysis';
import IncidentsOverview from './IncidentsOverview/IncidentsOverview';
import ResponsePlanOverview from './ReponsePlanOverview';

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

interface IIncidentManagementProps {
    menuItem?: SecondaryNavItemValues;
}

const IncidentManagement: FC<IIncidentManagementProps> = ({ menuItem }) => {
    const intl = useIntl();

    const navigate = useAgentSiteNavigate();

    const { agentObj, agentLoading, agentLoadFailure } = useContext(SreAgentContext);
    const { canReadIncidentManagement } = useUserPermissions();
    const { resourceId, isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const { onExpandOrCollapseNavBar, navBarTypeWhenOpen } = useContext(SreAgentSpaceContext);

    const showControlPlaneDependentFeatures = useMemo(() => !inStandaloneMode && !isCrossTenantPortalMode, [isCrossTenantPortalMode]);

    const styles = useIncidentManagementStyles();
    const commonStyles = useCommonStyles();

    const [iconsInitialized, setIconsInitialized] = useState(false);
    const [selectedKey, setSelectedKey] = useState<SecondaryNavItemValues | undefined>(undefined);

    const agentAppInsightsAppId = useMemo<string | undefined>(
        () => agentObj?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId,
        [agentObj]
    );

    const setNavigationHidden = useCallback(
        (hidden: boolean) => {
            // Do not auto open the nav bar if it would be opened as an overlay
            if (hidden || navBarTypeWhenOpen !== 'overlay') {
                onExpandOrCollapseNavBar(!hidden);
            }
        },
        [onExpandOrCollapseNavBar, navBarTypeWhenOpen]
    );

    useEffect(() => {
        const key = Object.values(SecondaryNavItemValues).find(
            settingsKey => settingsKey.toLocaleLowerCase() === menuItem?.toLocaleLowerCase()
        );

        if (key) {
            setSelectedKey(key);
        } else {
            navigate({
                primaryNavItemValue: PrimaryNavItemValues.Activities,
                secondaryNavItemValue: SecondaryNavItemValues.IncidentOverview,
            });
        }
    }, [menuItem, navigate]);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    return (
        iconsInitialized && (
            <div className={mergeClasses(commonStyles.contentRootBorderAndBackground, styles.root)}>
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
                        {selectedKey === SecondaryNavItemValues.IncidentOverview && (
                            <IncidentsOverview
                                agentAppInsightsAppId={agentAppInsightsAppId}
                                showControlPlaneDependentFeatures={showControlPlaneDependentFeatures}
                            />
                        )}
                        {selectedKey === SecondaryNavItemValues.ResponsePlans && (
                            <ResponsePlanOverview setNavigationHidden={setNavigationHidden} />
                        )}
                        {agentAppInsightsAppId && selectedKey === SecondaryNavItemValues.Metrics && (
                            <Analysis agentAppInsightsAppId={agentAppInsightsAppId} />
                        )}
                        {selectedKey === SecondaryNavItemValues.IncidentPlatform && <IncidentManagementSettings />}
                    </>
                )}
            </div>
        )
    );
};

export default IncidentManagement;
