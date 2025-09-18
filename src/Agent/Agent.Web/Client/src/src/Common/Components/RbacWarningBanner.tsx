import { Button, Link } from '@fluentui/react-components';
import { Dismiss12Regular } from '@fluentui/react-icons';
import { MessageBar, MessageBarBody } from '@fluentui/react-message-bar';
import { memo, useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { RbacWarningBannerResources } from '../../Strings/SREAgentResources';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../Clients/ArmClient';
import { PermissionClient } from '../Clients/PermissionsClient';
import { SreAgentFwLinks } from '../Constants/FwLinks';
import { RBACRoleIds } from '../Contracts/Azure/Permission';

const RBAC_BANNER_DISMISSED_KEY = 'sreagent.rbacWarningBannerDismissed';

const RbacWarningBanner = () => {
    const intl = useIntl();
    const { resourceId, userInfo, isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);
    const [isDismissed, setIsDismissed] = useState<boolean>(() => {
        try {
            return localStorage.getItem(RBAC_BANNER_DISMISSED_KEY) === 'true';
        } catch {
            return false;
        }
    });
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
            intl.formatMessage(RbacWarningBannerResources.addAdminNotificationDescription, { name: agentName })
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
            const failMsg = errorMessage
                ? intl.formatMessage(RbacWarningBannerResources.addAdminNotificationErrorWithMessage, {
                      name: agentName,
                      error: errorMessage,
                  })
                : intl.formatMessage(RbacWarningBannerResources.addAdminNotificationError, { name: agentName });
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
        try {
            localStorage.setItem(RBAC_BANNER_DISMISSED_KEY, 'true');
        } catch {
            // Ignore localStorage errors
        }
        setIsDismissed(true);
    }, []);

    if (isDismissed || alreadyHasAgentRole || isCrossTenantPortalMode || AzPortalProxy.inStandaloneMode) {
        return null;
    }

    return (
        <MessageBar
            intent={'warning'}
            shape={'rounded'}
            layout={'multiline'}
            style={{
                margin: '4px',
            }}
        >
            <MessageBarBody>
                <div style={{ display: 'flex', gap: '12px' }}>
                    <div style={{ flex: 1, wordBreak: 'break-word', overflowWrap: 'break-word' }}>
                        {`${intl.formatMessage(RbacWarningBannerResources.rbacWarningMessage)} `}
                        <Link onClick={handleAddAdminClick} aria-disabled={checking}>
                            {intl.formatMessage(RbacWarningBannerResources.clickHereToAssignRole)}
                        </Link>
                        {`${intl.formatMessage(RbacWarningBannerResources.or)} `}
                        <Link href={SreAgentFwLinks.sreAgentRbacInfo} target="_blank" rel="noopener noreferrer">
                            {intl.formatMessage(RbacWarningBannerResources.learnMoreAboutRbac)}
                        </Link>
                        .
                    </div>
                    <div></div>
                    <Button
                        appearance="subtle"
                        size="small"
                        icon={<Dismiss12Regular />}
                        onClick={handleDismiss}
                        aria-label={intl.formatMessage(RbacWarningBannerResources.dismissBanner)}
                    />
                </div>
            </MessageBarBody>
        </MessageBar>
    );
};

export default memo(RbacWarningBanner);
