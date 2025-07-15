import { AntUxStringComparison, equals } from '../../Helpers/Strings';
import { AgentAccessLevel } from './SreAgent';

export interface PermissionsCheckResponse {
    value: [
        {
            actions: string[];
            notActions: string[];
            dataActions: string[];
            notDataActions: string[];
        },
    ];
}

export enum PermissionActions {
    RbacWrite = 'Microsoft.Authorization/roleAssignments/write',
}

export enum PermissionPrincipalType {
    servicePrincipal = 'ServicePrincipal',
}

export type RoleAssignment = {
    createdOn: string;
    roleDefinitionId: string;
    updatedBy: string;
    scope: string;
    principalId?: string;
    principalType?: string;
};

export enum RBACRoleIds {
    owner = '8e3af657-a8ff-443c-a75c-2fe8c4bcb635',
    contributor = 'b24988ac-6180-42a0-ab88-20f7382dd24c',
    reader = 'acdd72a7-3385-48ef-bd42-f606fba81ae7',
    containerAppsContributor = '358470bc-b998-42bd-ab17-a7e34c199c0f',
    logAnalyticsReader = '73c42c96-874c-492b-b04d-ab87d138a893',
    websitesContributor = 'de139f84-1756-47ae-9be6-808fbbe84772',
    webPlanContributor = '2cc479cb-7b4d-49a8-b449-8c00fd0f0a4b',
    azureKubernetesServiceRbacReader = '7f6c6a51-bcf8-42ba-9220-52d62157d7db',
    azureKubernetesServiceClusterUser = '4abbcc35-e782-43d8-92c5-2d3f1bd2253f',
    azureKubernetesServiceClusterAdmin = '0ab0b1a8-8aac-4efd-b8c2-3ee1fb270be8',
    azureKubernetesServiceRbacClusterAdmin = 'b1ff04bb-8a4e-4dc4-8eb5-8693973ce19b',
    containerAppsOperator = 'f3bd1b5c-91fa-40e7-afe7-0c11d331232c',
    azureMonitorMonitoringContributor = '749f88d5-cbae-40b8-bcfc-e573ddc772fa',
    applicationInsightsComponentContributor = 'ae349356-3a1b-4a5e-921d-050484c6347e',
    logAnalyticsContributor = '92aaf0da-9dab-42b6-94a3-d43ce8d16293',
    storageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe',
    documentDbAccountContributor = '5bd9cd88-fe45-4216-938b-f97437e15450',
    redisCacheContributor = 'e0f68234-74aa-48ed-b826-c38b57376e17',
    sqlDbContributor = '9b7fa17d-e63e-47b0-bb0a-15c516ac86ec',
    monitoringReader = '43d0d8ad-25c7-4714-9337-8ba259a9fe05',
    storageBlobDataReader = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1',
    virtualMachineContributor = '9980e02c-c2be-4d73-94e8-173b1dc7cf3c',
    azureDatabaseForPostgreSqlContributor = '5d1d47af-2513-43cb-ba26-d8d6d6d6d6d6',
    sqlServerContributor = '6d8ee4ec-f05a-4a1d-8b00-a9b17e38b437',
    storageAccountContributor = '17d1049b-9a84-46fb-8f53-869881c3d3ab',
    postgreSqlFlexibleServerLongTermRetentionBackupRole = 'c088a766-074b-43ba-90d4-1fb21feae531',
    sqlManagedInstanceContributor = '4939a1f6-9ae0-4e48-a1e0-f2cbe897382d',
    dataFactoryContributor = '673868aa-7521-48a0-acc6-0f60742d39f5',
    hdInsightOnAksClusterAdmin = 'fd036e6b-1266-47a0-b0bb-a05d04831731',
    hdInsightOnAksClusterPoolAdmin = '7656b436-37d4-490a-a4ab-d39f838f0042',
    azureMlComputeOperator = 'e503ece1-11d0-4e8e-8e2c-7a6c3bf38815',
    azureMlDataScientist = 'f6c7c914-8db3-469d-8ca1-694a8f32e121',
    cognitiveServicesContributor = '25fbc0a9-bd7c-42a3-aa1a-3b75d497ee68',
    cognitiveServicesOpenAiContributor = 'a001fd3d-188f-4b5d-821b-7da978bf7442',
    cognitiveServicesCustomVisionContributor = 'c1ff6cc2-c111-46fe-8896-e0ef812ad9f3',
    cognitiveServicesLanguageWriter = 'f2310ca1-dc64-4889-bb49-c8e0fa3d47a8',
    cognitiveServicesLuisWriter = '6322a993-d5c9-4bed-b113-e49bbea25b27',
    cognitiveServicesQnaMakerEditor = 'f4cc2bf9-21be-47a1-bdf1-5c5804381025',
    cognitiveServicesSpeechContributor = '0e75ca1e-0464-4b4d-8b93-68208a576181',
    healthcareAgentEditor = 'af854a69-80ce-4ff7-8447-f1118a2e0ca8',
    searchServiceContributor = '7ca78c08-252a-4471-8644-bb5ff32d4ba0',
    azureDigitalTwinsDataOwner = 'bcd981a7-7f74-457b-83e1-cceb9e632ffe',
    deviceProvisioningServiceDataContributor = 'dfce44e4-17b7-4bd1-a6d1-04996ec95633',
    deviceUpdateAdministrator = '02ca0879-e8e4-47a5-a61e-5c618b76e64a',
    iotHubDataContributor = '4fc6c259-987e-4a07-842e-c321cc9d413f',
    iotHubRegistryContributor = '4ea46cd5-c1b2-4a8e-910b-273211f9ce47',
    iotHubTwinContributor = '494bdba2-168f-4f31-a0a1-191d2f7c028c',
    apiManagementServiceContributor = '312a565d-c81f-4fd8-895a-4e21e48d571c',
    apiManagementServiceOperatorRole = 'e022efe7-f5ba-4159-bbe4-b44f577e9b61',
    apiManagementWorkspaceContributor = '0c34c906-8d99-4cb7-8bb7-33f5b0a1a799',
    appConfigurationContributor = 'fe86443c-f201-4fc4-9d2a-ac61149fbda0',
    azureServiceBusDataOwner = '090c5cfd-751d-490a-894a-3ce6f1109419',
    logicAppContributor = '87a39d53-fc1b-424a-814c-f7e04687dc9e',
    workbookContributor = 'e8ddcd69-c73f-4f9f-9844-4100522f16ad',
    azureCenterForSapSolutionsAdministrator = '7b0c7e81-271f-4c71-90bf-e30bdfdbc2f7',
    costManagementContributor = '434105ed-43f6-45c7-a02f-909b2ba83430',
    hdInsightClusterOperator = '61ed4efc-fab3-44fd-b111-e24485cc132a',
    cognitiveServicesCustomVisionReader = '93586559-c37d-4a6b-ba08-b9f0940c2d73',
    cognitiveServicesDataReader = 'b59867f0-fa02-499b-be73-45a86b5b3e1c',
    cognitiveServicesLanguageReader = '7628b7b8-a8b2-4cdc-b46f-e9b35248918e',
    cognitiveServicesLuisReader = '18e81cdc-4e98-4e29-a639-e7d10c5a6226',
    cognitiveServicesQnaMakerReader = '466ccd10-b268-4a11-b098-b4849f024126',
    cognitiveServicesUsagesReader = 'bba48692-92b0-4667-a9ad-c31c7b334ac2',
    searchIndexDataReader = '1407120a-92aa-4202-b7e9-c0e197c71c8f',
    azureDigitalTwinsDataReader = 'd57506d4-4c8d-48b1-8587-93c323f6a5a3',
    deviceProvisioningServiceDataReader = '10745317-c249-44a1-a5ce-3a4353c0bbd8',
    deviceUpdateReader = 'e9dba6fb-3d52-4cf0-bce3-f06ce71b9e0f',
    iotHubDataReader = 'b447c946-2db7-41ec-983d-d8bf3b1c77e3',
    apiManagementServiceReader = '71522526-b88f-4d52-b57f-d31fc3546d0d',
    apiManagementWorkspaceReader = 'ef1c2c96-4a77-49e8-b9a4-6179fe1d2fd2',
    appConfigurationReader = '175b81b9-6e0d-490a-85e4-0d422273c10c',
    logicAppOperator = '515c2055-d9d4-4321-b1b9-bd0c9a0f79fe',
    workbookReader = 'b279062a-9be3-42a0-92ae-8b3cf002ec4d',
    azureCenterForSapSolutionsReader = '05352d14-a920-4328-a0de-4cbe7430e26b',
    costManagementReader = '72fafb9e-0641-4937-9268-a91bfd8191a3',
}

