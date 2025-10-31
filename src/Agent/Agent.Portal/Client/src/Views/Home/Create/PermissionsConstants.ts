import { MessageDescriptor } from 'react-intl';
import { RBACRoleNames } from '../../../Common/Contracts/Permissions';
import { RolesAndPermissions } from '../../../Strings/Resources';

export const permissionsMap: Record<string, { title: MessageDescriptor; description: MessageDescriptor }> = {
    [RBACRoleNames.reader]: {
        title: RolesAndPermissions.reader,
        description: RolesAndPermissions.readerDescription,
    },
    [RBACRoleNames.monitoringReader]: {
        title: RolesAndPermissions.monitoringReader,
        description: RolesAndPermissions.monitoringReaderDescription,
    },
    [RBACRoleNames.logAnalyticsReader]: {
        title: RolesAndPermissions.logAnalyticsReader,
        description: RolesAndPermissions.logAnalyticsReaderDescription,
    },
    [RBACRoleNames.containerAppsOperator]: {
        title: RolesAndPermissions.containerAppsOperator,
        description: RolesAndPermissions.containerAppsOperatorDescription,
    },
    [RBACRoleNames.azureKubernetesServiceRbacReader]: {
        title: RolesAndPermissions.azureKubernetesServiceRbacReader,
        description: RolesAndPermissions.azureKubernetesServiceRbacReaderDescription,
    },
    [RBACRoleNames.azureKubernetesServiceClusterUser]: {
        title: RolesAndPermissions.azureKubernetesServiceClusterUserRole,
        description: RolesAndPermissions.azureKubernetesServiceClusterUserRoleDescription,
    },
    [RBACRoleNames.containerAppsContributor]: {
        title: RolesAndPermissions.containerAppsContributor,
        description: RolesAndPermissions.containerAppsContributorDescription,
    },
    [RBACRoleNames.azureKubernetesServiceClusterAdmin]: {
        title: RolesAndPermissions.azureKubernetesServiceClusterAdmin,
        description: RolesAndPermissions.azureKubernetesServiceClusterAdminDescription,
    },
    [RBACRoleNames.azureKubernetesServiceRbacClusterAdmin]: {
        title: RolesAndPermissions.azureKubernetesServiceRbacClusterAdmin,
        description: RolesAndPermissions.azureKubernetesServiceRbacClusterAdminDescription,
    },
    [RBACRoleNames.redisCacheContributor]: {
        title: RolesAndPermissions.redisCacheContributor,
        description: RolesAndPermissions.redisCacheContributorDescription,
    },
    [RBACRoleNames.websitesContributor]: {
        title: RolesAndPermissions.websitesContributor,
        description: RolesAndPermissions.websitesContributorDescription,
    },
    [RBACRoleNames.webPlanContributor]: {
        title: RolesAndPermissions.webPlanContributor,
        description: RolesAndPermissions.webPlanContributorDescription,
    },
    [RBACRoleNames.storageBlobDataReader]: {
        title: RolesAndPermissions.storageBlobDataReader,
        description: RolesAndPermissions.storageBlobDataReaderDescription,
    },
    [RBACRoleNames.documentDbAccountContributor]: {
        title: RolesAndPermissions.documentDbAccountContributor,
        description: RolesAndPermissions.documentDbAccountContributorDescription,
    },
    [RBACRoleNames.storageBlobDataContributor]: {
        title: RolesAndPermissions.storageBlobDataContributor,
        description: RolesAndPermissions.storageBlobDataContributorDescription,
    },
    [RBACRoleNames.sqlDbContributor]: {
        title: RolesAndPermissions.sqlDbContributor,
        description: RolesAndPermissions.sqlDbContributorDescription,
    },
    [RBACRoleNames.storageAccountContributor]: {
        title: RolesAndPermissions.storageAccountContributor,
        description: RolesAndPermissions.storageAccountContributorDescription,
    },
    [RBACRoleNames.virtualMachineContributor]: {
        title: RolesAndPermissions.virtualMachineContributor,
        description: RolesAndPermissions.virtualMachineContributorDescription,
    },
    [RBACRoleNames.azureDatabaseForPostgreSqlContributor]: {
        title: RolesAndPermissions.postgreSqlContributor,
        description: RolesAndPermissions.postgreSqlContributorDescription,
    },
    [RBACRoleNames.sqlServerContributor]: {
        title: RolesAndPermissions.sqlServerContributor,
        description: RolesAndPermissions.sqlServerContributorDescription,
    },
    [RBACRoleNames.applicationInsightsComponentContributor]: {
        title: RolesAndPermissions.applicationInsightsComponentContributor,
        description: RolesAndPermissions.applicationInsightsComponentContributorDescription,
    },
    [RBACRoleNames.logAnalyticsContributor]: {
        title: RolesAndPermissions.logAnalyticsContributor,
        description: RolesAndPermissions.logAnalyticsContributorDescription,
    },
    [RBACRoleNames.azureMonitorMonitoringContributor]: {
        title: RolesAndPermissions.azureMonitorMonitoringContributor,
        description: RolesAndPermissions.azureMonitorMonitoringContributorDescription,
    },
    [RBACRoleNames.postgreSqlFlexibleServerLongTermRetentionBackupRole]: {
        title: RolesAndPermissions.postgreSqlFlexibleServerLongTermRetentionBackupRole,
        description: RolesAndPermissions.postgreSqlFlexibleServerLongTermRetentionBackupRoleDescription,
    },
    [RBACRoleNames.sqlManagedInstanceContributor]: {
        title: RolesAndPermissions.sqlManagedInstanceContributor,
        description: RolesAndPermissions.sqlManagedInstanceContributorDescription,
    },
    [RBACRoleNames.dataFactoryContributor]: {
        title: RolesAndPermissions.dataFactoryContributor,
        description: RolesAndPermissions.dataFactoryContributorDescription,
    },
    [RBACRoleNames.hdInsightOnAksClusterAdmin]: {
        title: RolesAndPermissions.hdInsightOnAksClusterAdmin,
        description: RolesAndPermissions.hdInsightOnAksClusterAdminDescription,
    },
    [RBACRoleNames.hdInsightOnAksClusterPoolAdmin]: {
        title: RolesAndPermissions.hdInsightOnAksClusterPoolAdmin,
        description: RolesAndPermissions.hdInsightOnAksClusterPoolAdminDescription,
    },
    [RBACRoleNames.azureMlComputeOperator]: {
        title: RolesAndPermissions.azureMlComputeOperator,
        description: RolesAndPermissions.azureMlComputeOperatorDescription,
    },
    [RBACRoleNames.azureMlDataScientist]: {
        title: RolesAndPermissions.azureMlDataScientist,
        description: RolesAndPermissions.azureMlDataScientistDescription,
    },
    [RBACRoleNames.cognitiveServicesContributor]: {
        title: RolesAndPermissions.cognitiveServicesContributor,
        description: RolesAndPermissions.cognitiveServicesContributorDescription,
    },
    [RBACRoleNames.cognitiveServicesOpenAiContributor]: {
        title: RolesAndPermissions.cognitiveServicesOpenAiContributor,
        description: RolesAndPermissions.cognitiveServicesOpenAiContributorDescription,
    },
    [RBACRoleNames.cognitiveServicesCustomVisionContributor]: {
        title: RolesAndPermissions.cognitiveServicesCustomVisionContributor,
        description: RolesAndPermissions.cognitiveServicesCustomVisionContributorDescription,
    },
    [RBACRoleNames.cognitiveServicesLanguageWriter]: {
        title: RolesAndPermissions.cognitiveServicesLanguageWriter,
        description: RolesAndPermissions.cognitiveServicesLanguageWriterDescription,
    },
    [RBACRoleNames.cognitiveServicesLuisWriter]: {
        title: RolesAndPermissions.cognitiveServicesLuisWriter,
        description: RolesAndPermissions.cognitiveServicesLuisWriterDescription,
    },
    [RBACRoleNames.cognitiveServicesQnaMakerEditor]: {
        title: RolesAndPermissions.cognitiveServicesQnaMakerEditor,
        description: RolesAndPermissions.cognitiveServicesQnaMakerEditorDescription,
    },
    [RBACRoleNames.cognitiveServicesSpeechContributor]: {
        title: RolesAndPermissions.cognitiveServicesSpeechContributor,
        description: RolesAndPermissions.cognitiveServicesSpeechContributorDescription,
    },
    [RBACRoleNames.healthcareAgentEditor]: {
        title: RolesAndPermissions.healthcareAgentEditor,
        description: RolesAndPermissions.healthcareAgentEditorDescription,
    },
    [RBACRoleNames.searchServiceContributor]: {
        title: RolesAndPermissions.searchServiceContributor,
        description: RolesAndPermissions.searchServiceContributorDescription,
    },
    [RBACRoleNames.azureDigitalTwinsDataOwner]: {
        title: RolesAndPermissions.azureDigitalTwinsDataOwner,
        description: RolesAndPermissions.azureDigitalTwinsDataOwnerDescription,
    },
    [RBACRoleNames.deviceProvisioningServiceDataContributor]: {
        title: RolesAndPermissions.deviceProvisioningServiceDataContributor,
        description: RolesAndPermissions.deviceProvisioningServiceDataContributorDescription,
    },
    [RBACRoleNames.deviceUpdateAdministrator]: {
        title: RolesAndPermissions.deviceUpdateAdministrator,
        description: RolesAndPermissions.deviceUpdateAdministratorDescription,
    },
    [RBACRoleNames.iotHubDataContributor]: {
        title: RolesAndPermissions.iotHubDataContributor,
        description: RolesAndPermissions.iotHubDataContributorDescription,
    },
    [RBACRoleNames.iotHubRegistryContributor]: {
        title: RolesAndPermissions.iotHubRegistryContributor,
        description: RolesAndPermissions.iotHubRegistryContributorDescription,
    },
    [RBACRoleNames.iotHubTwinContributor]: {
        title: RolesAndPermissions.iotHubTwinContributor,
        description: RolesAndPermissions.iotHubTwinContributorDescription,
    },
    [RBACRoleNames.apiManagementServiceContributor]: {
        title: RolesAndPermissions.apiManagementServiceContributor,
        description: RolesAndPermissions.apiManagementServiceContributorDescription,
    },
    [RBACRoleNames.apiManagementServiceOperatorRole]: {
        title: RolesAndPermissions.apiManagementServiceOperatorRole,
        description: RolesAndPermissions.apiManagementServiceOperatorRoleDescription,
    },
    [RBACRoleNames.apiManagementWorkspaceContributor]: {
        title: RolesAndPermissions.apiManagementWorkspaceContributor,
        description: RolesAndPermissions.apiManagementWorkspaceContributorDescription,
    },
    [RBACRoleNames.appConfigurationContributor]: {
        title: RolesAndPermissions.appConfigurationContributor,
        description: RolesAndPermissions.appConfigurationContributorDescription,
    },
    [RBACRoleNames.azureServiceBusDataOwner]: {
        title: RolesAndPermissions.azureServiceBusDataOwner,
        description: RolesAndPermissions.azureServiceBusDataOwnerDescription,
    },
    [RBACRoleNames.logicAppContributor]: {
        title: RolesAndPermissions.logicAppContributor,
        description: RolesAndPermissions.logicAppContributorDescription,
    },
    [RBACRoleNames.workbookContributor]: {
        title: RolesAndPermissions.workbookContributor,
        description: RolesAndPermissions.workbookContributorDescription,
    },
    [RBACRoleNames.azureCenterForSapSolutionsAdministrator]: {
        title: RolesAndPermissions.azureCenterForSapSolutionsAdministrator,
        description: RolesAndPermissions.azureCenterForSapSolutionsAdministratorDescription,
    },
    [RBACRoleNames.costManagementContributor]: {
        title: RolesAndPermissions.costManagementContributor,
        description: RolesAndPermissions.costManagementContributorDescription,
    },
    [RBACRoleNames.hdInsightClusterOperator]: {
        title: RolesAndPermissions.hdInsightClusterOperator,
        description: RolesAndPermissions.hdInsightClusterOperatorDescription,
    },
    [RBACRoleNames.cognitiveServicesCustomVisionReader]: {
        title: RolesAndPermissions.cognitiveServicesCustomVisionReader,
        description: RolesAndPermissions.cognitiveServicesCustomVisionReaderDescription,
    },
    [RBACRoleNames.cognitiveServicesDataReader]: {
        title: RolesAndPermissions.cognitiveServicesDataReader,
        description: RolesAndPermissions.cognitiveServicesDataReaderDescription,
    },
    [RBACRoleNames.cognitiveServicesLanguageReader]: {
        title: RolesAndPermissions.cognitiveServicesLanguageReader,
        description: RolesAndPermissions.cognitiveServicesLanguageReaderDescription,
    },
    [RBACRoleNames.cognitiveServicesLuisReader]: {
        title: RolesAndPermissions.cognitiveServicesLuisReader,
        description: RolesAndPermissions.cognitiveServicesLuisReaderDescription,
    },
    [RBACRoleNames.cognitiveServicesQnaMakerReader]: {
        title: RolesAndPermissions.cognitiveServicesQnaMakerReader,
        description: RolesAndPermissions.cognitiveServicesQnaMakerReaderDescription,
    },
    [RBACRoleNames.cognitiveServicesUsagesReader]: {
        title: RolesAndPermissions.cognitiveServicesUsagesReader,
        description: RolesAndPermissions.cognitiveServicesUsagesReaderDescription,
    },
    [RBACRoleNames.searchIndexDataReader]: {
        title: RolesAndPermissions.searchIndexDataReader,
        description: RolesAndPermissions.searchIndexDataReaderDescription,
    },
    [RBACRoleNames.azureDigitalTwinsDataReader]: {
        title: RolesAndPermissions.azureDigitalTwinsDataReader,
        description: RolesAndPermissions.azureDigitalTwinsDataReaderDescription,
    },
    [RBACRoleNames.deviceProvisioningServiceDataReader]: {
        title: RolesAndPermissions.deviceProvisioningServiceDataReader,
        description: RolesAndPermissions.deviceProvisioningServiceDataReaderDescription,
    },
    [RBACRoleNames.deviceUpdateReader]: {
        title: RolesAndPermissions.deviceUpdateReader,
        description: RolesAndPermissions.deviceUpdateReaderDescription,
    },
    [RBACRoleNames.iotHubDataReader]: {
        title: RolesAndPermissions.iotHubDataReader,
        description: RolesAndPermissions.iotHubDataReaderDescription,
    },
    [RBACRoleNames.apiManagementServiceReader]: {
        title: RolesAndPermissions.apiManagementServiceReader,
        description: RolesAndPermissions.apiManagementServiceReaderDescription,
    },
    [RBACRoleNames.apiManagementWorkspaceReader]: {
        title: RolesAndPermissions.apiManagementWorkspaceReader,
        description: RolesAndPermissions.apiManagementWorkspaceReaderDescription,
    },
    [RBACRoleNames.appConfigurationReader]: {
        title: RolesAndPermissions.appConfigurationReader,
        description: RolesAndPermissions.appConfigurationReaderDescription,
    },
    [RBACRoleNames.logicAppOperator]: {
        title: RolesAndPermissions.logicAppOperator,
        description: RolesAndPermissions.logicAppOperatorDescription,
    },
    [RBACRoleNames.workbookReader]: {
        title: RolesAndPermissions.workbookReader,
        description: RolesAndPermissions.workbookReaderDescription,
    },
    [RBACRoleNames.azureCenterForSapSolutionsReader]: {
        title: RolesAndPermissions.azureCenterForSapSolutionsReader,
        description: RolesAndPermissions.azureCenterForSapSolutionsReaderDescription,
    },
    [RBACRoleNames.costManagementReader]: {
        title: RolesAndPermissions.costManagementReader,
        description: RolesAndPermissions.costManagementReaderDescription,
    },
};
