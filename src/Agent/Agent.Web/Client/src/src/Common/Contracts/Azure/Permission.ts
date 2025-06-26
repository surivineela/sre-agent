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

export enum PermissionIds {
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
}

export enum PermissionNames {
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
}

export const PermissionIdToNameMap: Record<string, PermissionNames> = {
    'b24988ac-6180-42a0-ab88-20f7382dd24c': PermissionNames.contributor,
    'acdd72a7-3385-48ef-bd42-f606fba81ae7': PermissionNames.reader,
    '358470bc-b998-42bd-ab17-a7e34c199c0f': PermissionNames.containerAppsContributor,
    '73c42c96-874c-492b-b04d-ab87d138a893': PermissionNames.logAnalyticsReader,
    'de139f84-1756-47ae-9be6-808fbbe84772': PermissionNames.websitesContributor,
    '2cc479cb-7b4d-49a8-b449-8c00fd0f0a4b': PermissionNames.webPlanContributor,
    '7f6c6a51-bcf8-42ba-9220-52d62157d7db': PermissionNames.azureKubernetesServiceRbacReader,
    '4abbcc35-e782-43d8-92c5-2d3f1bd2253f': PermissionNames.azureKubernetesServiceClusterUser,
    '0ab0b1a8-8aac-4efd-b8c2-3ee1fb270be8': PermissionNames.azureKubernetesServiceClusterAdmin,
    'b1ff04bb-8a4e-4dc4-8eb5-8693973ce19b': PermissionNames.azureKubernetesServiceRbacClusterAdmin,
    'f3bd1b5c-91fa-40e7-afe7-0c11d331232c': PermissionNames.containerAppsOperator,
    '749f88d5-cbae-40b8-bcfc-e573ddc772fa': PermissionNames.azureMonitorMonitoringContributor,
    'ae349356-3a1b-4a5e-921d-050484c6347e': PermissionNames.applicationInsightsComponentContributor,
    '92aaf0da-9dab-42b6-94a3-d43ce8d16293': PermissionNames.logAnalyticsContributor,
    'ba92f5b4-2d11-453d-a403-e96b0029c9fe': PermissionNames.storageBlobDataContributor,
    '5bd9cd88-fe45-4216-938b-f97437e15450': PermissionNames.documentDbAccountContributor,
    'e0f68234-74aa-48ed-b826-c38b57376e17': PermissionNames.redisCacheContributor,
    '9b7fa17d-e63e-47b0-bb0a-15c516ac86ec': PermissionNames.sqlDbContributor,
};

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
