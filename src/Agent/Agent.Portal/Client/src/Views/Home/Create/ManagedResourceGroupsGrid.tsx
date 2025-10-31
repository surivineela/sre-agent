import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Image,
    Link,
    MessageBar,
    MessageBarBody,
    Spinner,
    TableCellLayout,
    TableColumnDefinition,
    Text,
} from '@fluentui/react-components';
import {
    CheckmarkCircle16Regular,
    Dismiss16Regular,
    ErrorCircle16Regular,
    LockClosed16Regular,
    ShieldError16Regular,
} from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PermissionsClient } from '../../../Common/Clients/PermissionsClient';
import { ApiVersions } from '../../../Common/Constants/ApiVersions';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { ResourceGroup } from '../../../Common/Contracts/Arm';
import { FieldRestrictionResult, LockLevels } from '../../../Common/Contracts/Permissions';
import { ArmServiceType } from '../../../Common/Utilities/ArmTemplateBuilder/ArmTemplateTypes';
import { getUserFriendlyLocation } from '../../../Common/Utilities/Location';
import { openResourceGroupOverviewInNewTab } from '../../../Common/Utilities/Url';
import { PortalResources } from '../../../Strings/Resources';
import { SreAgentCreateFormProps } from './CreateAgentDialog';

const MAX_RESOURCE_GROUPS = 100;

enum ResourceGroupListColumnKey {
    name = 'name',
    location = 'location',
    permissions = 'permissions',
    delete = 'delete',
}

export enum SreAgentPermissions {
    roleAssignmentWrite = 'Microsoft.Authorization/roleAssignments/write',
    roleAssignmentRead = 'Microsoft.Authorization/roleAssignments/read',
    roleAssignmentDelete = 'Microsoft.Authorization/roleAssignments/delete',
    identityWrite = 'Microsoft.ManagedIdentity/userAssignedIdentities/write',
    roleAssignmentAll = 'Microsoft.Authorization/roleAssignments/*',
    authWrite = 'Microsoft.Authorization/*/Write',
    authAll = 'Microsoft.Authorization/*',
    deployWrite = 'Microsoft.Resources/deployments/write',
}

interface ManagedResourceGroupGridItem extends ResourceGroup {
    permissions: boolean;
    readOnlyLock: boolean;
    denyAssignments: boolean;
    policyErrors: boolean;
}

interface ManagedResourceGroupsGridProps {
    isDeploying: boolean;
}

