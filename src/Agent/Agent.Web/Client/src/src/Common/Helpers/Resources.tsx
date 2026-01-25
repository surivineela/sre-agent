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

    // Logic Apps
    'web/sites/connections': 'Connection.svg',
    'microsoft.web/sites/apimanagementconnections': 'Connection.svg',
    'microsoft.web/sites/functionconnections': 'Connection.svg',

    // Web & Functions
    webapp: 'WebApp.svg',
    functionapp: 'FunctionApp.svg',
    function: 'Function.svg',
    logicapp: 'LogicApp.svg',
    workflow: 'Workflow.svg',
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
    storageaccounts: 'StorageAccount.svg',

    // Networking
    vnet: 'Vnet.svg',
    virtualnetwork: 'Vnet.svg',
    subnet: 'Vnet.svg',
    nsg: 'NSG.svg',
    networksecuritygroup: 'NSG.svg',

    // API Management
    apimanagement: 'ApiManagement.svg',
    apicenter: 'ApiCenter.svg',

    // Monitoring
    components: 'ApplicationInsights.svg',

    //Knowledge Base
    knowledgebase: 'KnowledgeBase.svg',
    folder: 'folder.svg',
    dataconnectors: 'DataConnectors.svg',
    connectors: 'Connectors.svg',
    azuredataexplorer: 'AzureDataExplorer.svg',
    azuredevops: 'AzureDevOps.svg',
    github: 'github-mark.svg',
    url: 'Url.svg',

    //Usage
    usagewarning: 'WarningSpotIllustration.svg',

    // Office
    outlook: 'Outlook.svg',
    teams: 'Teams.svg',

    //mcp
    customconnector: 'MCP server.svg',

    // Incident Management
    icm: 'IcM.svg',

    // Others Azure Resource Types
    'microsoft.documentdb/databaseaccounts': 'CosmosDB.svg',
    'microsoft.eventgrid/topics': 'EventGridTopic.svg',
    'microsoft.eventhub/namespaces': 'EventHubNamespace.svg',
    'microsoft.servicebus/namespaces': 'ServiceBusNamespace.svg',
    'microsoft.synapse/workspaces': 'SynapseWorkspace.svg',
    'microsoft.web/connections': 'Connection.svg',
    'microsoft.keyvault/vaults': 'KeyVault.svg',
    'microsoft.search/searchservices': 'AzureAiSearch.svg',
    'microsoft.cognitiveservices/accounts': 'AzureOpenAI.svg',
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

    // Logic Apps
    'web/sites/connections': 'Service Provider Connection',
    'microsoft.web/sites/apimanagementconnections': 'API Management Connection',
    'microsoft.web/sites/functionconnections': 'Function Connection',

    // Web & Functions
    webapp: 'Web App',
    functionapp: 'Function App',
    function: 'Function',
    logicapp: 'Logic App',
    workflow: 'Workflow',
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
    storageaccounts: 'Storage Account',

    // Networking
    vnet: 'Virtual Network',
    virtualnetwork: 'Virtual Network',
    subnet: 'Subnet',
    nsg: 'Network Security Group',
    networksecuritygroup: 'Network Security Group',

    //Mointoring
    components: 'Application Insights',

    // API Management
    apimanagement: 'API Management',
    'microsoft.apimanagement/service/backends': 'API Management Backend',
    apicenter: 'API Center',

    // Others Azure Resource Types
    'microsoft.documentdb/databaseaccounts': 'Document DB',
    'microsoft.eventgrid/topics': 'Event Grid Topic',
    'microsoft.eventhub/namespaces': 'Event Hub Namespace',
    'microsoft.servicebus/namespaces': 'Service Bus Namespace',
    'microsoft.synapse/workspaces': 'Synapse Workspace',
    'microsoft.web/connections': 'API Connection',
    'microsoft.keyvault/vaults': 'Key Vault',
    'microsoft.search/searchservices': 'Azure AI Search',
    'microsoft.cognitiveservices/accounts': 'Azure OpenAI',
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
    const sortedKeys = Object.keys(FRIENDLY_NAMES).sort((a, b) => b.length - a.length);
    const match = sortedKeys.find(k => t.includes(k));

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
