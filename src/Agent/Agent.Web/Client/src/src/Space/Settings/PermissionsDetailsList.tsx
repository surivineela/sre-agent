import {
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    makeStyles,
    SkeletonItem,
    TableCellLayout,
    TableColumnDefinition,
    tokens,
} from '@fluentui/react-components';
import React, { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { ResourceGroupClient } from '../../Common/Clients/ResourceGroupClient';
import { getRoleNamesForResourceGroup, RBACRoleNames } from '../../Common/Contracts/Azure/Permission';
import { AgentAccessLevel } from '../../Common/Contracts/Azure/SreAgent';
import { PermissionsResources } from '../../Strings/SREAgentResources';
import { ResourceGroup } from './Hooks/useResourceGroups';
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
    scrollContainer: {
        paddingTop: '10px',
        overflowY: 'auto',
        minHeight: '0px',
        maxHeight: '470px',
    },
    innerScrollContainer: {
        overflowY: 'auto',
        maxHeight: '100%',
    },
});

enum RoleListColumnKey {
    role = 'role',
    description = 'description',
}

interface RoleGridItem {
    role: string;
    title: string;
    description: string;
}

interface PermissionsDetailsListProps {
    accessLevel: AgentAccessLevel;
    managedResourceGroups: ResourceGroup[];
}

