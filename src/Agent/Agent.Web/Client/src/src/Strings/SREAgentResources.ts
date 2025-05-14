import { defineMessages } from 'react-intl';

export const SreAgentResources = defineMessages({
    deleteAgentTitle: { defaultMessage: 'Delete SRE Agent', id: 'ThSX0k' },
    deleteAgentDescription: {
        defaultMessage: 'Are you sure you want to delete this SRE Agent? This action cannot be undone.',
        id: 'O8zbbB',
    },
    deleteAgentNotificationTitle: { defaultMessage: 'Deleting SRE Agent', id: '6OBtew' },
    deleteAgentNotificationDescription: { defaultMessage: 'Deleting SRE Agent {name} in progress', id: 'L0KVkI' },
    deleteAgentNotificationSuccess: { defaultMessage: 'SRE Agent {name} deleted successfully', id: '1O47t6' },
    deleteAgentNotificationError: { defaultMessage: 'Failed to delete SRE Agent {name}', id: 'JCjxSH' },
    sreAgent: { defaultMessage: 'SRE Agent', id: '+WRusC' },
    add: { defaultMessage: 'Add', id: '2/2yg+' },
    new: { defaultMessage: 'New', id: 'bW7B87' },
    feedbackDialogTitle: {
        id: 'Nrc9ba',
        defaultMessage: 'Thank you for your feedback!',
    },
    approve: {
        id: 'WCaf5C',
        defaultMessage: 'Approve',
    },
    deny: {
        id: 'htvX+Z',
        defaultMessage: 'Deny',
    },
    approveUsingCreds: {
        id: 'A3Wps1',
        defaultMessage: 'If you approve, this operation will be executed on your behalf using your credentials.',
    },
    beingExecutedUsingCreds: {
        id: 'SRf69j',
        defaultMessage: "This operation is being executed using the approver's credentials.",
    },
    submit: {
        id: 'wSZR47',
        defaultMessage: 'Submit',
    },
    cancel: {
        id: '47FYwb',
        defaultMessage: 'Cancel',
    },
    approved: {
        id: '6XFO/C',
        defaultMessage: 'Approved',
    },
    denied: {
        id: '5kp1Ie',
        defaultMessage: 'Denied',
    },
    approvedBy: {
        defaultMessage: 'Approved by',
        id: '5+bQr/',
    },
    deniedBy: {
        defaultMessage: 'Denied by',
        id: '4Ez7HI',
    },
    requestedAt: {
        defaultMessage: 'Requested at',
        id: '9ZIBMx',
    },
    decisionTime: {
        defaultMessage: 'Decision Time',
        id: 'Q/agzh',
    },
    actions: { defaultMessage: 'Actions', id: 'wL7VAE' },
    actionsCompleted: { defaultMessage: '{numOfActions} actions completed', id: 'RI4Umu' },
    active: { defaultMessage: 'Active', id: '3a5wL8' },
    acknowledged: { defaultMessage: 'Acknowledged', id: 'FnKIAW' },
    triggered: { defaultMessage: 'Triggered', id: 'Zqa4dQ' },
    closed: { defaultMessage: 'Closed', id: 'Fv1ZSz' },
    mitigated: { defaultMessage: 'Mitigated', id: 'dnXgff' },
    resolved: { defaultMessage: 'Resolved', id: 'W6nSYE' },
    activeThreads: { defaultMessage: 'Active threads', id: 'rFlkvY' },
    allThreads: { defaultMessage: 'All threads', id: 'SDXmEJ' },
    agent: { defaultMessage: 'Agent', id: 'QGVI63' },
    agentDetails: { defaultMessage: 'Agent details', id: 'Wf6bDe' },
    agents: { defaultMessage: 'Agents', id: 'GBnvl1' },
    apply: { defaultMessage: 'Apply', id: 'EWw/tK' },
    buildingKnowledgeGraph: { defaultMessage: 'Building knowledge graph', id: '8Wiwkg' },
    client: { defaultMessage: 'Client', id: 'JX0502' },
    createAgent: { defaultMessage: 'Create agent', id: 'UrGI9K' },
    connectedToTeams: { defaultMessage: 'Connected to Teams', id: '7cS956' },
    create: { defaultMessage: 'Create', id: 'VzzYJk' },
    delete: { defaultMessage: 'Delete', id: 'K3r6DQ' },
    discard: { defaultMessage: 'Discard', id: 'nmpevl' },
    disconnect: { defaultMessage: 'Disconnect', id: 'qj1uhz' },
    endpoint: { defaultMessage: 'Endpoint', id: 'ljmS5P' },
    enterName: { defaultMessage: 'Enter name', id: 'OJ2u8k' },
    failed: { defaultMessage: 'Failed', id: 'vXCeIi' },
    filterMessage: { defaultMessage: 'Showing the first 1000 results. Filter to narrow down the list.', id: 'u64mw7' },
    fieldRequired: { defaultMessage: 'This field is required', id: 'TKmub+' },
    getMoreInfo: { defaultMessage: 'Get more info', id: 'TB6bkn' },
    azureManagedGrafana: { defaultMessage: 'Azure Managed Grafana', id: 'IF3r+X' },
    incidents: { defaultMessage: 'Incidents', id: 'mtr3R4' },
    location: { defaultMessage: 'Location', id: 'rvirM2' },
    managedResources: { defaultMessage: 'Managed resources', id: 'pCPZnU' },
    managedResourceGroups: { defaultMessage: 'Managed resource groups', id: 'yilQrD' },
    name: { defaultMessage: 'Name', id: 'HAlOn1' },
    newThread: { defaultMessage: 'New thread', id: 'ITYqY7' },
    overview: { defaultMessage: 'Overview', id: '9uOFF3' },
    pendingApprovals: { defaultMessage: 'Pending approvals', id: 'HbTFJf' },
    projectDetails: { defaultMessage: 'Project details', id: '7gMEKc' },
    projectDetailsDescription: {
        defaultMessage:
            'Select the subscription to manage deployed resources and costs. Use resources groups like folders to organize and manage all your resources.',
        id: '6dWeUs',
    },
    refresh: { defaultMessage: 'Refresh', id: 'rELDbB' },
    region: { defaultMessage: 'Region', id: 'lnaWo/' },
    regionPlaceHolder: { defaultMessage: 'Select region', id: 'tshYzs' },
    resourceGroup: { defaultMessage: 'Resource group', id: '+uAdUZ' },
    resourceGroupName: { defaultMessage: 'Resource group name', id: 'xVPoso' },
    save: { defaultMessage: 'Save', id: 'jvo0vs' },
    scope: { defaultMessage: 'Scope', id: 'nso3Mj' },
    selectResourceGroups: { defaultMessage: 'Select resource groups', id: 'ftfFhS' },
    selectResourceGroupsToMonitor: { defaultMessage: 'Select resource groups to monitor', id: 'CfGC/2' },
    search: { defaultMessage: 'Search', id: 'xmcVZ0' },
    summary: { defaultMessage: 'Summary', id: 'RrCui3' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    success: { defaultMessage: 'Success', id: 'xrKHS6' },
    startTime: { defaultMessage: 'Start Time', id: '5QYdPU' },
    stop: { defaultMessage: 'Stop', id: 'q/uwLT' },
    sreAgentSpace: { defaultMessage: 'SRE Agent Space', id: 'iv1ryQ' },
    subscription: { defaultMessage: 'Subscription', id: 'R/6nsx' },
    appGroup: { defaultMessage: 'App Group', id: 'V6juiN' },
    subscriptionId: { defaultMessage: 'Subscription ID', id: 'FUQvS0' },
    totalThreads: { defaultMessage: 'Total threads', id: 'zN87hN' },
    tasks: { defaultMessage: 'Tasks', id: 'yhU1et' },
    task: { defaultMessage: 'Task', id: '0wJ7N+' },
    threadCount: { defaultMessage: 'Thread count', id: 'yYK6LR' },
    logicAppName: { defaultMessage: 'Logic App name', id: 'f0Y4Zr' },
    yes: { defaultMessage: 'Yes', id: 'a5msuh' },
    no: { defaultMessage: 'No', id: 'oUWADl' },
    managedIdentity: { defaultMessage: 'Managed identity', id: 'Ys9AIu' },
    chatAiContentAndPrivacyMessageStatement: {
        defaultMessage:
            'AI-generated content might be incorrect, so review carefully before use. Do not include personal or confidential information in the chat.',
        id: 'BKMqtr',
    },
    tipsOnHowToChat: { defaultMessage: 'Tips on how to chat with the SRE Agent', id: 'UVS724' },
});

export const SreAgentTabResources = defineMessages({
    activities: { defaultMessage: 'Activities', id: 'UmEsZF' },
    settings: { defaultMessage: 'Settings', id: 'D3idYv' },
    resourceMapping: { defaultMessage: 'Resource mapping', id: 'TdeXH0' },
    logs: { defaultMessage: 'Logs', id: 'SNuQo7' },
});

export const ResourcePickerTabResources = defineMessages({
    selectTabTitle: { defaultMessage: 'Choose resource groups', id: 'zcyAwy' },
    reviewTabTitle: { defaultMessage: 'Review resource groups', id: 'LPprqA' },
    assignTabTitle: { defaultMessage: 'Assign roles and permissions', id: '5dWZzT' },
    assignTabDescription: {
        defaultMessage:
            'The resources you picked have permissions for these roles. If you add new resources later, the SRE Agent might request any additional permissions if needed.',
        id: 'w3tjx3',
    },
    reader: { defaultMessage: 'Reader', id: '3nhWFW' },
    containerAppsOperator: { defaultMessage: 'Container Apps Operator', id: '/WrP/v' },
    monitoringReader: { defaultMessage: 'Monitoring Reader', id: 'Sr4IbA' },
    logAnalyticsReader: { defaultMessage: 'Log Analytics Reader', id: 'sI+CCC' },
    kubernetesReader: { defaultMessage: 'Azure Kubernetes Service RBAC Reader', id: 'RrsyUh' },
    websiteContributor: { defaultMessage: 'Website Contributor/Operator', id: 'UV4Dx5' },
    writerOperator: { defaultMessage: 'Writer/Operator', id: 'oUbzA/' },
    permissionsForRoleAssignment: { defaultMessage: 'Permissions to assign roles', id: 'ob9EPi' },
    resourceGroupPermissionError: {
        defaultMessage:
            'Some of the selected resource groups do not have the required roleAssignments/write and Microsoft.ManagedIdentity/userAssignedIdentities/write permissions.',
        id: 'E60v6W',
    },
    resourceGroupMaxError: { defaultMessage: 'You can choose a maximum of 20 resource groups that the agent will manage.', id: '6tlKgy' },
});

export const ActionsResources = defineMessages({
    actions: { defaultMessage: 'Actions', id: 'wL7VAE' },
    allStatuses: { defaultMessage: 'All statuses', id: 'fvK8Qi' },
    inProgress: { defaultMessage: 'In progress', id: 'q1WWIr' },
    time: { defaultMessage: 'Time', id: 'ug01Mk' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    completed: { defaultMessage: 'Completed', id: '95stPq' },
    failed: { defaultMessage: 'Failed', id: 'vXCeIi' },
    pending: { defaultMessage: 'Pending', id: 'eKEL/g' },
});

export const AccessControlResources = defineMessages({
    accessControl: { defaultMessage: 'Access control', id: 'rpG/Bn' },
    accessControlDescription: {
        defaultMessage: 'Manage access to the SRE Agent resource by clicking on the link below.',
        id: 'ol51Ez',
    },
    openAccessControl: { defaultMessage: 'Open Access control', id: 'Ez5ZJg' },
});

export const ActivitiesResources = defineMessages({
    createThreadButtonText: { defaultMessage: 'New chat thread', id: 'TkWiD5' },
    chatPivotHeader: { defaultMessage: 'Chat', id: 'WTrOy3' },
    actionsPivotHeader: { defaultMessage: 'Actions', id: 'wL7VAE' },
    chatInputPlaceholder: { defaultMessage: 'I want to...', id: 'PxLzzW' },
    knowledgeGraphBuildStatus: {
        defaultMessage:
            'Gathering info about your resources, which might take a few minutes. You can still chat about other topics while the data loads.',
        id: 'ciMXhP',
    },
    newMessagesButtonText: {
        defaultMessage: 'New messages',
        id: 'O79Wpv',
    },
    showThreadActionsButtonText: {
        defaultMessage: 'Show thread actions',
        id: 'PipPB3',
    },
    hideThreadActionsButtonText: {
        defaultMessage: 'Hide thread actions',
        id: 'MACADd',
    },
    showThreadMenuButtonText: {
        defaultMessage: 'Show thread menu',
        id: 'RSDnlS',
    },
    hideThreadMenuButtonText: {
        defaultMessage: 'Hide thread menu',
        id: 'OWBgF9',
    },
});

export const ThreadContextStateResources = defineMessages({
    initializing: { defaultMessage: 'Initializing...', id: 'xQRfI5' },
    waiting: { defaultMessage: 'Waiting...', id: '35vd1u' },
    determiningNextSteps: { defaultMessage: 'Determining next steps...', id: 'vjy7Cr' },
    generatingAResponse: { defaultMessage: 'Generating a response...', id: 'wInZgf' },
    responseCompleted: { defaultMessage: 'Response completed', id: 'u0RCrx' },
    somethingWentWrong: { defaultMessage: 'Something went wrong', id: 'JqiqNj' },
});

export const ActivitiesThreadHeaderResources = defineMessages({
    deleteThreadTitle: { defaultMessage: 'Delete thread', id: '2WxP2i' },
    deleteThreadInProgressDescription: { defaultMessage: 'Deleting thread with title {title}', id: 'axE+Jt' },
    deleteThreadSuccessDescription: { defaultMessage: 'Thread with title {title} deleted successfully', id: 'K5yM40' },
    deleteThreadFailureDescription: {
        defaultMessage: 'Failed to delete thread with title {title} with error: {errorMessage}',
        id: 'bi05ZL',
    },
    deleteThreadDialogTitle: { defaultMessage: 'Delete thread?', id: '+5BJJL' },
    deleteThreadDialogDescription: {
        defaultMessage: 'This will permanently delete the chat and all actions in this thread.',
        id: '1uDzPU',
    },
});

export const ActionsHeaderResources = defineMessages({
    action: { defaultMessage: 'Action', id: 'QlsDcr' },
    time: { defaultMessage: 'Time', id: 'ug01Mk' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
});

export const ActionsStatusResources = defineMessages({
    failed: { defaultMessage: 'Failed', id: 'vXCeIi' },
    completed: { defaultMessage: 'Completed', id: '95stPq' },
    inProgress: { defaultMessage: 'In progress', id: 'q1WWIr' },
    pending: { defaultMessage: 'Pending', id: 'eKEL/g' },
});

export const IncidentManagementResources = defineMessages({
    incidentManagement: { defaultMessage: 'Incident management', id: 'T7WpWs' },
    incidentManagementDescription: {
        defaultMessage:
            'Integrate an incident management platform with the SRE Agent to automate routine incident tasks, manage alerts, and get data-driven insights.',
        id: 'HafgWq',
    },
    incidentPlatform: { defaultMessage: 'Incident platform', id: 'EZBG/A' },
});

export const IncidentManagementPlatformResources = defineMessages({
    disconnected: { defaultMessage: 'Disconnected', id: 'FZeQlc' },
    pagerDuty: { defaultMessage: 'PagerDuty', id: '6UyZlH' },
    azMonitor: { defaultMessage: 'Azure Monitor', id: '7Nz2Ev' },
});

export const IncidentManagementNotificationResources = defineMessages({
    saveTitle: { defaultMessage: 'Save incident management configuration', id: '35UE1s' },
    saveStarted: { defaultMessage: 'Saving incident management configuration', id: 'TyvDrC' },
    saveSucceeded: { defaultMessage: 'Successfully saved incident management configuration', id: 'NrGEOo' },
    saveFailed: { defaultMessage: 'Failed to save incident management configuration. Error: {errorMessage}', id: 'slxYbm' },
});

export const IncidentManagementSaveErrorResources = defineMessages({
    managedConnectionFailure: { defaultMessage: 'Failed to create managed connection', id: '6ENgmY' },
    logicAppCreateFailure: { defaultMessage: 'Failed to create logic app', id: 'ZUXvHE' },
    logicAppDeleteFailure: { defaultMessage: 'Failed to delete logic app', id: 'DpMYPV' },
    configFailure: { defaultMessage: 'Failed to save incident management configuration', id: 'hX7X4n' },
});

export const IncidentAlertResources = defineMessages({
    headerTitle: { defaultMessage: 'New Azure Monitor Alert Detected', id: '6u8zJX' },
    alertID: { defaultMessage: 'Alert ID', id: 'k8ZNgH' },
    firedAt: { defaultMessage: 'Fired At', id: 'ielB5f' },
    monitorService: { defaultMessage: 'Monitor Service', id: 'snEUKZ' },
    alertRule: { defaultMessage: 'Alert Rule', id: 'NV14yw' },
    description: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    monitoredResource: { defaultMessage: 'Monitored Resource', id: 'X192re' },
    resourceGroup: { defaultMessage: 'Resource Group', id: 'ZbUTXC' },
    portalUrlLinkText: { defaultMessage: 'View Alert Details', id: '0sIUhe' },
    subscription: { defaultMessage: 'Subscription', id: 'R/6nsx' },
});

export const ManagedResourcesStringResources = defineMessages({
    allRegions: { defaultMessage: 'All Regions', id: 'w+XRP2' },
    managedResources: { defaultMessage: 'Managed resources', id: 'pCPZnU' },
    addResourceGroup: { defaultMessage: 'Add resource group', id: 'HWMrXF' },
    add: { defaultMessage: 'Add', id: '2/2yg+' },
    deleteTitle: { defaultMessage: 'Remove managed resource group', id: 'JjKcCj' },
    confirmDelete: {
        defaultMessage:
            "This will permanently remove a resource group from the agent's managed resource groups. Are you sure you want to remove?",
        id: '3BubSI',
    },
    deleteNotificationTitle: { defaultMessage: 'Deleting selected managed resource groups', id: '3Rh7XU' },
    deleteNotificationDescription: { defaultMessage: 'Deleting selected managed resource groups in progress', id: '3PgCIp' },
    deleteNotificationSuccess: { defaultMessage: 'The selected managed resource groups were deleted successfully', id: '1PVhxx' },
    deleteNotificationError: { defaultMessage: 'Failed to delete the selected managed resource groups', id: 'hK4HcH' },
    addNotificationTitle: { defaultMessage: 'Adding {number} managed resource group', id: 'Bcgdb7' },
    addNotificationPluralTitle: { defaultMessage: 'Adding {number} managed resource groups', id: '0UwFzs' },
    addNotificationPluralDescription: { defaultMessage: 'The selected resource groups are being set up', id: '2OQ6yB' },
    addNotificationDescription: { defaultMessage: 'The selected resource group is being set up', id: 'tSKi9q' },
    addNotificationSuccess: { defaultMessage: 'Managed resource group added successfully', id: '5yG7b2' },
    addNotificationPluralSuccess: { defaultMessage: 'Managed resource groups added successfully', id: 'FGAk9M' },
    addNotificationAgentError: { defaultMessage: 'Failed to add {number} managed resource groups to your SRE Agent', id: 'ZukeYV' },
    addNotificationError: { defaultMessage: 'Failed to add {number} managed resource groups with errors: {error}', id: 'sKQWgY' },
    resourceGroupsLoadFailure: { defaultMessage: 'Failed to load resource groups.', id: 'anSi7M' },
    selectAll: { defaultMessage: 'Select all', id: '94Fg25' },
    selectResourceGroupsToMonitor: { defaultMessage: 'Select resource groups to monitor', id: 'CfGC/2' },
    cancel: { defaultMessage: 'Cancel', id: '47FYwb' },
    save: { defaultMessage: 'Save', id: 'jvo0vs' },
    next: { defaultMessage: 'Next', id: '9+Ddtu' },
    back: { defaultMessage: 'Back', id: 'cyR7Kh' },
    search: { defaultMessage: 'Search', id: 'xmcVZ0' },
    resourceGroupName: { defaultMessage: 'Resource group name', id: 'xVPoso' },
    location: { defaultMessage: 'Location', id: 'rvirM2' },
    allSubscriptions: { defaultMessage: 'All subscriptions', id: '8yyU6n' },
    subscription: { defaultMessage: 'Subscription', id: 'R/6nsx' },
    searchForResourceGroups: { defaultMessage: 'Search for resource groups', id: 'ElI1gd' },
    noResults: { defaultMessage: 'No results', id: 'jHJmjf' },
    filterItems: { defaultMessage: 'Filter items', id: 'F9LrJA' },
    loading: { defaultMessage: 'Loading...', id: 'gjBiyj' },
    subscriptionsLoadFailure: { defaultMessage: 'Failed to load subscriptions.', id: 'EKfWmx' },
    region: { defaultMessage: 'Region', id: 'lnaWo/' },
});

export const MetricsResources = defineMessages({
    active: { defaultMessage: 'Active', id: '3a5wL8' },
    mitigated: { defaultMessage: 'Mitigated', id: 'dnXgff' },
    resolved: { defaultMessage: 'Resolved', id: 'W6nSYE' },
});

export const ComponentResources = defineMessages({
    gridItemsCountAriaLabel: { defaultMessage: '{numOfResults} {results} for {searchString}', id: 'xbKhzp' },
    gridItemsCountAriaLabelNoFilter: { defaultMessage: '{numOfResults} {results}', id: '5pQrWI' },
    loading: { defaultMessage: 'Loading...', id: 'gjBiyj' },
    noResultsFound: { defaultMessage: 'No results found', id: 'hX5PAb' },
    noResultsFoundFor: { defaultMessage: 'No results found for {searchString}', id: 'xUgb9H' },
    result: { defaultMessage: 'result', id: 'bxnWhY' },
    results: { defaultMessage: 'results', id: '8quEg9' },
    search: { defaultMessage: 'Search', id: 'xmcVZ0' },
});

export const IncidentManagementValidationResources = defineMessages({
    apiKeyInvalid: { defaultMessage: 'API key is not valid', id: 'AXAtAX' },
    apiKeyRequired: { defaultMessage: 'API Key is required', id: 'S7xtEr' },
    apiKeyFailedToValidate: { defaultMessage: 'Failed to validate API Key', id: 'hMGoYE' },
});

export const PagerDutyResources = defineMessages({
    pagerDutyApiKey: { defaultMessage: 'REST API access key', id: 'AuFOi8' },
    pagerDutyApiKeyDescription: {
        defaultMessage:
            "You'll need to connect through an access key. Go to pagerduty.com and the information will be within the API Access Keys section.",
        id: 'UvQLwV',
    },
    editKey: { defaultMessage: 'Edit key', id: 'CQF3U+' },
    disconnectConfirmationTitle: { defaultMessage: 'Disconnect from PagerDuty?', id: '8kVGbB' },
    disconnectConfirmationMessage: {
        defaultMessage:
            'This will permanently disconnect from PagerDuty. The agent will no longer be able to manage tickets. Are you sure you want to disconnect?',
        id: 'Sw/LL9',
    },
    connectedMessage: { defaultMessage: 'PagerDuty added', id: 'i0mHNo' },
});

export const AzMonitorResources = defineMessages({
    disconnectConfirmationTitle: { defaultMessage: 'Disconnect from Azure Monitor?', id: 'HsGdXO' },
    disconnectConfirmationMessage: {
        defaultMessage:
            'This will permanently disconnect to Azure Monitor. The agent will no longer be integrated with Azure Monitor alerts. Are you sure you want to disconnect?',
        id: 'Y1qw19',
    },
    connectedMessage: { defaultMessage: 'Azure Monitor added', id: '14xQ4i' },
});

export const SettingsTabResources = defineMessages({
    incidentManagement: { defaultMessage: 'Incident management', id: 'T7WpWs' },
    accessControl: { defaultMessage: 'Access control (IAM)', id: '7w4v59' },
    basics: { defaultMessage: 'Basics', id: 'itC9lG' },
    grafanaInsights: { defaultMessage: 'Grafana insights', id: 'Nf40QB' },
    managedResources: { defaultMessage: 'Managed resource groups', id: 'yilQrD' },
});

export const GrafanaDashboardResources = defineMessages({
    assignedTo: { defaultMessage: 'Assigned to', id: 'ONVN5F' },
    scope: { defaultMessage: 'Scope', id: 'nso3Mj' },
    monitoringMetricsPublisher: { defaultMessage: 'Monitoring metrics publisher', id: 'XvrDTb' },
    grafanaAdmin: { defaultMessage: 'Grafana Admin', id: 'TMZ2Rq' },
    monitoringReaderRole: { defaultMessage: 'Monitoring Reader Role', id: 'AfbqHj' },
    monitoringDataReaderRole: { defaultMessage: 'Monitoring Data Reader Role', id: 'Z1VHv1' },
    dataCollectionRule: { defaultMessage: 'Data Collection Rule', id: '6m5Ba0' },
    azureManagedGrafana: { defaultMessage: 'Azure Managed Grafana', id: 'IF3r+X' },
    subscription: { defaultMessage: 'Subscription', id: 'R/6nsx' },
    azureMonitorWorkspace: { defaultMessage: 'Azure Monitor Workspace', id: 'gj6Qc6' },
    userAssignedManagedIdentity: { defaultMessage: 'User Assigned Managed Identity', id: 'lGIJSX' },
    user: { defaultMessage: 'User', id: 'EwRIOm' },
    role: { defaultMessage: 'Role', id: '1ZgrhW' },
    roleAssignments: {
        defaultMessage: 'Role assignments',
        id: 'A/xTHO',
    },
    insufficientPermissions: {
        defaultMessage:
            'You do not have the required Microsoft.Authorization/roleAssignments/write permission to set up a Grafana dashboard.',
        id: 'vkNGfn',
    },
    tooltipContent: {
        defaultMessage:
            'Once a Grafana dashboard is provisioned and linked, it may take some time for resources to fully populate the dashboard.',
        id: 'piCpTn',
    },
    description: {
        defaultMessage: `Azure Managed Grafana is a fully managed service for analytics and monitoring. To add the service and a Grafana dashboard, you need to create an Azure Managed Grafana resource. The necessary permissions will be automatically assigned so that the dashboard can display monitoring data.`,
        id: 'R1YfDi',
    },
    grafanaCreationTitle: { defaultMessage: 'Provisioning Grafana dashboard', id: 'PzGaVI' },
    grafanaCreationInProgress: { defaultMessage: 'Provisioning of your Grafana dashboard is in progress.', id: 'kWs94j' },
    grafanaCreationSuccess: { defaultMessage: 'Grafana dashboard created successfully', id: 'WY8nWe' },
    grafanaCreationFailed: {
        defaultMessage: 'Failed to create the Grafana dashboard with the error: {errorMessage}',
        id: 'ACfSSZ',
    },
    linkGrafanaDashboardTitle: { defaultMessage: 'Linking Grafana dashboard', id: 'XEso9C' },
    linkGrafanaDashboardInProgress: { defaultMessage: 'Linking Grafana dashboard to SRE Agent in progress', id: '40YXE1' },
    linkGrafanaDashboardSuccess: { defaultMessage: 'Grafana dashboard linked successfully', id: 'FDJdmL' },
    linkGrafanaDashboardFailed: { defaultMessage: 'Failed to link Grafana dashboard', id: 'S1VBUm' },
    grafanaResource: { defaultMessage: 'Grafana resource', id: 'hmrm7i' },
    grafanaDashboardUrl: { defaultMessage: 'Grafana dashboard URL', id: '1dOMiz' },
    createGrafanaDashboard: { defaultMessage: 'Create Grafana dashboard', id: 'eFKqV0' },
    createGrafanaDashboardSameResourceGroup: {
        defaultMessage: 'Create Grafana dashboard in the same resource group',
        id: '6IIC+D',
    },
    generateApiKey: { defaultMessage: 'Generate API key', id: 'JVZyFs' },
    inProgress: { defaultMessage: '- in progress', id: 'GCVHNV' },
    completed: { defaultMessage: '- completed', id: 'I03XmI' },
    firstStepInstructions: { defaultMessage: 'First, a Grafana resource needs to be created.', id: 'ftzwIR' },
    uniqueGrafanaResourceNameError: {
        defaultMessage: 'Grafana resource must be unique within the resource group.',
        id: 'jTQZpk',
    },
    invalidGrafanaResourceNameError: {
        defaultMessage:
            'Grafana resource name must be between 2 to 23 characters long. They must begin with a letter and end with a letter or digit.',
        id: 'ca43CS',
    },
    grafanaResourceName: { defaultMessage: 'Grafana resource name', id: 'a2+0+5' },
});

export const FeedbackResources = defineMessages({
    submitFeedbackTitle: { defaultMessage: 'Submit feedback to Microsoft', id: '+FoBRs' },
    feedbackPlaceholder: { defaultMessage: 'Give as much detail as you can, but do not include any personal information.', id: 'csu0rb' },
    feedbackContactMe: { defaultMessage: "It's OK to contact me about my feedback.", id: 'E396gv' },
    feedbackPrivacyStatement: {
        defaultMessage: 'Data usage, customer rights, and privacy statement if needed. We got it covered.',
        id: 'OV0MpG',
    },
});

export const GraphResources = defineMessages({
    resourceSelectorDescription: {
        defaultMessage:
            "This logical map shows how your application's resources are connected. The agent analyzes those resources and organizes them into app group based on the primary resources.",
        id: '/0qSPz',
    },
});

export const ResourceInfoResources = defineMessages({
    name: { defaultMessage: 'Name', id: 'HAlOn1' },
    type: { defaultMessage: 'Type', id: '+U6ozc' },
    dashboard: { defaultMessage: 'Dashboard', id: 'hzSNj4' },
    dashboardLinkText: { defaultMessage: 'Go to Azure Managed Grafana', id: 'SAINuE' },
    grafanaLogo: { defaultMessage: 'Grafana logo', id: 'mzRg+7' },
    repositoryConnection: { defaultMessage: 'Repository Connection', id: 'aRH5fG' },
    authorizeRepositoryAccess: { defaultMessage: 'Authorize Repository Access', id: 'Az8/Pe' },
    connectRepository: { defaultMessage: 'Connect Repository', id: 'rP/nDW' },
    linkRepositoryToResource: { defaultMessage: 'Link Repository to Resource', id: 'uVeSVH' },
    repositoryUrl: { defaultMessage: 'Repository URL', id: 'AA/tRJ' },
    repositoryUrlErrorMessage: { defaultMessage: 'Repository URL must be like: https://github.com/owner/repo-name.git', id: 'HekEs4' },
    connecting: { defaultMessage: 'Connecting...', id: '5y2qWO' },
    annotation: { defaultMessage: 'Annotation', id: 'dQtJBl' },
    editAnnotation: { defaultMessage: 'Edit Annotation', id: '7MvYEX' },
    addAnnotation: { defaultMessage: 'Add Annotation', id: 'vv2vLv' },
    addAnnotationToYourResource: { defaultMessage: 'Add annotation to your resource', id: 'YwZ7+5' },
    appHealthInfoCost: { defaultMessage: 'Costs for last 7 days', id: 'qeUtXk' },
    appHealthInfoCostCalculationPending: { defaultMessage: 'Cost calculation pending', id: 'wElgyk' },
    appHealthInfoAvailability: { defaultMessage: 'Availability', id: 'hOxIeP' },
    appHealthInfoHealthStatus: { defaultMessage: 'Health', id: 'hlmkcL' },
    appHealthInfoTransactionCount: { defaultMessage: 'Transactions for last 30 minutes', id: 'ED/jWk' },
    appHealthInfoAverageLatency: { defaultMessage: 'Average latency', id: 'DPnEXy' },
    appHealthInfoAverageMemoryUsage: { defaultMessage: 'Average memory usage', id: 'levHvg' },
    appHealthInfoAverageCPUUsage: { defaultMessage: 'Average CPU usage', id: 'JWM0w5' },
    appHealthInfoLastDataCaptureTime: { defaultMessage: 'Last data capture time', id: 'WIm1Zh' },
    annotationUpdateTitle: { defaultMessage: 'Update annotation', id: 'IAwPyV' },
    annotationUpdateInProgressDescription: { defaultMessage: 'We are updating annotation for your resource {name}', id: 'J6XCfl' },
    annotationUpdateSuccessDescription: { defaultMessage: 'Your annotation is updated successfully', id: 'A2BJnD' },
    annotationUpdateFailureDescription: {
        defaultMessage: 'Failed to update the annotation with the error: {errorMessage}',
        id: 'SL7+hl',
    },
});

export const AppHealth = defineMessages({
    unhealthy: { defaultMessage: 'Unhealthy', id: 'YdXbbC' },
    healthy: { defaultMessage: 'Healthy', id: 'TIDNOO' },
    degraded: { defaultMessage: 'Degraded', id: 'VQDmmK' },
    reportUnhealthyNode: { defaultMessage: 'Report unhealthy node', id: 'YE+vjH' },
    sendingReport: { defaultMessage: 'Sending a report...', id: '5GUtRJ' },
});
