import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { RbacWarningBannerResources } from '../../Strings/SREAgentResources';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../Clients/ArmClient';
import { PermissionClient } from '../Clients/PermissionsClient';
import { RBACRoleIds } from '../Contracts/Azure/Permission';

export const useRbacWarning = () => {
    const intl = useIntl();
    const { resourceId, userInfo, isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);
    const [isDismissed, setIsDismissed] = useState<boolean>(false);
    const [checking, setChecking] = useState<boolean>(true);
    const [alreadyHasAgentRole, setAlreadyHasAgentRole] = useState<boolean>(false);

    useEffect(() => {
        let cancelled = false;
        const run = async () => {
            const principalId = userInfo?.objectId;
            if (!principalId || !resourceId) {
                setChecking(false);
                return;
            }
            const roleIdsToCheck = [RBACRoleIds.sreAgentAdmin, RBACRoleIds.sreAgentUser, RBACRoleIds.sreAgentReader];
            let hasAny = false;
            for (const roleId of roleIdsToCheck) {
                if (await PermissionClient.getInstance().hasRoleAssignment(resourceId, roleId, principalId)) {
                    hasAny = true;
                    break;
                }
            }
            if (!cancelled) {
                setAlreadyHasAgentRole(hasAny);
                setChecking(false);
            }
        };
        run();
        return () => {
            cancelled = true;
        };
    }, [userInfo, resourceId]);

    const handleAddAdminClick = useCallback(async () => {
        const principalId = userInfo?.objectId;
        if (!principalId || !resourceId) {
            return;
        }

        let agentName = '';
        try {
            const match = /\/providers\/Microsoft\.App\/agents\/([^/]+)$/i.exec(resourceId);
            if (match && match[1]) {
                agentName = decodeURIComponent(match[1]);
            }
        } catch {
            // ignore parsing errors
        }

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(RbacWarningBannerResources.addAdminNotificationTitle, { name: agentName }),
            intl.formatMessage(RbacWarningBannerResources.addAdminNotificationInProgress, { name: agentName })
        );
        azPortalContext.logAmplitudeOperationEvent({
            targetType: 'update',
            targetAction: 'start',
            targetName: 'addSreAgentAdminRole',
            targetFriendlyName: 'Add SRE Agent Admin Role',
        });

        const response = await PermissionClient.getInstance().assignRole(resourceId, RBACRoleIds.sreAgentAdmin, principalId, 'User');
        if (response.metadata.success) {
            const successMsg = intl.formatMessage(RbacWarningBannerResources.addAdminNotificationSuccess, { name: agentName });
            azPortalContext.stopNotification(notificationId, true, successMsg);
            setAlreadyHasAgentRole(true);
            azPortalContext.logAmplitudeOperationEvent({
                targetType: 'update',
                targetAction: 'success',
                targetName: 'addSreAgentAdminRole',
                targetFriendlyName: 'Add SRE Agent Admin Role',
            });
        } else {
            const errorMessage = getErrorMessage(response.metadata.error);
            const failMsg = intl.formatMessage(RbacWarningBannerResources.addAdminNotificationFailure, {
                name: agentName,
                errorMessage,
            });
            azPortalContext.stopNotification(notificationId, false, failMsg);
            azPortalContext.logAmplitudeOperationEvent({
                targetType: 'update',
                targetAction: 'failed',
                targetName: 'addSreAgentAdminRole',
                targetFriendlyName: 'Add SRE Agent Admin Role',
                errorInfo: {
                    message: errorMessage,
                },
            });
        }
    }, [userInfo, resourceId, azPortalContext, intl]);

    const handleDismiss = useCallback(() => {
        setIsDismissed(true);
    }, []);

    const showRbacWarning = useMemo(() => {
        return !checking && !isDismissed && !alreadyHasAgentRole && !isCrossTenantPortalMode;
    }, [checking, isDismissed, alreadyHasAgentRole, isCrossTenantPortalMode]);

    return {
        showRbacWarning,
        handleAddAdminClick,
        handleDismiss,
        isCheckingRbac: checking,
    };
};
