const ICON_BASE = ''; // eg: assets
const ICON_LOOKUP: Record<string, string> = {
    // Compute / containers
    containerapp: 'ContainerApp.svg',
    containerappjob: 'ContainerAppJob.svg',
    managedenvironment: 'ManagedEnvironment.svg',
    source: 'git-svgrepo-com.svg',

    // Kubernetes / orchestrators
    aks: 'AKS.svg',
    managedcluster: 'AKS.svg',
    kubernetes: 'AKS.svg',
    scaleset: 'ScaleSet.svg',

    // Web & Functions
    webapp: 'WebApp.svg',
    functionapp: 'FunctionApp.svg',
    logicapp: 'LogicApp.svg',
    site: 'WebApp.svg',
    serverfarms: 'AppServicePlan.svg',

    // Databases & caches
    cosmos: 'CosmosDB.svg',
    cosmosdb: 'CosmosDB.svg',
    postgresql: 'POSTGRESQLServer.svg',
    postgres: 'POSTGRESQLServer.svg',
    sql: 'SQLServer.svg',
    sqlserver: 'SQLServer.svg',
    redis: 'AzureRedisCache.svg',
    cache: 'AzureRedisCache.svg',

    // Networking
    vnet: 'Vnet.svg',
    virtualnetwork: 'Vnet.svg',
    subnet: 'Vnet.svg',
    nsg: 'NSG.svg',
    networksecuritygroup: 'NSG.svg',
};

// Friendly names for resource types
const FRIENDLY_NAMES: Record<string, string> = {
    // Compute / containers
    containerapp: 'Container App',
    containerappjob: 'Container App Job',
    managedenvironment: 'Managed Environment',

    // Kubernetes / orchestrators
    aks: 'Kubernetes Service',
    managedcluster: 'Kubernetes Service',
    kubernetes: 'Kubernetes Service',
    scaleset: 'Scale Set',

    // Web & Functions
    webapp: 'Web App',
    functionapp: 'Function App',
    logicapp: 'Logic App',
    site: 'Web App',
    serverfarms: 'App Service Plan',

    // Databases & caches
    cosmos: 'Cosmos DB',
    cosmosdb: 'Cosmos DB',
    postgresql: 'PostgreSQL Server',
    postgres: 'PostgreSQL Server',
    sql: 'SQL Server',
    sqlserver: 'SQL Server',
    redis: 'Redis Cache',
    cache: 'Redis Cache',

    // Networking
    vnet: 'Virtual Network',
    virtualnetwork: 'Virtual Network',
    subnet: 'Subnet',
    nsg: 'Network Security Group',
    networksecuritygroup: 'Network Security Group',
};

const DEFAULT_ICON = 'azureResource.svg';

/** Can be passed to `img.src` */
export const resolveResourceIcon = (azureType?: string): string => {
    if (!azureType) return ICON_BASE + DEFAULT_ICON;
    const t = azureType.toLowerCase();
    const match = Object.keys(ICON_LOOKUP).find(k => t.includes(k));
    return ICON_BASE + (match ? ICON_LOOKUP[match] : DEFAULT_ICON);
};

export const getResourceTypeFriendlyName = (azureType?: string, subType?: string): string => {
    if (!azureType) return 'Subscription';

    if (subType == 'k8s/apps/v1/deployments') {
        return 'Kubernetes Deployment';
    }
    if (subType == 'k8s/apps/v1/statefulsets') {
        return 'Kubernetes StatefulSet';
    }

    const t = azureType.toLowerCase();
    const match = Object.keys(FRIENDLY_NAMES).find(k => t.includes(k));

    if (match) {
        return FRIENDLY_NAMES[match];
    } else {
        // Extract the type from resourceType path as fallback
        const typeArray = azureType.split('/');
        return typeArray[typeArray.length - 1];
    }
};

const paasResourceTypeMatchers = [
    'microsoft.app',
    'microsoft.web',
    'microsoft.containerservice/managedclusters',
    'k8s/apps/v1/deployments',
    'k8s/apps/v1/statefulsets',
];
export const isPaasResourceType = (rscType?: string): boolean => {
    if (!rscType) return false;

    const lowerRscType = rscType.toLowerCase();
    return paasResourceTypeMatchers.some(matcher => lowerRscType.includes(matcher));
};