export enum RBACRoleNames {
    contributor = 'Contributor',
    containerAppsContributor = 'ContainerAppsContributor',
    logAnalyticsReader = 'LogAnalyticsReader',
    websitesContributor = 'WebsitesContributor',
    webPlanContributor = 'WebPlanContributor',
    reader = 'Reader',
    containerAppsOperator = 'ContainerAppsOperator',
    azureKubernetesServiceRbacReader = 'AzureKubernetesServiceRbacReader',
    azureKubernetesServiceClusterUser = 'AzureKubernetesServiceClusterUser',
    azureKubernetesServiceClusterAdmin = 'AzureKubernetesServiceClusterAdmin',
    azureKubernetesServiceRbacClusterAdmin = 'AzureKubernetesServiceRbacClusterAdmin',
    azureMonitorMonitoringContributor = 'AzureMonitorMonitoringContributor',
    applicationInsightsComponentContributor = 'ApplicationInsightsComponentContributor',
    logAnalyticsContributor = 'LogAnalyticsContributor',
    storageBlobDataContributor = 'StorageBlobDataContributor',
    documentDbAccountContributor = 'DocumentDbAccountContributor',
    redisCacheContributor = 'RedisCacheContributor',
    sqlDbContributor = 'SqlDbContributor',
    monitoringReader = 'MonitoringReader',
    storageBlobDataReader = 'StorageBlobDataReader',
    virtualMachineContributor = 'VirtualMachineContributor',
    azureDatabaseForPostgreSqlContributor = 'AzureDatabaseForPostgreSqlContributor',
    sqlServerContributor = 'SqlServerContributor',
    storageAccountContributor = 'StorageAccountContributor',
    postgreSqlFlexibleServerLongTermRetentionBackupRole = 'PostgreSQLFlexibleServerLongTermRetentionBackupRole',
    sqlManagedInstanceContributor = 'SqlManagedInstanceContributor',
    dataFactoryContributor = 'DataFactoryContributor',
    hdInsightOnAksClusterAdmin = 'HDInsightOnAKSClusterAdmin',
    hdInsightOnAksClusterPoolAdmin = 'HDInsightOnAKSClusterPoolAdmin',
    azureMlComputeOperator = 'AzureMLComputeOperator',
    azureMlDataScientist = 'AzureMLDataScientist',
    cognitiveServicesContributor = 'CognitiveServicesContributor',
    cognitiveServicesOpenAiContributor = 'CognitiveServicesOpenAIContributor',
    cognitiveServicesCustomVisionContributor = 'CognitiveServicesCustomVisionContributor',
    cognitiveServicesLanguageWriter = 'CognitiveServicesLanguageWriter',
    cognitiveServicesLuisWriter = 'CognitiveServicesLUISWriter',
    cognitiveServicesQnaMakerEditor = 'CognitiveServicesQnAMakerEditor',
    cognitiveServicesSpeechContributor = 'CognitiveServicesSpeechContributor',
    healthcareAgentEditor = 'HealthcareAgentEditor',
    searchServiceContributor = 'SearchServiceContributor',
    azureDigitalTwinsDataOwner = 'AzureDigitalTwinsDataOwner',
    deviceProvisioningServiceDataContributor = 'DeviceProvisioningServiceDataContributor',
    deviceUpdateAdministrator = 'DeviceUpdateAdministrator',
    iotHubDataContributor = 'IoTHubDataContributor',
    iotHubRegistryContributor = 'IoTHubRegistryContributor',
    iotHubTwinContributor = 'IoTHubTwinContributor',
    apiManagementServiceContributor = 'APIManagementServiceContributor',
    apiManagementServiceOperatorRole = 'APIManagementServiceOperatorRole',
    apiManagementWorkspaceContributor = 'APIManagementWorkspaceContributor',
    appConfigurationContributor = 'AppConfigurationContributor',
    azureServiceBusDataOwner = 'AzureServiceBusDataOwner',
    logicAppContributor = 'LogicAppContributor',
    monitoringContributor = 'MonitoringContributor',
    workbookContributor = 'WorkbookContributor',
    azureCenterForSapSolutionsAdministrator = 'AzureCenterForSAPSolutionsAdministrator',
    costManagementContributor = 'CostManagementContributor',
    hdInsightClusterOperator = 'HDInsightClusterOperator',
    cognitiveServicesCustomVisionReader = 'CognitiveServicesCustomVisionReader',
    cognitiveServicesDataReader = 'CognitiveServicesDataReader',
    cognitiveServicesLanguageReader = 'CognitiveServicesLanguageReader',
    cognitiveServicesLuisReader = 'CognitiveServicesLUISReader',
    cognitiveServicesQnaMakerReader = 'CognitiveServicesQnAMakerReader',
    cognitiveServicesUsagesReader = 'CognitiveServicesUsagesReader',
    searchIndexDataReader = 'SearchIndexDataReader',
    azureDigitalTwinsDataReader = 'AzureDigitalTwinsDataReader',
    deviceProvisioningServiceDataReader = 'DeviceProvisioningServiceDataReader',
    deviceUpdateReader = 'DeviceUpdateReader',
    iotHubDataReader = 'IoTHubDataReader',
    apiManagementServiceReader = 'APIManagementServiceReader',
    apiManagementWorkspaceReader = 'APIManagementWorkspaceReader',
    appConfigurationReader = 'AppConfigurationReader',
    logicAppOperator = 'LogicAppOperator',
    workbookReader = 'WorkbookReader',
    azureCenterForSapSolutionsReader = 'AzureCenterForSAPSolutionsReader',
    costManagementReader = 'CostManagementReader',
}