const ManagedResourceGroupsGrid = ({ isDeploying }: ManagedResourceGroupsGridProps) => {
    const intl = useIntl();
    const { values, setFieldValue } = useFormikContext<SreAgentCreateFormProps>();
    const [permissionsLoading, setPermissionsLoading] = useState<boolean>(false);
    const [managedResourceGroups, setManagedResourceGroups] = useState<ManagedResourceGroupGridItem[]>([]);

    const permissionsClient = useMemo(() => PermissionsClient.getInstance(TelemetrySource.SreAgentCreate), []);

    const checkHasPermissionToCreateIdentities = useCallback(
        async (resourceGroup: string) => {
            return permissionsClient.hasPermission(resourceGroup, [
                SreAgentPermissions.roleAssignmentWrite,
                SreAgentPermissions.roleAssignmentRead,
                SreAgentPermissions.roleAssignmentDelete,
                SreAgentPermissions.identityWrite,
                SreAgentPermissions.roleAssignmentAll,
                SreAgentPermissions.authWrite,
                SreAgentPermissions.authAll,
            ]);
        },
        [permissionsClient]
    );

    const checkResourceLocks = useCallback(
        async (resourceGroupId: string) => {
            return permissionsClient.getLocks(resourceGroupId);
        },
        [permissionsClient]
    );

    const checkResourceDenyAssignments = useCallback(
        async (resourceGroupId: string) => {
            return permissionsClient.getDenyAssignments(resourceGroupId);
        },
        [permissionsClient]
    );

    const checkResourcePolicies = useCallback(
        async (resourceGroupId: string) => {
            const content = {
                resourceDetails: {
                    scope: resourceGroupId,
                    apiVersion: ApiVersions.armApiVersion20230301,
                    resourceContent: {
                        type: ArmServiceType.Deployments,
                    },
                },
                pendingFields: [
                    {
                        field: 'name',
                        values: [`${values.name}-deployment`],
                    },
                    {
                        field: 'location',
                        values: [values.location],
                    },
                    {
                        field: 'tags',
                    },
                ],
            };
            return permissionsClient.checkPolicies(resourceGroupId, content);
        },
        [permissionsClient, values.location, values.name]
    );

    const runResourceGroupPermissionChecks = useCallback(async () => {
        const resourceGroupsWithPermissionChecks: ManagedResourceGroupGridItem[] = await Promise.all(
            values.managedResourceGroups.map(async rg => {
                const permissionsResponses = await Promise.all([
                    checkHasPermissionToCreateIdentities(rg.id),
                    checkResourceLocks(rg.id),
                    checkResourceDenyAssignments(rg.id),
                    checkResourcePolicies(rg.id),
                ]);
                const hasPermissionToAssignRoles = permissionsResponses[0];
                const hasReadOnlyLock = permissionsResponses[1]?.content?.value?.some(
                    lock => lock.properties.level === LockLevels.readOnly
                );
                const hasDenyAssignments = permissionsResponses[2]?.content?.value?.some(
                    denyAssignment =>
                        denyAssignment.properties.scope === rg.id &&
                        denyAssignment.properties?.permissions?.notActions?.findIndex(
                            action => action === SreAgentPermissions.deployWrite
                        ) !== -1
                );
                const hasDenyPolicies = permissionsResponses[3]?.content?.fieldRestrictions?.some(fieldRestriction => {
                    return fieldRestriction.restrictions?.some(restriction => {
                        return restriction.result === FieldRestrictionResult.Deny;
                    });
                });
                return {
                    ...rg,
                    permissions: hasPermissionToAssignRoles,
                    readOnlyLock: hasReadOnlyLock ?? false,
                    denyAssignments: hasDenyAssignments ?? false,
                    policyErrors: hasDenyPolicies ?? false,
                };
            })
        );

        const hasUnauthorized = resourceGroupsWithPermissionChecks.some(rg => !rg.permissions);
        const hasLocked = resourceGroupsWithPermissionChecks.some(rg => rg.readOnlyLock);
        const hasDenyAssignments = resourceGroupsWithPermissionChecks.some(rg => rg.denyAssignments);
        const hasPolicyErrors = resourceGroupsWithPermissionChecks.some(rg => rg.policyErrors);

        setManagedResourceGroups(resourceGroupsWithPermissionChecks);
        setFieldValue('managedResourceGroupsPermissionError', hasUnauthorized);
        setFieldValue('managedResourceGroupsLockError', hasLocked);
        setFieldValue('managedResourceGroupsDenyAssignmentError', hasDenyAssignments);
        setFieldValue('managedResourceGroupsPolicyError', hasPolicyErrors);
    }, [
        values.managedResourceGroups,
        setFieldValue,
        checkHasPermissionToCreateIdentities,
        checkResourceLocks,
        checkResourceDenyAssignments,
        checkResourcePolicies,
    ]);

    const checkAllPermissions = useCallback(async () => {
        if (values.managedResourceGroups.length === 0) {
            setManagedResourceGroups([]);
            setFieldValue('maxResourceGroupsError', false);
            setFieldValue('managedResourceGroupsPermissionError', false);
            setFieldValue('managedResourceGroupsLockError', false);
            setFieldValue('managedResourceGroupsDenyAssignmentError', false);
            setFieldValue('managedResourceGroupsPolicyError', false);
            return;
        }
        setPermissionsLoading(true);
        await runResourceGroupPermissionChecks();
        setPermissionsLoading(false);
    }, [runResourceGroupPermissionChecks, values.managedResourceGroups.length, setFieldValue]);

    useEffect(() => {
        const currentIds = new Set(managedResourceGroups.map(rg => rg.id));
        const newResourceGroups = values.managedResourceGroups.filter(rg => !currentIds.has(rg.id));

        setFieldValue('maxResourceGroupsError', values.managedResourceGroups.length > MAX_RESOURCE_GROUPS);

        if (newResourceGroups.length > 0 || values.managedResourceGroups.length === 0) {
            checkAllPermissions();
        }
    }, [values.managedResourceGroups, managedResourceGroups, setFieldValue, checkAllPermissions]);

    const onNameClick = useCallback((id: string) => {
        if (id) {
            openResourceGroupOverviewInNewTab(id);
        }
    }, []);

    const handleDelete = useCallback(
        (item: ManagedResourceGroupGridItem) => {
            const newValues = values.managedResourceGroups.filter(rg => rg.id !== item.id);
            const newGridValues = managedResourceGroups.filter(rg => rg.id !== item.id);

            const hasUnauthorized = newGridValues.some(rg => !rg.permissions);
            const hasLocked = newGridValues.some(rg => rg.readOnlyLock);
            const hasDenyAssignments = newGridValues.some(rg => rg.denyAssignments);
            const hasPolicyErrors = newGridValues.some(rg => rg.policyErrors);
            const maxResourceGroupsError = newValues.length > MAX_RESOURCE_GROUPS;

            setManagedResourceGroups(newGridValues);

            setFieldValue('managedResourceGroups', newValues);
            setFieldValue('managedResourceGroupsPermissionError', hasUnauthorized);
            setFieldValue('managedResourceGroupsLockError', hasLocked);
            setFieldValue('managedResourceGroupsDenyAssignmentError', hasDenyAssignments);
            setFieldValue('managedResourceGroupsPolicyError', hasPolicyErrors);
            setFieldValue('maxResourceGroupsError', maxResourceGroupsError);
        },
        [values.managedResourceGroups, setFieldValue, managedResourceGroups]
    );

    const renderPermissionStatus = useCallback(
        (item: ManagedResourceGroupGridItem) => {
            if (permissionsLoading) {
                return (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <Spinner size="tiny" />
                    </div>
                );
            } else if (!item.permissions) {
                return (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <ErrorCircle16Regular />
                        <Text>{intl.formatMessage(PortalResources.no)}</Text>
                    </div>
                );
            } else if (item.readOnlyLock) {
                return (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <LockClosed16Regular />
                        <Text>{intl.formatMessage(PortalResources.readOnlyLock)}</Text>
                    </div>
                );
            } else if (item.denyAssignments) {
                return (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <ShieldError16Regular />
                        <Text>{intl.formatMessage(PortalResources.denyAssignments)}</Text>
                    </div>
                );
            } else if (item.policyErrors) {
                return (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <ErrorCircle16Regular />
                        <Text>{intl.formatMessage(PortalResources.policyErrors)}</Text>
                    </div>
                );
            }
            return (
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                    <CheckmarkCircle16Regular />
                    <Text>{intl.formatMessage(PortalResources.yes)}</Text>
                </div>
            );
        },
        [permissionsLoading, intl]
    );

    const columns: TableColumnDefinition<ManagedResourceGroupGridItem>[] = useMemo(
        () => [
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.name,
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.resourceGroup)}</Text>,
                renderCell: item => (
                    <TableCellLayout media={<Image src="/ResourceGroup.svg" height={16} width={16} />}>
                        <Link
                            disabled={isDeploying}
                            onClick={() => onNameClick(item.id)}
                            aria-label={`${intl.formatMessage(PortalResources.resourceGroupNameLinkAriaLabel)} ${item.name}`}
                        >
                            {item.name}
                        </Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.location,
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.region)}</Text>,
                renderCell: item => <TableCellLayout>{getUserFriendlyLocation(item.location)}</TableCellLayout>,
            }),
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.permissions,
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.userPermissions)}</Text>,
                renderCell: item => <TableCellLayout>{renderPermissionStatus(item)}</TableCellLayout>,
            }),
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.delete,
                renderHeaderCell: () => null,
                renderCell: item => (
                    <TableCellLayout>
                        <Button
                            icon={<Dismiss16Regular />}
                            appearance="subtle"
                            onClick={() => handleDelete(item)}
                            disabled={isDeploying}
                            aria-label={`${intl.formatMessage(PortalResources.deleteResourceGroup)} ${item.name}`}
                        />
                    </TableCellLayout>
                ),
            }),
        ],
        [intl, isDeploying, onNameClick, renderPermissionStatus, handleDelete]
    );

    const errorMessage = useMemo(() => {
        if (values.maxResourceGroupsError) {
            return intl.formatMessage(PortalResources.resourceGroupMaxError);
        } else if (values.managedResourceGroupsPermissionError) {
            return intl.formatMessage(PortalResources.resourceGroupPermissionError);
        } else if (values.managedResourceGroupsLockError) {
            return intl.formatMessage(PortalResources.resourceGroupLockError);
        } else if (values.managedResourceGroupsDenyAssignmentError) {
            return intl.formatMessage(PortalResources.resourceGroupDenyAssignmentError);
        } else if (values.managedResourceGroupsPolicyError) {
            return intl.formatMessage(PortalResources.resourceGroupPolicyError);
        } else {
            return '';
        }
    }, [
        intl,
        values.managedResourceGroupsDenyAssignmentError,
        values.managedResourceGroupsLockError,
        values.managedResourceGroupsPermissionError,
        values.managedResourceGroupsPolicyError,
        values.maxResourceGroupsError,
    ]);

    return (
        <div style={{ width: '100%' }}>
            {errorMessage && (
                <div style={{ paddingTop: '5px' }}>
                    <MessageBar intent="error" role="alert" aria-live="assertive">
                        <MessageBarBody>{errorMessage}</MessageBarBody>
                    </MessageBar>
                </div>
            )}
            {permissionsLoading ? (
                <div style={{ display: 'flex', justifyContent: 'center', padding: '20px' }}>
                    <Spinner size="medium" />
                </div>
            ) : (
                <DataGrid
                    items={managedResourceGroups}
                    columns={columns}
                    getRowId={item => item.id}
                    aria-label={intl.formatMessage(PortalResources.managedResourceGroupsTableAriaLabel)}
                >
                    <DataGridHeader>
                        <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
                    </DataGridHeader>
                    <DataGridBody<ManagedResourceGroupGridItem>>
                        {({ item, rowId }) => (
                            <DataGridRow<ManagedResourceGroupGridItem> key={rowId}>
                                {({ renderCell }) => <DataGridCell>{renderCell((item as any).item)}</DataGridCell>}
                            </DataGridRow>
                        )}
                    </DataGridBody>
                </DataGrid>
            )}
        </div>
    );
};

export default ManagedResourceGroupsGrid;
