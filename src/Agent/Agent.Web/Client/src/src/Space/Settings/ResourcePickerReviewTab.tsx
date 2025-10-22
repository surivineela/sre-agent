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
    },
    container: {
        display: 'flex',
        gap: '5px',
        flexDirection: 'column',
    },
    errorMessageContainer: {
        paddingTop: '5px',
        marginBottom: '-5px',
    },
    scrollableArea: {
        flex: '1',
        overflowY: 'auto',
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
}

interface ManagedResourceGroupGridItem extends ResourceGroup {
    permissions: boolean;
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

const RESOURCE_GROUP_LIMIT = 20;

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

    const checkHasPermissionToCreateIdentities = useCallback(async (resourceGroup: string) => {
        return PermissionClient.getInstance().hasPermission(resourceGroup, [
            SreAgentPermissions.roleAssignmentWrite,
            SreAgentPermissions.identityWrite,
        ]);
    }, []);

    const runResourceGroupPermissionChecks = useCallback(async () => {
        const resourceGroupsWithPermissionChecks: ManagedResourceGroupGridItem[] = await Promise.all(
            selectedResourceGroups.map(async rg => {
                const permissions = await checkHasPermissionToCreateIdentities(rg.id);
                return { ...rg, permissions };
            })
        );

        const hasUnauthorized = resourceGroupsWithPermissionChecks.some(rg => !rg.permissions);

        setManagedResourceGroups(resourceGroupsWithPermissionChecks);
        setResourceGroupPermissionsError(hasUnauthorized);
    }, [selectedResourceGroups, checkHasPermissionToCreateIdentities, setResourceGroupPermissionsError]);

    useEffect(() => {
        setResourceGroupMaxError(selectedResourceGroups.length > RESOURCE_GROUP_LIMIT);
        runResourceGroupPermissionChecks();
    }, [selectedResourceGroups, runResourceGroupPermissionChecks, setResourceGroupMaxError]);

    const onDeleteClick = useCallback(
        (item: ManagedResourceGroupGridItem) => {
            toggleItemSelection(item.id);

            const newGridValues = managedResourceGroups.filter(rg => rg.id !== item.id);
            setManagedResourceGroups(newGridValues);
            const hasUnauthorized = newGridValues.some(rg => !rg.permissions);
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
                    <TableCellLayout>
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
                renderCell: item => (
                    <TableCellLayout>
                        <div className={styles.iconRow}>
                            {item.permissions ? (
                                <CheckmarkCircle16Filled primaryFill={'green'} />
                            ) : (
                                <DismissCircle16Filled primaryFill={'red'} />
                            )}
                            {item.permissions ? intl.formatMessage(SreAgentResources.yes) : intl.formatMessage(SreAgentResources.no)}
                        </div>
                    </TableCellLayout>
                ),
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
            {errorMessage && (
                <div className={localStyles.errorMessageContainer}>
                    <MessageBar intent="error">
                        <MessageBarBody>{errorMessage}</MessageBarBody>
                    </MessageBar>
                </div>
            )}
            <div className={localStyles.scrollableArea} data-is-scrollable="true">
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
                        <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
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
        </div>
    );
};

export default ReviewTab;
