import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Link,
    makeStyles,
    MessageBar,
    MessageBarBody,
    TableCellLayout,
    TableColumnDefinition,
    tokens,
} from '@fluentui/react-components';
import { CheckmarkCircle16Filled, Delete16Regular, DismissCircle16Filled } from '@fluentui/react-icons';
import { Dispatch, FC, SetStateAction, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { PermissionClient } from '../../Common/Clients/PermissionsClient';
import { getUserFriendlyLocation } from '../../Common/Helpers/LocationHelper';
import { ManagedResourcesStringResources, ResourcePickerTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ResourceGroup } from './Hooks/useResourceGroups';
import { ResourceGroupWithSelection } from './ResourceGroupPicker';
import { ResourceGroupPickerSkeleton } from './ResourceGroupPickerSkeleton';
import { useManagedResourcesStyles } from './Styles/ManagedResources.styles';

const useLocalStyles = makeStyles({
    dataGrid: {
        width: '100%',
        tableLayout: 'auto',
    },
    dataGridHeader: {
        fontWeight: '600',
        position: 'sticky',
        top: '0',
        backgroundColor: tokens.colorNeutralBackground1,
        zIndex: '1',
    },
    deleteButton: {
        minWidth: 'auto',
        padding: '4px',
    },
    headerText: {
        fontWeight: '600',
    },
    resourceGroupIcon: {
        height: '16px',
        width: '16px',
    },
    resourceGroupLink: {
        userSelect: 'text',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flexShrink: '5',
    },
    container: {
        display: 'flex',
        gap: '5px',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
    },
    errorMessageContainer: {
        paddingTop: '5px',
        marginBottom: '-5px',
    },
    scrollableArea: {
        flex: '1',
        overflowY: 'auto',
        minHeight: '0',
    },
    descriptionText: {
        marginBottom: '10px',
        fontSize: '14px',
        color: tokens.colorNeutralForeground2,
    },
    tableScrollableArea: {
        flex: '1',
        overflowY: 'auto',
        overflowX: 'auto',
        minHeight: '0',
    },
    permissionText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
});

enum ResourceGroupListColumnKey {
    name = 'name',
    subscription = 'subscription',
    location = 'location',
    permissions = 'permissions',
    delete = 'delete',
}

enum SreAgentPermissions {
    roleAssignmentWrite = 'Microsoft.Authorization/roleAssignments/write',
    identityWrite = 'Microsoft.ManagedIdentity/userAssignedIdentities/write',
    deployWrite = 'Microsoft.Resources/deployments/write',
}

enum LockLevels {
    readOnly = 'ReadOnly',
    canNotDelete = 'CanNotDelete',
}

interface ManagedResourceGroupGridItem extends ResourceGroup {
    permissions: boolean;
    hasReadOnlyLock: boolean;
    hasDenyAssignment: boolean;
}

export type ReviewTabProps = {
    selectedResourceGroups: ResourceGroupWithSelection[];
    resourceGroupPermissionsError: boolean;
    setResourceGroupPermissionsError: Dispatch<SetStateAction<boolean>>;
    resourceGroupMaxError: boolean;
    setResourceGroupMaxError: Dispatch<SetStateAction<boolean>>;
    toggleItemSelection: (id: string) => void;
    onRenderSubscription: (item: ResourceGroupWithSelection) => JSX.Element;
};

const RESOURCE_GROUP_LIMIT = 100;
const permissionClient = PermissionClient.getInstance();

const ReviewTab: FC<ReviewTabProps> = (props: ReviewTabProps) => {
    const {
        selectedResourceGroups,
        resourceGroupPermissionsError,
        setResourceGroupPermissionsError,
        resourceGroupMaxError,
        setResourceGroupMaxError,
        toggleItemSelection,
        onRenderSubscription,
    } = props;

    const portalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const styles = useManagedResourcesStyles();
    const localStyles = useLocalStyles();
    const [managedResourceGroups, setManagedResourceGroups] = useState<ManagedResourceGroupGridItem[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);

    const checkHasPermissionToCreateIdentities = useCallback(async (resourceGroup: string) => {
        return permissionClient.hasPermission(resourceGroup, [SreAgentPermissions.roleAssignmentWrite, SreAgentPermissions.identityWrite]);
    }, []);

    const checkResourceLocks = useCallback(async (resourceGroupId: string) => {
        return permissionClient.getLocks(resourceGroupId);
    }, []);

    const checkResourceDenyAssignments = useCallback(async (resourceGroupId: string) => {
        return permissionClient.getDenyAssignments(resourceGroupId);
    }, []);

    const runResourceGroupPermissionChecks = useCallback(async () => {
        setIsLoading(true);
        const resourceGroupsWithPermissionChecks: ManagedResourceGroupGridItem[] = await Promise.all(
            selectedResourceGroups.map(async rg => {
                const [permissions, locks, denyAssignments] = await Promise.all([
                    checkHasPermissionToCreateIdentities(rg.id),
                    checkResourceLocks(rg.id),
                    checkResourceDenyAssignments(rg.id),
                ]);

                const hasReadOnlyLock = locks?.some(lock => lock.level === LockLevels.readOnly) ?? false;

                const hasDenyAssignment =
                    denyAssignments?.some(
                        denyAssignment =>
                            denyAssignment.scope === rg.id &&
                            denyAssignment.permissions.notActions?.includes(SreAgentPermissions.deployWrite)
                    ) ?? false;

                return {
                    ...rg,
                    permissions,
                    hasReadOnlyLock,
                    hasDenyAssignment,
                };
            })
        );

        const hasUnauthorized = resourceGroupsWithPermissionChecks.some(
            rg => !rg.permissions || rg.hasReadOnlyLock || rg.hasDenyAssignment
        );

        setManagedResourceGroups(resourceGroupsWithPermissionChecks);
        setResourceGroupPermissionsError(hasUnauthorized);
        setIsLoading(false);
    }, [
        selectedResourceGroups,
        checkHasPermissionToCreateIdentities,
        checkResourceLocks,
        checkResourceDenyAssignments,
        setResourceGroupPermissionsError,
    ]);

    useEffect(() => {
        setResourceGroupMaxError(selectedResourceGroups.length > RESOURCE_GROUP_LIMIT);
        if (
            (selectedResourceGroups.length > 0 && managedResourceGroups.length === 0) ||
            selectedResourceGroups.length > managedResourceGroups.length
        ) {
            runResourceGroupPermissionChecks();
        }
    }, [selectedResourceGroups, runResourceGroupPermissionChecks, setResourceGroupMaxError, managedResourceGroups.length]);

    const onDeleteClick = useCallback(
        (item: ManagedResourceGroupGridItem) => {
            toggleItemSelection(item.id);

            const newGridValues = managedResourceGroups.filter(rg => rg.id !== item.id);
            setManagedResourceGroups(newGridValues);
            const hasUnauthorized = newGridValues.some(rg => !rg.permissions || rg.hasReadOnlyLock || rg.hasDenyAssignment);
            setResourceGroupPermissionsError(hasUnauthorized);
        },
        [managedResourceGroups, setResourceGroupPermissionsError, setManagedResourceGroups, toggleItemSelection]
    );

    const columns = useMemo<TableColumnDefinition<ManagedResourceGroupGridItem>[]>(() => {
        return [
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.name,
                renderHeaderCell: () => (
                    <span className={localStyles.headerText}>{intl.formatMessage(ManagedResourcesStringResources.resourceGroupName)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout truncate>
                        <div className={styles.statusRow}>
                            <img src="./ResourceGroup.svg" alt="ResourceGroup" className={localStyles.resourceGroupIcon} />
                            <Link
                                className={localStyles.resourceGroupLink}
                                onClick={_e => {
                                    if (item.id) {
                                        portalContext.openBlade({
                                            extension: 'HubsExtension',
                                            detailBlade: 'ResourceGroupOverview',
                                            detailBladeInputs: {
                                                id: item.id,
                                            },
                                        });
                                    }
                                }}
                            >
                                {item.name}
                            </Link>
                        </div>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.subscription,
                renderHeaderCell: () => (
                    <span className={localStyles.headerText}>{intl.formatMessage(ManagedResourcesStringResources.subscription)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout>{onRenderSubscription(item as unknown as ResourceGroupWithSelection)}</TableCellLayout>
                ),
            }),
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.location,
                renderHeaderCell: () => (
                    <span className={localStyles.headerText}>{intl.formatMessage(ManagedResourcesStringResources.location)}</span>
                ),
                renderCell: item => <TableCellLayout>{getUserFriendlyLocation(item.location)}</TableCellLayout>,
            }),
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.permissions,
                renderHeaderCell: () => (
                    <span className={localStyles.headerText}>
                        {intl.formatMessage(ResourcePickerTabResources.permissionsForRoleAssignment)}
                    </span>
                ),
                renderCell: item => {
                    const hasIssues = !item.permissions || item.hasReadOnlyLock || item.hasDenyAssignment;
                    return (
                        <TableCellLayout truncate>
                            <div className={styles.iconRow}>
                                {hasIssues ? (
                                    <DismissCircle16Filled primaryFill={'red'} />
                                ) : (
                                    <CheckmarkCircle16Filled primaryFill={'green'} />
                                )}
                                <span className={localStyles.permissionText}>
                                    {item.hasReadOnlyLock
                                        ? intl.formatMessage(ResourcePickerTabResources.readOnlyLock)
                                        : item.hasDenyAssignment
                                          ? intl.formatMessage(ResourcePickerTabResources.denyAssignment)
                                          : hasIssues
                                            ? intl.formatMessage(SreAgentResources.no)
                                            : intl.formatMessage(SreAgentResources.yes)}
                                </span>
                            </div>
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<ManagedResourceGroupGridItem>({
                columnId: ResourceGroupListColumnKey.delete,
                renderHeaderCell: () => '',
                renderCell: item => (
                    <TableCellLayout>
                        <Button
                            icon={<Delete16Regular />}
                            appearance="subtle"
                            size="small"
                            className={localStyles.deleteButton}
                            onClick={() => onDeleteClick(item)}
                            aria-label={intl.formatMessage(ManagedResourcesStringResources.deleteResourceGroupAriaLabel, {
                                resourceGroupName: item.name,
                            })}
                        />
                    </TableCellLayout>
                ),
            }),
        ];
    }, [intl, styles, portalContext, onRenderSubscription, onDeleteClick, localStyles]);

    const errorMessage = useMemo(() => {
        if (resourceGroupMaxError) {
            return intl.formatMessage(ResourcePickerTabResources.resourceGroupMaxError);
        } else if (resourceGroupPermissionsError) {
            return intl.formatMessage(ResourcePickerTabResources.resourceGroupPermissionError);
        }
        return '';
    }, [intl, resourceGroupMaxError, resourceGroupPermissionsError]);

    const columnSizingOptions = useMemo(
        () => ({
            [ResourceGroupListColumnKey.name]: {
                minWidth: 200,
                idealWidth: 300,
            },
            [ResourceGroupListColumnKey.subscription]: {
                minWidth: 150,
                idealWidth: 175,
            },
            [ResourceGroupListColumnKey.location]: {
                minWidth: 100,
                idealWidth: 100,
            },
            [ResourceGroupListColumnKey.permissions]: {
                minWidth: 150,
                idealWidth: 170,
            },
            [ResourceGroupListColumnKey.delete]: {
                minWidth: 50,
                idealWidth: 50,
            },
        }),
        []
    );

    return (
        <div className={localStyles.container}>
            <div className={localStyles.descriptionText}>{intl.formatMessage(ResourcePickerTabResources.reviewTabDescription)}</div>
            {errorMessage && (
                <div className={localStyles.errorMessageContainer}>
                    <MessageBar intent="error">
                        <MessageBarBody>{errorMessage}</MessageBarBody>
                    </MessageBar>
                </div>
            )}
            <div className={localStyles.scrollableArea} data-is-scrollable="true">
                {isLoading ? (
                    <ResourceGroupPickerSkeleton />
                ) : (
                    <div className={localStyles.tableScrollableArea} data-is-scrollable="true">
                        <DataGrid
                            items={managedResourceGroups}
                            columns={columns}
                            sortable
                            resizableColumns
                            columnSizingOptions={columnSizingOptions}
                            getRowId={item => item.id}
                            className={localStyles.dataGrid}
                            size="small"
                        >
                            <DataGridHeader className={localStyles.dataGridHeader}>
                                <DataGridRow>
                                    {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                                </DataGridRow>
                            </DataGridHeader>
                            <DataGridBody<ManagedResourceGroupGridItem>>
                                {({ item, rowId }) => (
                                    <DataGridRow<ManagedResourceGroupGridItem> key={rowId}>
                                        {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                    </DataGridRow>
                                )}
                            </DataGridBody>
                        </DataGrid>
                    </div>
                )}
            </div>
        </div>
    );
};

export default ReviewTab;