const PermissionsDetailsList: React.FC<PermissionsDetailsListProps> = ({ accessLevel, managedResourceGroups }) => {
    const styles = useManagedResourcesStyles();
    const localStyles = useLocalStyles();
    const portalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const [basePermissionsGridItems, setBasePermissionsGridItems] = useState<RoleGridItem[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [allResourceTypes, setAllResourceTypes] = useState<string[]>([]);

    const permissionsMap: Record<string, { title: string; description: string }> = useMemo(
        () => ({
            [RBACRoleNames.contributor]: {
                title: intl.formatMessage(PermissionsResources.contributor),
                description: intl.formatMessage(PermissionsResources.contributorDescription),
            },
            [RBACRoleNames.reader]: {
                title: intl.formatMessage(PermissionsResources.reader),
                description: intl.formatMessage(PermissionsResources.readerDescription),
            },
            [RBACRoleNames.monitoringReader]: {
                title: intl.formatMessage(PermissionsResources.monitoringReader),
                description: intl.formatMessage(PermissionsResources.monitoringReaderDescription),
            },
            [RBACRoleNames.logAnalyticsReader]: {
                title: intl.formatMessage(PermissionsResources.logAnalyticsReader),
                description: intl.formatMessage(PermissionsResources.logAnalyticsReaderDescription),
            },
            [RBACRoleNames.containerAppsOperator]: {
                title: intl.formatMessage(PermissionsResources.containerAppsOperator),
                description: intl.formatMessage(PermissionsResources.containerAppsOperatorDescription),
            },
            [RBACRoleNames.azureKubernetesServiceRbacReader]: {
                title: intl.formatMessage(PermissionsResources.azureKubernetesServiceRbacReader),
                description: intl.formatMessage(PermissionsResources.azureKubernetesServiceRbacReaderDescription),
            },
            [RBACRoleNames.azureKubernetesServiceClusterUser]: {
                title: intl.formatMessage(PermissionsResources.azureKubernetesServiceClusterUserRole),
                description: intl.formatMessage(PermissionsResources.azureKubernetesServiceClusterUserRoleDescription),
            },
            [RBACRoleNames.containerAppsContributor]: {
                title: intl.formatMessage(PermissionsResources.containerAppsContributor),
                description: intl.formatMessage(PermissionsResources.containerAppsContributorDescription),
            },
            [RBACRoleNames.azureKubernetesServiceClusterAdmin]: {
                title: intl.formatMessage(PermissionsResources.azureKubernetesServiceClusterAdmin),
                description: intl.formatMessage(PermissionsResources.azureKubernetesServiceClusterAdminDescription),
            },
            [RBACRoleNames.azureKubernetesServiceRbacClusterAdmin]: {
                title: intl.formatMessage(PermissionsResources.azureKubernetesServiceRbacClusterAdmin),
                description: intl.formatMessage(PermissionsResources.azureKubernetesServiceRbacClusterAdminDescription),
            },
            [RBACRoleNames.redisCacheContributor]: {
                title: intl.formatMessage(PermissionsResources.redisCacheContributor),
                description: intl.formatMessage(PermissionsResources.redisCacheContributorDescription),
            },
            [RBACRoleNames.websitesContributor]: {
                title: intl.formatMessage(PermissionsResources.websitesContributor),
                description: intl.formatMessage(PermissionsResources.websitesContributorDescription),
            },
            [RBACRoleNames.webPlanContributor]: {
                title: intl.formatMessage(PermissionsResources.webPlanContributor),
                description: intl.formatMessage(PermissionsResources.webPlanContributorDescription),
            },
            [RBACRoleNames.storageBlobDataReader]: {
                title: intl.formatMessage(PermissionsResources.storageBlobDataReader),
                description: intl.formatMessage(PermissionsResources.storageBlobDataReaderDescription),
            },
            [RBACRoleNames.documentDbAccountContributor]: {
                title: intl.formatMessage(PermissionsResources.documentDbAccountContributor),
                description: intl.formatMessage(PermissionsResources.documentDbAccountContributorDescription),
            },
            [RBACRoleNames.storageBlobDataContributor]: {
                title: intl.formatMessage(PermissionsResources.storageBlobDataContributor),
                description: intl.formatMessage(PermissionsResources.storageBlobDataContributorDescription),
            },
            [RBACRoleNames.sqlDbContributor]: {
                title: intl.formatMessage(PermissionsResources.sqlDbContributor),
                description: intl.formatMessage(PermissionsResources.sqlDbContributorDescription),
            },
            [RBACRoleNames.storageAccountContributor]: {
                title: intl.formatMessage(PermissionsResources.storageAccountContributor),
                description: intl.formatMessage(PermissionsResources.storageAccountContributorDescription),
            },
            [RBACRoleNames.virtualMachineContributor]: {
                title: intl.formatMessage(PermissionsResources.virtualMachineContributor),
                description: intl.formatMessage(PermissionsResources.virtualMachineContributorDescription),
            },
            [RBACRoleNames.azureDatabaseForPostgreSqlContributor]: {
                title: intl.formatMessage(PermissionsResources.postgreSqlContributor),
                description: intl.formatMessage(PermissionsResources.postgreSqlContributorDescription),
            },
            [RBACRoleNames.sqlServerContributor]: {
                title: intl.formatMessage(PermissionsResources.sqlServerContributor),
                description: intl.formatMessage(PermissionsResources.sqlServerContributorDescription),
            },
            [RBACRoleNames.applicationInsightsComponentContributor]: {
                title: intl.formatMessage(PermissionsResources.applicationInsightsComponentContributor),
                description: intl.formatMessage(PermissionsResources.applicationInsightsComponentContributorDescription),
            },
            [RBACRoleNames.logAnalyticsContributor]: {
                title: intl.formatMessage(PermissionsResources.logAnalyticsContributor),
                description: intl.formatMessage(PermissionsResources.logAnalyticsContributorDescription),
            },
            [RBACRoleNames.azureMonitorMonitoringContributor]: {
                title: intl.formatMessage(PermissionsResources.azureMonitorMonitoringContributor),
                description: intl.formatMessage(PermissionsResources.azureMonitorMonitoringContributorDescription),
            },
            [RBACRoleNames.postgreSqlFlexibleServerLongTermRetentionBackupRole]: {
                title: intl.formatMessage(PermissionsResources.postgreSqlFlexibleServerLongTermRetentionBackupRole),
                description: intl.formatMessage(PermissionsResources.postgreSqlFlexibleServerLongTermRetentionBackupRoleDescription),
            },
            [RBACRoleNames.sqlManagedInstanceContributor]: {
                title: intl.formatMessage(PermissionsResources.sqlManagedInstanceContributor),
                description: intl.formatMessage(PermissionsResources.sqlManagedInstanceContributorDescription),
            },
            [RBACRoleNames.dataFactoryContributor]: {
                title: intl.formatMessage(PermissionsResources.dataFactoryContributor),
                description: intl.formatMessage(PermissionsResources.dataFactoryContributorDescription),
            },
            [RBACRoleNames.hdInsightOnAksClusterAdmin]: {
                title: intl.formatMessage(PermissionsResources.hdInsightOnAksClusterAdmin),
                description: intl.formatMessage(PermissionsResources.hdInsightOnAksClusterAdminDescription),
            },
            [RBACRoleNames.hdInsightOnAksClusterPoolAdmin]: {
                title: intl.formatMessage(PermissionsResources.hdInsightOnAksClusterPoolAdmin),
                description: intl.formatMessage(PermissionsResources.hdInsightOnAksClusterPoolAdminDescription),
            },
            [RBACRoleNames.azureMlComputeOperator]: {
                title: intl.formatMessage(PermissionsResources.azureMlComputeOperator),
                description: intl.formatMessage(PermissionsResources.azureMlComputeOperatorDescription),
            },
            [RBACRoleNames.azureMlDataScientist]: {
                title: intl.formatMessage(PermissionsResources.azureMlDataScientist),
                description: intl.formatMessage(PermissionsResources.azureMlDataScientistDescription),
            },
            [RBACRoleNames.cognitiveServicesContributor]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesContributor),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesContributorDescription),
            },
            [RBACRoleNames.cognitiveServicesOpenAiContributor]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesOpenAiContributor),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesOpenAiContributorDescription),
            },
            [RBACRoleNames.cognitiveServicesCustomVisionContributor]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesCustomVisionContributor),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesCustomVisionContributorDescription),
            },
            [RBACRoleNames.cognitiveServicesLanguageWriter]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesLanguageWriter),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesLanguageWriterDescription),
            },
            [RBACRoleNames.cognitiveServicesLuisWriter]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesLuisWriter),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesLuisWriterDescription),
            },
            [RBACRoleNames.cognitiveServicesQnaMakerEditor]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesQnaMakerEditor),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesQnaMakerEditorDescription),
            },
            [RBACRoleNames.cognitiveServicesSpeechContributor]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesSpeechContributor),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesSpeechContributorDescription),
            },
            [RBACRoleNames.healthcareAgentEditor]: {
                title: intl.formatMessage(PermissionsResources.healthcareAgentEditor),
                description: intl.formatMessage(PermissionsResources.healthcareAgentEditorDescription),
            },
            [RBACRoleNames.searchServiceContributor]: {
                title: intl.formatMessage(PermissionsResources.searchServiceContributor),
                description: intl.formatMessage(PermissionsResources.searchServiceContributorDescription),
            },
            [RBACRoleNames.azureDigitalTwinsDataOwner]: {
                title: intl.formatMessage(PermissionsResources.azureDigitalTwinsDataOwner),
                description: intl.formatMessage(PermissionsResources.azureDigitalTwinsDataOwnerDescription),
            },
            [RBACRoleNames.deviceProvisioningServiceDataContributor]: {
                title: intl.formatMessage(PermissionsResources.deviceProvisioningServiceDataContributor),
                description: intl.formatMessage(PermissionsResources.deviceProvisioningServiceDataContributorDescription),
            },
            [RBACRoleNames.deviceUpdateAdministrator]: {
                title: intl.formatMessage(PermissionsResources.deviceUpdateAdministrator),
                description: intl.formatMessage(PermissionsResources.deviceUpdateAdministratorDescription),
            },
            [RBACRoleNames.iotHubDataContributor]: {
                title: intl.formatMessage(PermissionsResources.iotHubDataContributor),
                description: intl.formatMessage(PermissionsResources.iotHubDataContributorDescription),
            },
            [RBACRoleNames.iotHubRegistryContributor]: {
                title: intl.formatMessage(PermissionsResources.iotHubRegistryContributor),
                description: intl.formatMessage(PermissionsResources.iotHubRegistryContributorDescription),
            },
            [RBACRoleNames.iotHubTwinContributor]: {
                title: intl.formatMessage(PermissionsResources.iotHubTwinContributor),
                description: intl.formatMessage(PermissionsResources.iotHubTwinContributorDescription),
            },
            [RBACRoleNames.apiManagementServiceContributor]: {
                title: intl.formatMessage(PermissionsResources.apiManagementServiceContributor),
                description: intl.formatMessage(PermissionsResources.apiManagementServiceContributorDescription),
            },
            [RBACRoleNames.apiManagementServiceOperatorRole]: {
                title: intl.formatMessage(PermissionsResources.apiManagementServiceOperatorRole),
                description: intl.formatMessage(PermissionsResources.apiManagementServiceOperatorRoleDescription),
            },
            [RBACRoleNames.apiManagementWorkspaceContributor]: {
                title: intl.formatMessage(PermissionsResources.apiManagementWorkspaceContributor),
                description: intl.formatMessage(PermissionsResources.apiManagementWorkspaceContributorDescription),
            },
            [RBACRoleNames.appConfigurationContributor]: {
                title: intl.formatMessage(PermissionsResources.appConfigurationContributor),
                description: intl.formatMessage(PermissionsResources.appConfigurationContributorDescription),
            },
            [RBACRoleNames.azureServiceBusDataOwner]: {
                title: intl.formatMessage(PermissionsResources.azureServiceBusDataOwner),
                description: intl.formatMessage(PermissionsResources.azureServiceBusDataOwnerDescription),
            },
            [RBACRoleNames.logicAppContributor]: {
                title: intl.formatMessage(PermissionsResources.logicAppContributor),
                description: intl.formatMessage(PermissionsResources.logicAppContributorDescription),
            },
            [RBACRoleNames.workbookContributor]: {
                title: intl.formatMessage(PermissionsResources.workbookContributor),
                description: intl.formatMessage(PermissionsResources.workbookContributorDescription),
            },
            [RBACRoleNames.azureCenterForSapSolutionsAdministrator]: {
                title: intl.formatMessage(PermissionsResources.azureCenterForSapSolutionsAdministrator),
                description: intl.formatMessage(PermissionsResources.azureCenterForSapSolutionsAdministratorDescription),
            },
            [RBACRoleNames.costManagementContributor]: {
                title: intl.formatMessage(PermissionsResources.costManagementContributor),
                description: intl.formatMessage(PermissionsResources.costManagementContributorDescription),
            },
            [RBACRoleNames.hdInsightClusterOperator]: {
                title: intl.formatMessage(PermissionsResources.hdInsightClusterOperator),
                description: intl.formatMessage(PermissionsResources.hdInsightClusterOperatorDescription),
            },
            [RBACRoleNames.cognitiveServicesCustomVisionReader]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesCustomVisionReader),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesCustomVisionReaderDescription),
            },
            [RBACRoleNames.cognitiveServicesDataReader]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesDataReader),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesDataReaderDescription),
            },
            [RBACRoleNames.cognitiveServicesLanguageReader]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesLanguageReader),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesLanguageReaderDescription),
            },
            [RBACRoleNames.cognitiveServicesLuisReader]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesLuisReader),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesLuisReaderDescription),
            },
            [RBACRoleNames.cognitiveServicesQnaMakerReader]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesQnaMakerReader),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesQnaMakerReaderDescription),
            },
            [RBACRoleNames.cognitiveServicesUsagesReader]: {
                title: intl.formatMessage(PermissionsResources.cognitiveServicesUsagesReader),
                description: intl.formatMessage(PermissionsResources.cognitiveServicesUsagesReaderDescription),
            },
            [RBACRoleNames.searchIndexDataReader]: {
                title: intl.formatMessage(PermissionsResources.searchIndexDataReader),
                description: intl.formatMessage(PermissionsResources.searchIndexDataReaderDescription),
            },
            [RBACRoleNames.azureDigitalTwinsDataReader]: {
                title: intl.formatMessage(PermissionsResources.azureDigitalTwinsDataReader),
                description: intl.formatMessage(PermissionsResources.azureDigitalTwinsDataReaderDescription),
            },
            [RBACRoleNames.deviceProvisioningServiceDataReader]: {
                title: intl.formatMessage(PermissionsResources.deviceProvisioningServiceDataReader),
                description: intl.formatMessage(PermissionsResources.deviceProvisioningServiceDataReaderDescription),
            },
            [RBACRoleNames.deviceUpdateReader]: {
                title: intl.formatMessage(PermissionsResources.deviceUpdateReader),
                description: intl.formatMessage(PermissionsResources.deviceUpdateReaderDescription),
            },
            [RBACRoleNames.iotHubDataReader]: {
                title: intl.formatMessage(PermissionsResources.iotHubDataReader),
                description: intl.formatMessage(PermissionsResources.iotHubDataReaderDescription),
            },
            [RBACRoleNames.apiManagementServiceReader]: {
                title: intl.formatMessage(PermissionsResources.apiManagementServiceReader),
                description: intl.formatMessage(PermissionsResources.apiManagementServiceReaderDescription),
            },
            [RBACRoleNames.apiManagementWorkspaceReader]: {
                title: intl.formatMessage(PermissionsResources.apiManagementWorkspaceReader),
                description: intl.formatMessage(PermissionsResources.apiManagementWorkspaceReaderDescription),
            },
            [RBACRoleNames.appConfigurationReader]: {
                title: intl.formatMessage(PermissionsResources.appConfigurationReader),
                description: intl.formatMessage(PermissionsResources.appConfigurationReaderDescription),
            },
            [RBACRoleNames.logicAppOperator]: {
                title: intl.formatMessage(PermissionsResources.logicAppOperator),
                description: intl.formatMessage(PermissionsResources.logicAppOperatorDescription),
            },
            [RBACRoleNames.workbookReader]: {
                title: intl.formatMessage(PermissionsResources.workbookReader),
                description: intl.formatMessage(PermissionsResources.workbookReaderDescription),
            },
            [RBACRoleNames.azureCenterForSapSolutionsReader]: {
                title: intl.formatMessage(PermissionsResources.azureCenterForSapSolutionsReader),
                description: intl.formatMessage(PermissionsResources.azureCenterForSapSolutionsReaderDescription),
            },
            [RBACRoleNames.costManagementReader]: {
                title: intl.formatMessage(PermissionsResources.costManagementReader),
                description: intl.formatMessage(PermissionsResources.costManagementReaderDescription),
            },
        }),
        [intl]
    );

    const getResourceTypesForResourceGroups = useCallback(
        async (resourceGroupIds: string[]) => {
            try {
                const resourceTypes = await ResourceGroupClient.listAllResourcesInResourceGroups(resourceGroupIds);
                return resourceTypes;
            } catch (error) {
                portalContext.log({
                    action: 'GetResourceGroupsFromArg',
                    actionModifier: 'Error',
                });
                return [];
            }
        },
        [portalContext]
    );

    useEffect(() => {
        const fetchResourceTypes = async () => {
            if (managedResourceGroups.length > 0) {
                setIsLoading(true);
                const resourceGroupIds = managedResourceGroups.map(rg => rg.id);
                const resourceTypes = await getResourceTypesForResourceGroups(resourceGroupIds);
                setAllResourceTypes(resourceTypes);
                setIsLoading(false);
            } else {
                setAllResourceTypes([]);
                setBasePermissionsGridItems([]);
            }
        };

        fetchResourceTypes();
    }, [getResourceTypesForResourceGroups, managedResourceGroups]);

    useEffect(() => {
        const allRoleNames = new Set<string>();
        const roleIds = getRoleNamesForResourceGroup(allResourceTypes, accessLevel);
        roleIds.forEach(roleName => allRoleNames.add(roleName));

        const gridItems: RoleGridItem[] = Array.from(allRoleNames)
            .map(roleName => {
                const permission = permissionsMap[roleName];
                if (permission) {
                    return {
                        role: roleName,
                        title: permission.title,
                        description: permission.description,
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
    }, [allResourceTypes, accessLevel, permissionsMap]);

    const columns = React.useMemo<TableColumnDefinition<RoleGridItem>[]>(() => {
        return [
            createTableColumn<RoleGridItem>({
                columnId: RoleListColumnKey.role,
                compare: (a, b) => a.title.localeCompare(b.title),
                renderHeaderCell: () => <span style={{ fontWeight: 600 }}>{intl.formatMessage(PermissionsResources.role)}</span>,
                renderCell: item => (
                    <TableCellLayout>
                        <div className={styles.row}>{item.title}</div>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<RoleGridItem>({
                columnId: RoleListColumnKey.description,
                compare: (a, b) => a.description.localeCompare(b.description),
                renderHeaderCell: () => <span style={{ fontWeight: 600 }}>{intl.formatMessage(PermissionsResources.description)}</span>,
                renderCell: item => (
                    <TableCellLayout>
                        <div className={styles.row}>{item.description}</div>
                    </TableCellLayout>
                ),
            }),
        ];
    }, [intl, styles.row]);

    const columnSizingOptions = React.useMemo(
        () => ({
            [RoleListColumnKey.role]: {
                minWidth: 200,
                idealWidth: 200,
            },
            [RoleListColumnKey.description]: {
                minWidth: 300,
                idealWidth: 500,
            },
        }),
        []
    );

    return (
        <div>
            <div className={localStyles.scrollContainer} data-is-scrollable="true">
                {isLoading ? (
                    <DataGrid
                        items={Array(5).fill({})}
                        columns={columns}
                        getRowId={() => Math.random().toString()}
                        className={localStyles.dataGrid}
                        resizableColumns
                        columnSizingOptions={columnSizingOptions}
                        size="small"
                    >
                        <DataGridHeader className={localStyles.dataGridHeader}>
                            <DataGridRow>
                                {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                            </DataGridRow>
                        </DataGridHeader>
                        <DataGridBody<RoleGridItem>>
                            {() => (
                                <DataGridRow<RoleGridItem>>
                                    {() => (
                                        <DataGridCell>
                                            <SkeletonItem size={16} />
                                        </DataGridCell>
                                    )}
                                </DataGridRow>
                            )}
                        </DataGridBody>
                    </DataGrid>
                ) : (
                    <div className={localStyles.innerScrollContainer}>
                        <DataGrid
                            items={basePermissionsGridItems}
                            columns={columns}
                            getRowId={item => item.role}
                            className={localStyles.dataGrid}
                            resizableColumns
                            sortable
                            columnSizingOptions={columnSizingOptions}
                        >
                            <DataGridHeader className={localStyles.dataGridHeader}>
                                <DataGridRow>
                                    {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                                </DataGridRow>
                            </DataGridHeader>
                            <DataGridBody<RoleGridItem>>
                                {({ item, rowId }) => (
                                    <DataGridRow<RoleGridItem> key={rowId}>
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

export default PermissionsDetailsList;