export enum ResourceTypes {
    WebsiteOrFunctionApp = 'microsoft.web/sites',
    StorageAccounts = 'microsoft.storage/storageaccounts',
    StorageBlobContainers = 'microsoft.storage/storageaccounts/blobservices/containers',
    CosmosDbAccounts = 'microsoft.documentdb/databaseaccounts',
    SqlServers = 'microsoft.sql/servers',
    SqlDatabases = 'microsoft.sql/servers/databases',
    SqlManagedInstances = 'microsoft.sql/managedinstances',
    AksClusters = 'microsoft.containerservice/managedclusters',
    ContainerApps = 'microsoft.app/containerapps',
    RedisCache = 'microsoft.cache/redis',
    VirtualMachines = 'microsoft.compute/virtualmachines',
    PostgreSqlFlexibleServers = 'microsoft.dbforpostgresql/flexibleservers',
    PostgreSqlServers = 'microsoft.dbforpostgresql/servers',
    DataFactories = 'microsoft.datafactory/factories',
    MachineLearningWorkspaces = 'microsoft.machinelearningservices/workspaces',
    CognitiveServices = 'microsoft.cognitiveservices/accounts',
    SearchServices = 'microsoft.search/searchservices',
    DigitalTwins = 'microsoft.digitaltwins/digitaltwinsinstances',
    IotHubs = 'microsoft.devices/iothubs',
    ApiManagement = 'microsoft.apimanagement/service',
    AppConfiguration = 'microsoft.appconfiguration/configurationstores',
    ServiceBus = 'microsoft.servicebus/namespaces',
    LogicApps = 'microsoft.logic/workflows',
    ApplicationInsights = 'microsoft.insights/components',
    LogAnalyticsWorkspaces = 'microsoft.operationalinsights/workspaces',
    Insights = 'microsoft.insights',
    Workbooks = 'microsoft.insights/workbooks',
    SapVirtualInstances = 'microsoft.workloads/sapvirtualinstances',
    CostManagement = 'microsoft.costmanagement',
}

