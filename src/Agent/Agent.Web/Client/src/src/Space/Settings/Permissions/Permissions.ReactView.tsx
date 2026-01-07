import { Text, Title2, Toolbar, ToolbarButton, ToolbarDivider } from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Permission } from '../../../Common/Contracts/Azure/SreAgent';
import { AgentPermissionsResources, SettingsTabResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { useSreAgent } from '../Hooks/useSreAgent';
import { AddPermissionDialog } from './AddPermissionDialog';
import { usePermissionsStyles } from './Permissions.styles';
import { PermissionsDataGrid } from './PermissionsDataGrid';

const Permissions: FC = () => {
    const intl = useIntl();
    const styles = usePermissionsStyles();
    const { log, startNotification, stopNotification } = useAzPortalContext();
    const { resourceId } = useContext(EnvironmentContext);

    const { agentPatching, patchAgent } = useContext(SreAgentContext);
    const { agent, refresh, agentLoading } = useSreAgent(resourceId);

    const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
    const [selectedItems, setSelectedItems] = useState<Set<string>>(new Set());

    const permissions = useMemo(() => {
        return agent?.properties?.permissions ?? [];
    }, [agent?.properties?.permissions]);

    const isLoading = useMemo(() => agentLoading, [agentLoading]);
    const isOperationInProgress = useMemo(() => agentPatching, [agentPatching]);
    const isEmpty = useMemo(() => permissions.length === 0, [permissions.length]);

    const handleAddClick = useCallback(() => {
        setIsAddDialogOpen(true);
    }, []);

    const handleRefresh = useCallback(() => {
        refresh();
    }, [refresh]);

    const handleAddPermission = useCallback(
        async (newPermission: Permission) => {
            const notificationId = startNotification(
                intl.formatMessage(AgentPermissionsResources.addingPermission),
                intl.formatMessage(AgentPermissionsResources.addingPermissionDescription, { name: newPermission.displayName })
            );

            const updatedPermissions = [...permissions, newPermission];

            const response = await patchAgent({
                properties: {
                    permissions: updatedPermissions,
                },
            });

            if (response.metadata.success) {
                stopNotification(notificationId, true, intl.formatMessage(AgentPermissionsResources.permissionAddedSuccess));
                log({
                    action: 'addPermission',
                    actionModifier: 'succeeded',
                    logLevel: 'info',
                    data: {
                        permission: newPermission,
                    },
                });
            } else {
                stopNotification(notificationId, false, intl.formatMessage(AgentPermissionsResources.permissionAddFailed));
                log({
                    action: 'addPermission',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        error: response.metadata.error,
                        permission: newPermission,
                    },
                });
            }
        },
        [permissions, patchAgent, log, startNotification, stopNotification, intl]
    );

    const handleDelete = useCallback(async () => {
        const notificationId = startNotification(
            intl.formatMessage(AgentPermissionsResources.deletingPermission),
            intl.formatMessage(AgentPermissionsResources.deletingPermissionDescription, { count: selectedItems.size })
        );

        const updatedPermissions = permissions.filter(permission => !selectedItems.has(permission.objectId));

        const response = await patchAgent({
            properties: {
                permissions: updatedPermissions,
            },
        });

        if (response.metadata.success) {
            setSelectedItems(new Set());
            stopNotification(notificationId, true, intl.formatMessage(AgentPermissionsResources.permissionDeletedSuccess));
            log({
                action: 'deletePermission',
                actionModifier: 'succeeded',
                logLevel: 'info',
                data: {
                    deletedCount: selectedItems.size,
                },
            });
        } else {
            stopNotification(notificationId, false, intl.formatMessage(AgentPermissionsResources.permissionDeleteFailed));
            log({
                action: 'deletePermission',
                actionModifier: 'failed',
                logLevel: 'error',
                data: {
                    error: response.metadata.error,
                    deletedCount: selectedItems.size,
                },
            });
        }
    }, [permissions, selectedItems, patchAgent, log, startNotification, stopNotification, intl]);

    const isDeleteDisabled = useMemo(
        () => selectedItems.size === 0 || isLoading || isOperationInProgress,
        [selectedItems.size, isLoading, isOperationInProgress]
    );

    return (
        <div className={styles.container}>
            <Title2 className={styles.header}>{intl.formatMessage(SettingsTabResources.crossTenantPermissions)}</Title2>
            <Text className={styles.headerDescription}>{intl.formatMessage(AgentPermissionsResources.permissionsDescription)}</Text>

            <div className={styles.toolbar}>
                <Toolbar>
                    <ToolbarButton
                        icon={<Add16Regular />}
                        onClick={handleAddClick}
                        disabled={isOperationInProgress}
                        appearance="subtle"
                        className={styles.toolbarButton}
                    >
                        {intl.formatMessage(AgentPermissionsResources.add)}
                    </ToolbarButton>
                    <ToolbarButton
                        icon={<ArrowClockwise16Regular />}
                        onClick={handleRefresh}
                        disabled={isLoading || isOperationInProgress}
                        appearance="subtle"
                        className={styles.toolbarButton}
                    >
                        {intl.formatMessage(AgentPermissionsResources.refresh)}
                    </ToolbarButton>
                    <ToolbarDivider className={styles.toolbarDivider} />
                    <ToolbarButton icon={<Delete16Regular />} onClick={handleDelete} disabled={isDeleteDisabled} appearance="subtle">
                        {intl.formatMessage(AgentPermissionsResources.delete)}
                    </ToolbarButton>
                </Toolbar>
            </div>

            <PermissionsDataGrid
                permissions={permissions}
                selectedItems={selectedItems}
                onSelectionChange={setSelectedItems}
                isLoading={isLoading}
                isEmpty={isEmpty}
                onEmptyStateAction={handleAddClick}
            />

            <AddPermissionDialog isOpen={isAddDialogOpen} onOpenChange={setIsAddDialogOpen} onSave={handleAddPermission} />
        </div>
    );
};

export default Permissions;
