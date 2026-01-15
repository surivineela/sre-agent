import {
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Skeleton,
    SkeletonItem,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    useTableFeatures,
    useTableSort,
} from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ResourceGroupClient } from '../../../Common/Clients/ResourceGroupClient';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { getRoleNamesForResourceGroup, ResourceTypeToDisplayNameMap } from '../../../Common/Contracts/Permissions';
import { LogLevel } from '../../../Common/Contracts/Telemetry';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { safeCompare } from '../../../Common/Utilities/String';
import { PortalResources, RolesAndPermissions } from '../../../Strings/Resources';
import { SreAgentCreateFormProps } from './CreateAgentDialog';
import { permissionsMap } from './PermissionsConstants';

enum RoleListColumnKey {
    role = 'role',
    description = 'description',
}

interface RoleGridItem {
    role: string;
    title: string;
    description: string;
}

const PermissionsGrid = () => {
    const intl = useIntl();
    const { values } = useFormikContext<SreAgentCreateFormProps>();
    const { logEvent } = useTelemetry(TelemetrySource.SreAgentCreate, undefined);

    const [basePermissionsGridItems, setBasePermissionsGridItems] = useState<RoleGridItem[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [allResourceTypes, setAllResourceTypes] = useState<string[]>([]);
    const [allResourceTypesForDisplay, setAllResourceTypesForDisplay] = useState<string[]>([]);

    const resourceGroupClient = useMemo(() => ResourceGroupClient.getInstance(TelemetrySource.SreAgentCreate), []);

    const getResourceTypesForResourceGroups = useCallback(
        async (resourceGroupIds: string[]) => {
            const response = await resourceGroupClient.listAllResourcesInResourceGroups(resourceGroupIds);

            if (!response.isSuccessful) {
                logEvent({
                    action: `Failed to get resource types for resource groups: ${resourceGroupIds.join(', ')}`,
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    additionalData: { resourceGroupIds, error: response.error },
                });
                return [];
            }

            return response.content ?? [];
        },
        [logEvent, resourceGroupClient]
    );

    const getResourceTypeAndKindsForResourceGroups = useCallback(
        async (resourceGroupIds: string[]) => {
            const response = await resourceGroupClient.listResourceTypeAndKindsInResourceGroups(resourceGroupIds);

            if (!response.isSuccessful) {
                logEvent({
                    action: `Failed to get resource type and kinds for resource groups: ${resourceGroupIds.join(', ')}`,
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    additionalData: { resourceGroupIds, error: response.error },
                });
                return [];
            }

            return response.content ?? [];
        },
        [logEvent, resourceGroupClient]
    );

    useEffect(() => {
        const fetchResourceTypes = async () => {
            if (values.managedResourceGroups.length > 0) {
                setIsLoading(true);
                const resourceGroupIds = values.managedResourceGroups.map(rg => rg.id);
                const resourceTypes = await Promise.all([
                    getResourceTypesForResourceGroups(resourceGroupIds),
                    getResourceTypeAndKindsForResourceGroups(resourceGroupIds),
                ]);
                setAllResourceTypes(resourceTypes[0]);
                setAllResourceTypesForDisplay(resourceTypes[1]);
                setIsLoading(false);
            } else {
                setAllResourceTypes([]);
                setBasePermissionsGridItems([]);
            }
        };

        fetchResourceTypes();
    }, [getResourceTypeAndKindsForResourceGroups, getResourceTypesForResourceGroups, values.managedResourceGroups]);

    useEffect(() => {
        if (allResourceTypes.length > 0) {
            const allRoleNames = new Set<string>();

            const roleIds = getRoleNamesForResourceGroup(allResourceTypes, values.permissionsLevel);
            roleIds.forEach(roleName => allRoleNames.add(roleName));

            const gridItems: RoleGridItem[] = Array.from(allRoleNames)
                .map(roleName => {
                    const permission = permissionsMap[roleName];
                    if (permission) {
                        return {
                            role: roleName,
                            title: intl.formatMessage(permission.title),
                            description: intl.formatMessage(permission.description),
                        };
                    }
                    return {
                        role: roleName,
                        title: roleName,
                        description: roleName,
                    };
                })
                .filter(Boolean);

            setBasePermissionsGridItems(gridItems);
        }
    }, [allResourceTypes, values.permissionsLevel, intl]);

    const columns: TableColumnDefinition<RoleGridItem>[] = useMemo(
        () => [
            createTableColumn<RoleGridItem>({
                columnId: RoleListColumnKey.role,
                compare: (a, b) => safeCompare(a.title, b.title),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(RolesAndPermissions.role)}</Text>,
                renderCell: item => <TableCellLayout>{item.title}</TableCellLayout>,
            }),
            createTableColumn<RoleGridItem>({
                columnId: RoleListColumnKey.description,
                compare: (a, b) => safeCompare(a.description, b.description),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(RolesAndPermissions.description)}</Text>,
                renderCell: item => <TableCellLayout>{item.description}</TableCellLayout>,
            }),
        ],
        [intl]
    );

    const {
        getRows,
        sort: { getSortDirection, toggleColumnSort, sort },
    } = useTableFeatures(
        {
            columns,
            items: basePermissionsGridItems,
        },
        [
            useTableSort({
                defaultSortState: { sortColumn: RoleListColumnKey.role, sortDirection: 'ascending' },
            }),
        ]
    );

    const headerSortProps = (columnId: string) => ({
        onClick: (e: React.MouseEvent) => {
            toggleColumnSort(e, columnId);
        },
        sortDirection: getSortDirection(columnId),
    });

    const rows = sort(getRows());

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div aria-live="polite">
                <Text weight="semibold">{`${intl.formatMessage(RolesAndPermissions.detectedResourceTypes)}: `}</Text>
                {isLoading && <>{intl.formatMessage(RolesAndPermissions.loadingResourceTypes)}</>}
                {allResourceTypesForDisplay.length > 0 ? (
                    <>
                        {allResourceTypesForDisplay
                            .filter(type => ResourceTypeToDisplayNameMap[type])
                            .map((type, index, filteredArray) => (
                                <span key={type}>
                                    {intl.formatMessage(ResourceTypeToDisplayNameMap[type])}
                                    {index < filteredArray.length - 1 && ', '}
                                </span>
                            ))}
                    </>
                ) : (
                    intl.formatMessage(PortalResources.none)
                )}
            </div>

            <div style={{ width: '100%' }}>
                {isLoading ? (
                    <Skeleton>
                        <SkeletonItem />
                    </Skeleton>
                ) : (
                    <DataGrid
                        items={rows}
                        columns={columns}
                        sortable
                        getRowId={item => item.rowId}
                        aria-label={intl.formatMessage(RolesAndPermissions.permissionsTableAriaLabel)}
                    >
                        <DataGridHeader>
                            <DataGridRow>
                                {({ renderHeaderCell, columnId }) => (
                                    <DataGridHeaderCell {...headerSortProps(columnId as string)}>{renderHeaderCell()}</DataGridHeaderCell>
                                )}
                            </DataGridRow>
                        </DataGridHeader>
                        <DataGridBody<RoleGridItem>>
                            {({ item, rowId }) => (
                                <DataGridRow<RoleGridItem> key={rowId}>
                                    {({ renderCell }) => <DataGridCell>{renderCell((item as any).item)}</DataGridCell>}
                                </DataGridRow>
                            )}
                        </DataGridBody>
                    </DataGrid>
                )}
            </div>
        </div>
    );
};

export default PermissionsGrid;