export const ResourceTypeToReaderRBACRoleNameMap: Record<string, string[]> = {
    [ResourceTypes.StorageAccounts]: [RBACRoleNames.storageBlobDataReader],
    [ResourceTypes.StorageBlobContainers]: [RBACRoleNames.storageBlobDataReader],
    [ResourceTypes.AksClusters]: [
        RBACRoleNames.azureKubernetesServiceRbacReader,
        RBACRoleNames.azureKubernetesServiceClusterUser,
        RBACRoleNames.hdInsightClusterOperator,
    ],
    [ResourceTypes.ContainerApps]: [RBACRoleNames.containerAppsOperator],
    [ResourceTypes.CognitiveServices]: [
        RBACRoleNames.cognitiveServicesCustomVisionReader,
        RBACRoleNames.cognitiveServicesDataReader,
        RBACRoleNames.cognitiveServicesLanguageReader,
        RBACRoleNames.cognitiveServicesLuisReader,
        RBACRoleNames.cognitiveServicesQnaMakerReader,
        RBACRoleNames.cognitiveServicesUsagesReader,
    ],
    [ResourceTypes.SearchServices]: [RBACRoleNames.searchIndexDataReader],
    [ResourceTypes.DigitalTwins]: [RBACRoleNames.azureDigitalTwinsDataReader],
    [ResourceTypes.IotHubs]: [
        RBACRoleNames.deviceProvisioningServiceDataReader,
        RBACRoleNames.deviceUpdateReader,
        RBACRoleNames.iotHubDataReader,
    ],
    [ResourceTypes.ApiManagement]: [RBACRoleNames.apiManagementServiceReader, RBACRoleNames.apiManagementWorkspaceReader],
    [ResourceTypes.AppConfiguration]: [RBACRoleNames.appConfigurationReader],
    [ResourceTypes.LogicApps]: [RBACRoleNames.logicAppOperator],
    [ResourceTypes.Insights]: [RBACRoleNames.monitoringReader],
    [ResourceTypes.Workbooks]: [RBACRoleNames.workbookReader],
    [ResourceTypes.SapVirtualInstances]: [RBACRoleNames.azureCenterForSapSolutionsReader],
    [ResourceTypes.CostManagement]: [RBACRoleNames.costManagementReader],
};

export const ResourceTypeToContributorRBACRoleNameMap: Record<string, string[]> = {
    [ResourceTypes.WebsiteOrFunctionApp]: [RBACRoleNames.websitesContributor, RBACRoleNames.webPlanContributor],
    [ResourceTypes.StorageAccounts]: [RBACRoleNames.storageAccountContributor],
    [ResourceTypes.StorageBlobContainers]: [RBACRoleNames.storageBlobDataContributor],
    [ResourceTypes.ContainerApps]: [RBACRoleNames.containerAppsContributor],
    [ResourceTypes.AksClusters]: [
        RBACRoleNames.azureKubernetesServiceRbacReader,
        RBACRoleNames.azureKubernetesServiceClusterUser,
        RBACRoleNames.azureKubernetesServiceClusterAdmin,
        RBACRoleNames.hdInsightOnAksClusterAdmin,
        RBACRoleNames.hdInsightOnAksClusterPoolAdmin,
    ],
    [ResourceTypes.RedisCache]: [RBACRoleNames.redisCacheContributor],
    [ResourceTypes.SqlDatabases]: [RBACRoleNames.sqlDbContributor],
    [ResourceTypes.CosmosDbAccounts]: [RBACRoleNames.documentDbAccountContributor],
    [ResourceTypes.PostgreSqlFlexibleServers]: [RBACRoleNames.postgreSqlFlexibleServerLongTermRetentionBackupRole],
    [ResourceTypes.SqlManagedInstances]: [RBACRoleNames.sqlManagedInstanceContributor],
    [ResourceTypes.SqlServers]: [RBACRoleNames.sqlServerContributor],
    [ResourceTypes.DataFactories]: [RBACRoleNames.dataFactoryContributor],
    [ResourceTypes.MachineLearningWorkspaces]: [RBACRoleNames.azureMlComputeOperator, RBACRoleNames.azureMlDataScientist],
    [ResourceTypes.CognitiveServices]: [
        RBACRoleNames.cognitiveServicesContributor,
        RBACRoleNames.cognitiveServicesOpenAiContributor,
        RBACRoleNames.cognitiveServicesCustomVisionContributor,
        RBACRoleNames.cognitiveServicesLanguageWriter,
        RBACRoleNames.cognitiveServicesLuisWriter,
        RBACRoleNames.cognitiveServicesQnaMakerEditor,
        RBACRoleNames.cognitiveServicesSpeechContributor,
    ],
    [ResourceTypes.SearchServices]: [RBACRoleNames.searchServiceContributor],
    [ResourceTypes.DigitalTwins]: [RBACRoleNames.azureDigitalTwinsDataOwner],
    [ResourceTypes.IotHubs]: [
        RBACRoleNames.deviceProvisioningServiceDataContributor,
        RBACRoleNames.deviceUpdateAdministrator,
        RBACRoleNames.iotHubDataContributor,
        RBACRoleNames.iotHubRegistryContributor,
        RBACRoleNames.iotHubTwinContributor,
    ],
    [ResourceTypes.ApiManagement]: [
        RBACRoleNames.apiManagementServiceContributor,
        RBACRoleNames.apiManagementServiceOperatorRole,
        RBACRoleNames.apiManagementWorkspaceContributor,
    ],
    [ResourceTypes.AppConfiguration]: [RBACRoleNames.appConfigurationContributor],
    [ResourceTypes.ServiceBus]: [RBACRoleNames.azureServiceBusDataOwner],
    [ResourceTypes.LogicApps]: [RBACRoleNames.logicAppContributor],
    [ResourceTypes.ApplicationInsights]: [RBACRoleNames.applicationInsightsComponentContributor],
    [ResourceTypes.LogAnalyticsWorkspaces]: [RBACRoleNames.logAnalyticsContributor],
    [ResourceTypes.Insights]: [RBACRoleNames.azureMonitorMonitoringContributor],
    [ResourceTypes.Workbooks]: [RBACRoleNames.workbookContributor],
    [ResourceTypes.SapVirtualInstances]: [RBACRoleNames.azureCenterForSapSolutionsAdministrator],
    [ResourceTypes.CostManagement]: [RBACRoleNames.costManagementContributor],
    [ResourceTypes.VirtualMachines]: [RBACRoleNames.virtualMachineContributor],
    [ResourceTypes.PostgreSqlServers]: [RBACRoleNames.azureDatabaseForPostgreSqlContributor],
};

export const RBACRoleNameToIdMap: Record<RBACRoleNames, string> = {
    [RBACRoleNames.contributor]: RBACRoleIds.contributor,
    [RBACRoleNames.reader]: RBACRoleIds.reader,
    [RBACRoleNames.containerAppsContributor]: RBACRoleIds.containerAppsContributor,
    [RBACRoleNames.logAnalyticsReader]: RBACRoleIds.logAnalyticsReader,
    [RBACRoleNames.websitesContributor]: RBACRoleIds.websitesContributor,
    [RBACRoleNames.webPlanContributor]: RBACRoleIds.webPlanContributor,
    [RBACRoleNames.azureKubernetesServiceRbacReader]: RBACRoleIds.azureKubernetesServiceRbacReader,
    [RBACRoleNames.azureKubernetesServiceClusterUser]: RBACRoleIds.azureKubernetesServiceClusterUser,
    [RBACRoleNames.azureKubernetesServiceClusterAdmin]: RBACRoleIds.azureKubernetesServiceClusterAdmin,
    [RBACRoleNames.azureKubernetesServiceRbacClusterAdmin]: RBACRoleIds.azureKubernetesServiceRbacClusterAdmin,
    [RBACRoleNames.containerAppsOperator]: RBACRoleIds.containerAppsOperator,
    [RBACRoleNames.azureMonitorMonitoringContributor]: RBACRoleIds.azureMonitorMonitoringContributor,
    [RBACRoleNames.applicationInsightsComponentContributor]: RBACRoleIds.applicationInsightsComponentContributor,
    [RBACRoleNames.logAnalyticsContributor]: RBACRoleIds.logAnalyticsContributor,
    [RBACRoleNames.storageBlobDataContributor]: RBACRoleIds.storageBlobDataContributor,
    [RBACRoleNames.documentDbAccountContributor]: RBACRoleIds.documentDbAccountContributor,
    [RBACRoleNames.redisCacheContributor]: RBACRoleIds.redisCacheContributor,
    [RBACRoleNames.sqlDbContributor]: RBACRoleIds.sqlDbContributor,
    [RBACRoleNames.monitoringReader]: RBACRoleIds.monitoringReader,
    [RBACRoleNames.storageBlobDataReader]: RBACRoleIds.storageBlobDataReader,
    [RBACRoleNames.virtualMachineContributor]: RBACRoleIds.virtualMachineContributor,
    [RBACRoleNames.azureDatabaseForPostgreSqlContributor]: RBACRoleIds.azureDatabaseForPostgreSqlContributor,
    [RBACRoleNames.sqlServerContributor]: RBACRoleIds.sqlServerContributor,
    [RBACRoleNames.storageAccountContributor]: RBACRoleIds.storageAccountContributor,
    [RBACRoleNames.postgreSqlFlexibleServerLongTermRetentionBackupRole]: RBACRoleIds.postgreSqlFlexibleServerLongTermRetentionBackupRole,
    [RBACRoleNames.sqlManagedInstanceContributor]: RBACRoleIds.sqlManagedInstanceContributor,
    [RBACRoleNames.dataFactoryContributor]: RBACRoleIds.dataFactoryContributor,
    [RBACRoleNames.hdInsightOnAksClusterAdmin]: RBACRoleIds.hdInsightOnAksClusterAdmin,
    [RBACRoleNames.hdInsightOnAksClusterPoolAdmin]: RBACRoleIds.hdInsightOnAksClusterPoolAdmin,
    [RBACRoleNames.azureMlComputeOperator]: RBACRoleIds.azureMlComputeOperator,
    [RBACRoleNames.azureMlDataScientist]: RBACRoleIds.azureMlDataScientist,
    [RBACRoleNames.cognitiveServicesContributor]: RBACRoleIds.cognitiveServicesContributor,
    [RBACRoleNames.cognitiveServicesOpenAiContributor]: RBACRoleIds.cognitiveServicesOpenAiContributor,
    [RBACRoleNames.cognitiveServicesCustomVisionContributor]: RBACRoleIds.cognitiveServicesCustomVisionContributor,
    [RBACRoleNames.cognitiveServicesLanguageWriter]: RBACRoleIds.cognitiveServicesLanguageWriter,
    [RBACRoleNames.cognitiveServicesLuisWriter]: RBACRoleIds.cognitiveServicesLuisWriter,
    [RBACRoleNames.cognitiveServicesQnaMakerEditor]: RBACRoleIds.cognitiveServicesQnaMakerEditor,
    [RBACRoleNames.cognitiveServicesSpeechContributor]: RBACRoleIds.cognitiveServicesSpeechContributor,
    [RBACRoleNames.healthcareAgentEditor]: RBACRoleIds.healthcareAgentEditor,
    [RBACRoleNames.searchServiceContributor]: RBACRoleIds.searchServiceContributor,
    [RBACRoleNames.azureDigitalTwinsDataOwner]: RBACRoleIds.azureDigitalTwinsDataOwner,
    [RBACRoleNames.deviceProvisioningServiceDataContributor]: RBACRoleIds.deviceProvisioningServiceDataContributor,
    [RBACRoleNames.deviceUpdateAdministrator]: RBACRoleIds.deviceUpdateAdministrator,
    [RBACRoleNames.iotHubDataContributor]: RBACRoleIds.iotHubDataContributor,
    [RBACRoleNames.iotHubRegistryContributor]: RBACRoleIds.iotHubRegistryContributor,
    [RBACRoleNames.iotHubTwinContributor]: RBACRoleIds.iotHubTwinContributor,
    [RBACRoleNames.apiManagementServiceContributor]: RBACRoleIds.apiManagementServiceContributor,
    [RBACRoleNames.apiManagementServiceOperatorRole]: RBACRoleIds.apiManagementServiceOperatorRole,
    [RBACRoleNames.apiManagementWorkspaceContributor]: RBACRoleIds.apiManagementWorkspaceContributor,
    [RBACRoleNames.appConfigurationContributor]: RBACRoleIds.appConfigurationContributor,
    [RBACRoleNames.azureServiceBusDataOwner]: RBACRoleIds.azureServiceBusDataOwner,
    [RBACRoleNames.logicAppContributor]: RBACRoleIds.logicAppContributor,
    [RBACRoleNames.monitoringContributor]: RBACRoleIds.azureMonitorMonitoringContributor,
    [RBACRoleNames.workbookContributor]: RBACRoleIds.workbookContributor,
    [RBACRoleNames.azureCenterForSapSolutionsAdministrator]: RBACRoleIds.azureCenterForSapSolutionsAdministrator,
    [RBACRoleNames.costManagementContributor]: RBACRoleIds.costManagementContributor,
    [RBACRoleNames.hdInsightClusterOperator]: RBACRoleIds.hdInsightClusterOperator,
    [RBACRoleNames.cognitiveServicesCustomVisionReader]: RBACRoleIds.cognitiveServicesCustomVisionReader,
    [RBACRoleNames.cognitiveServicesDataReader]: RBACRoleIds.cognitiveServicesDataReader,
    [RBACRoleNames.cognitiveServicesLanguageReader]: RBACRoleIds.cognitiveServicesLanguageReader,
    [RBACRoleNames.cognitiveServicesLuisReader]: RBACRoleIds.cognitiveServicesLuisReader,
    [RBACRoleNames.cognitiveServicesQnaMakerReader]: RBACRoleIds.cognitiveServicesQnaMakerReader,
    [RBACRoleNames.cognitiveServicesUsagesReader]: RBACRoleIds.cognitiveServicesUsagesReader,
    [RBACRoleNames.searchIndexDataReader]: RBACRoleIds.searchIndexDataReader,
    [RBACRoleNames.azureDigitalTwinsDataReader]: RBACRoleIds.azureDigitalTwinsDataReader,
    [RBACRoleNames.deviceProvisioningServiceDataReader]: RBACRoleIds.deviceProvisioningServiceDataReader,
    [RBACRoleNames.deviceUpdateReader]: RBACRoleIds.deviceUpdateReader,
    [RBACRoleNames.iotHubDataReader]: RBACRoleIds.iotHubDataReader,
    [RBACRoleNames.apiManagementServiceReader]: RBACRoleIds.apiManagementServiceReader,
    [RBACRoleNames.apiManagementWorkspaceReader]: RBACRoleIds.apiManagementWorkspaceReader,
    [RBACRoleNames.appConfigurationReader]: RBACRoleIds.appConfigurationReader,
    [RBACRoleNames.logicAppOperator]: RBACRoleIds.logicAppOperator,
    [RBACRoleNames.workbookReader]: RBACRoleIds.workbookReader,
    [RBACRoleNames.azureCenterForSapSolutionsReader]: RBACRoleIds.azureCenterForSapSolutionsReader,
    [RBACRoleNames.costManagementReader]: RBACRoleIds.costManagementReader,
};

export const RBACRoleIdToNameMap: Record<string, RBACRoleNames> = {
    [RBACRoleIds.contributor]: RBACRoleNames.contributor,
    [RBACRoleIds.reader]: RBACRoleNames.reader,
    [RBACRoleIds.containerAppsContributor]: RBACRoleNames.containerAppsContributor,
    [RBACRoleIds.logAnalyticsReader]: RBACRoleNames.logAnalyticsReader,
    [RBACRoleIds.websitesContributor]: RBACRoleNames.websitesContributor,
    [RBACRoleIds.webPlanContributor]: RBACRoleNames.webPlanContributor,
    [RBACRoleIds.azureKubernetesServiceRbacReader]: RBACRoleNames.azureKubernetesServiceRbacReader,
    [RBACRoleIds.azureKubernetesServiceClusterUser]: RBACRoleNames.azureKubernetesServiceClusterUser,
    [RBACRoleIds.azureKubernetesServiceClusterAdmin]: RBACRoleNames.azureKubernetesServiceClusterAdmin,
    [RBACRoleIds.azureKubernetesServiceRbacClusterAdmin]: RBACRoleNames.azureKubernetesServiceRbacClusterAdmin,
    [RBACRoleIds.containerAppsOperator]: RBACRoleNames.containerAppsOperator,
    [RBACRoleIds.azureMonitorMonitoringContributor]: RBACRoleNames.azureMonitorMonitoringContributor,
    [RBACRoleIds.applicationInsightsComponentContributor]: RBACRoleNames.applicationInsightsComponentContributor,
    [RBACRoleIds.logAnalyticsContributor]: RBACRoleNames.logAnalyticsContributor,
    [RBACRoleIds.storageBlobDataContributor]: RBACRoleNames.storageBlobDataContributor,
    [RBACRoleIds.documentDbAccountContributor]: RBACRoleNames.documentDbAccountContributor,
    [RBACRoleIds.redisCacheContributor]: RBACRoleNames.redisCacheContributor,
    [RBACRoleIds.sqlDbContributor]: RBACRoleNames.sqlDbContributor,
    [RBACRoleIds.monitoringReader]: RBACRoleNames.monitoringReader,
    [RBACRoleIds.storageBlobDataReader]: RBACRoleNames.storageBlobDataReader,
    [RBACRoleIds.virtualMachineContributor]: RBACRoleNames.virtualMachineContributor,
    [RBACRoleIds.azureDatabaseForPostgreSqlContributor]: RBACRoleNames.azureDatabaseForPostgreSqlContributor,
    [RBACRoleIds.sqlServerContributor]: RBACRoleNames.sqlServerContributor,
    [RBACRoleIds.storageAccountContributor]: RBACRoleNames.storageAccountContributor,
    [RBACRoleIds.postgreSqlFlexibleServerLongTermRetentionBackupRole]: RBACRoleNames.postgreSqlFlexibleServerLongTermRetentionBackupRole,
    [RBACRoleIds.sqlManagedInstanceContributor]: RBACRoleNames.sqlManagedInstanceContributor,
    [RBACRoleIds.dataFactoryContributor]: RBACRoleNames.dataFactoryContributor,
    [RBACRoleIds.hdInsightOnAksClusterAdmin]: RBACRoleNames.hdInsightOnAksClusterAdmin,
    [RBACRoleIds.hdInsightOnAksClusterPoolAdmin]: RBACRoleNames.hdInsightOnAksClusterPoolAdmin,
    [RBACRoleIds.azureMlComputeOperator]: RBACRoleNames.azureMlComputeOperator,
    [RBACRoleIds.azureMlDataScientist]: RBACRoleNames.azureMlDataScientist,
    [RBACRoleIds.cognitiveServicesContributor]: RBACRoleNames.cognitiveServicesContributor,
    [RBACRoleIds.cognitiveServicesOpenAiContributor]: RBACRoleNames.cognitiveServicesOpenAiContributor,
    [RBACRoleIds.cognitiveServicesCustomVisionContributor]: RBACRoleNames.cognitiveServicesCustomVisionContributor,
    [RBACRoleIds.cognitiveServicesLanguageWriter]: RBACRoleNames.cognitiveServicesLanguageWriter,
    [RBACRoleIds.cognitiveServicesLuisWriter]: RBACRoleNames.cognitiveServicesLuisWriter,
    [RBACRoleIds.cognitiveServicesQnaMakerEditor]: RBACRoleNames.cognitiveServicesQnaMakerEditor,
    [RBACRoleIds.cognitiveServicesSpeechContributor]: RBACRoleNames.cognitiveServicesSpeechContributor,
    [RBACRoleIds.healthcareAgentEditor]: RBACRoleNames.healthcareAgentEditor,
    [RBACRoleIds.searchServiceContributor]: RBACRoleNames.searchServiceContributor,
    [RBACRoleIds.azureDigitalTwinsDataOwner]: RBACRoleNames.azureDigitalTwinsDataOwner,
    [RBACRoleIds.deviceProvisioningServiceDataContributor]: RBACRoleNames.deviceProvisioningServiceDataContributor,
    [RBACRoleIds.deviceUpdateAdministrator]: RBACRoleNames.deviceUpdateAdministrator,
    [RBACRoleIds.iotHubDataContributor]: RBACRoleNames.iotHubDataContributor,
    [RBACRoleIds.iotHubRegistryContributor]: RBACRoleNames.iotHubRegistryContributor,
    [RBACRoleIds.iotHubTwinContributor]: RBACRoleNames.iotHubTwinContributor,
    [RBACRoleIds.apiManagementServiceContributor]: RBACRoleNames.apiManagementServiceContributor,
    [RBACRoleIds.apiManagementServiceOperatorRole]: RBACRoleNames.apiManagementServiceOperatorRole,
    [RBACRoleIds.apiManagementWorkspaceContributor]: RBACRoleNames.apiManagementWorkspaceContributor,
    [RBACRoleIds.appConfigurationContributor]: RBACRoleNames.appConfigurationContributor,
    [RBACRoleIds.azureServiceBusDataOwner]: RBACRoleNames.azureServiceBusDataOwner,
    [RBACRoleIds.logicAppContributor]: RBACRoleNames.logicAppContributor,
    [RBACRoleIds.workbookContributor]: RBACRoleNames.workbookContributor,
    [RBACRoleIds.azureCenterForSapSolutionsAdministrator]: RBACRoleNames.azureCenterForSapSolutionsAdministrator,
    [RBACRoleIds.costManagementContributor]: RBACRoleNames.costManagementContributor,
    [RBACRoleIds.hdInsightClusterOperator]: RBACRoleNames.hdInsightClusterOperator,
    [RBACRoleIds.cognitiveServicesCustomVisionReader]: RBACRoleNames.cognitiveServicesCustomVisionReader,
    [RBACRoleIds.cognitiveServicesDataReader]: RBACRoleNames.cognitiveServicesDataReader,
    [RBACRoleIds.cognitiveServicesLanguageReader]: RBACRoleNames.cognitiveServicesLanguageReader,
    [RBACRoleIds.cognitiveServicesLuisReader]: RBACRoleNames.cognitiveServicesLuisReader,
    [RBACRoleIds.cognitiveServicesQnaMakerReader]: RBACRoleNames.cognitiveServicesQnaMakerReader,
    [RBACRoleIds.cognitiveServicesUsagesReader]: RBACRoleNames.cognitiveServicesUsagesReader,
    [RBACRoleIds.searchIndexDataReader]: RBACRoleNames.searchIndexDataReader,
    [RBACRoleIds.azureDigitalTwinsDataReader]: RBACRoleNames.azureDigitalTwinsDataReader,
    [RBACRoleIds.deviceProvisioningServiceDataReader]: RBACRoleNames.deviceProvisioningServiceDataReader,
    [RBACRoleIds.deviceUpdateReader]: RBACRoleNames.deviceUpdateReader,
    [RBACRoleIds.iotHubDataReader]: RBACRoleNames.iotHubDataReader,
    [RBACRoleIds.apiManagementServiceReader]: RBACRoleNames.apiManagementServiceReader,
    [RBACRoleIds.apiManagementWorkspaceReader]: RBACRoleNames.apiManagementWorkspaceReader,
    [RBACRoleIds.appConfigurationReader]: RBACRoleNames.appConfigurationReader,
    [RBACRoleIds.logicAppOperator]: RBACRoleNames.logicAppOperator,
    [RBACRoleIds.workbookReader]: RBACRoleNames.workbookReader,
    [RBACRoleIds.azureCenterForSapSolutionsReader]: RBACRoleNames.azureCenterForSapSolutionsReader,
    [RBACRoleIds.costManagementReader]: RBACRoleNames.costManagementReader,
};

export const CoreRBACRoleIds: string[] = [RBACRoleIds.reader, RBACRoleIds.monitoringReader, RBACRoleIds.logAnalyticsReader];

export const CoreRBACRoleNames: string[] = [RBACRoleNames.reader, RBACRoleNames.monitoringReader, RBACRoleNames.logAnalyticsReader];

export function getRoleIdsForResourceGroup(resourceTypes: string[], agentAccessLevel: AgentAccessLevel): string[] {
    const roleIds: string[] = [];
    roleIds.push(...CoreRBACRoleIds);

    const permissionMap = equals(AgentAccessLevel.high, agentAccessLevel, AntUxStringComparison.IgnoreCase)
        ? ResourceTypeToContributorRBACRoleNameMap
        : ResourceTypeToReaderRBACRoleNameMap;

    // For each resource type, get the corresponding RBAC Role name array (can be multiple associated with a type) and convert to IDs
    resourceTypes.forEach(resourceType => {
        const permissionNames = permissionMap[resourceType];
        if (permissionNames) {
            permissionNames.forEach(permissionName => {
                const permissionId = RBACRoleNameToIdMap[permissionName as RBACRoleNames];
                if (permissionId) {
                    roleIds.push(permissionId);
                }
            });
        }
    });

    return roleIds;
}

export function getRoleNamesForResourceGroup(resourceTypes: string[], agentAccessLevel: AgentAccessLevel): string[] {
    const roleIds: string[] = [];
    roleIds.push(...CoreRBACRoleNames);

    const permissionMap = equals(AgentAccessLevel.high, agentAccessLevel, AntUxStringComparison.IgnoreCase)
        ? ResourceTypeToContributorRBACRoleNameMap
        : ResourceTypeToReaderRBACRoleNameMap;

    // For each resource type, get the corresponding RBAC Role names
    resourceTypes.forEach(resourceType => {
        const permissionNames = permissionMap[resourceType];
        if (permissionNames) {
            roleIds.push(...permissionNames);
        }
    });

    return roleIds;
}
