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
    close: { defaultMessage: 'Close', id: 'rbrahO' },
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
    learnMoreAboutSupportedServices: { defaultMessage: 'Learn more about supported services', id: 'fxw/H0' },
    supportedServicesMessage: {
        defaultMessage:
            'To get optimal agent performance for diagnostics, metrics, knowledge, and more during preview, use resource groups that include one or more of these Azure compute services: Azure Kubernetes Service, Functions, Container Apps, or Web Apps.',
        id: '0aN6iW',
    },
    learnMore: { defaultMessage: 'Learn more', id: 'TdTXXf' },
});

export const SreAgentTabResources = defineMessages({
    activities: { defaultMessage: 'Activities', id: 'UmEsZF' },
    settings: { defaultMessage: 'Settings', id: 'D3idYv' },
    resourceMapping: { defaultMessage: 'Resource mapping', id: 'TdeXH0' },
    incidentHandlers: { defaultMessage: 'Incident handlers', id: '5/4URn' },
    logs: { defaultMessage: 'Logs', id: 'SNuQo7' },
    feedback: { defaultMessage: 'Give us feedback', id: 'aQPexO' },
    incidentManagement: { defaultMessage: 'Incident management', id: 'T7WpWs' },
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

export const PromptResources = defineMessages({
    latestPrompts: { defaultMessage: 'Latest prompts', id: 'cHsYT3' },
    popularPrompts: { defaultMessage: 'Popular prompts', id: 'rAGm8M' },
    promptLibrary: { defaultMessage: 'Prompt library', id: 'zvLfRe' },
    bestPracticesPrompt: { defaultMessage: 'Can you audit best practices for my resource?', id: '4OUjTL' },
    notWorkingPrompt: { defaultMessage: "Why isn't my application working?", id: 'DlSXUR' },
    availabilityPrompt: { defaultMessage: "Can you analyze my resource's availability over the last 24 hours?", id: 'EI4WZI' },
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
    deleteThreadTitle: { defaultMessage: "Deleting thread ''{title}''", id: 'OMp9VI' },
    deleteThreadInProgressDescription: { defaultMessage: 'Deleting thread', id: 'fLkL3F' },
    deleteThreadSuccessDescription: { defaultMessage: 'Thread was deleted successfully', id: 'ns7TyW' },
    deleteThreadFailureDescription: {
        defaultMessage: 'Failed to delete thread with error: {errorMessage}',
        id: 'ocQXDt',
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
            'Connect an incident management platform to the SRE Agent so that it can detect and respond to routine incident tasks and notifications. To change to a different platform, delete the connection to the current one.',
        id: 'aSn3tP',
    },
    refresh: { defaultMessage: 'Refresh', id: 'rELDbB' },
    incidentPlatform: { defaultMessage: 'Incident platform', id: 'EZBG/A' },
    newIncidentHandler: { defaultMessage: 'New incident handler', id: '1TlUPy' },
    incidentHandler: { defaultMessage: 'Incident handler', id: '0AwXvo' },
    createIncidentHandler: { defaultMessage: 'Create incident handler', id: 'C3qF2+' },
    incidentHandlerNamePlaceholder: { defaultMessage: 'Enter a handler name', id: 'yalJ8o' },
    customHandler: { defaultMessage: 'Custom handler', id: '71/osV' },
    id: { defaultMessage: 'ID', id: 'qlcuNQ' },
    severity: { defaultMessage: 'Severity', id: 'vCAhII' },
    dateModified: { defaultMessage: 'Date modified', id: 'KyDsjH' },
    allSeverity: { defaultMessage: 'All severity', id: 'zGhyFV' },
    title: { defaultMessage: 'Title', id: '9a9+ww' },
    titlePlaceholder: { defaultMessage: 'Enter title keywords', id: 'sH0O5v' },
    incidentType: { defaultMessage: 'Incident type', id: 'Udeffr' },
    impactedService: { defaultMessage: 'Impacted service', id: 'fdCjVS' },
    alertId: { defaultMessage: 'Alert ID', id: 'k8ZNgH' },
    titleContains: { defaultMessage: 'Title contains', id: 'brxlTt' },
    setUp: { defaultMessage: 'Set up', id: 'rrGMSx' },
    getAllIncidents: { defaultMessage: 'Get all incidents', id: 'JgQ1gX' },
    filterIncidents: { defaultMessage: 'Filter incidents', id: 'PJ2FQv' },
    priority: { defaultMessage: 'Priority', id: '8lCjAM' },
    allIncidentTypes: { defaultMessage: 'All incident types', id: 'G8H3+s' },
    allImpactedServices: { defaultMessage: 'All impacted services', id: 'MlX0aZ' },
    allPriorities: { defaultMessage: 'All priorities', id: 'uCkn4+' },
    baseIncident: { defaultMessage: 'Base incident', id: 'UjETJe' },
    last30Days: { defaultMessage: 'Last 30 days', id: 'Rfvi9/' },
    last7Days: { defaultMessage: 'Last 7 days', id: 'irFBKn' },
    last24Hours: { defaultMessage: 'Last 24 hours', id: '8O9cAb' },
    notSet: { defaultMessage: 'Not set', id: 'p5LNtB' },
    turnOff: { defaultMessage: 'Turn off', id: 'XZ+Fx6' },
    turnOn: { defaultMessage: 'Turn on', id: 'npvxpr' },
    off: { defaultMessage: 'Off', id: 'OvzONl' },
    on: { defaultMessage: 'On', id: 'Zh+5A6' },
    filterName: { defaultMessage: 'Filter name', id: 'abzUj6' },
    getStarted: { defaultMessage: 'Get started with incident management', id: 'NOMbLF' },
    optimizeAgentResponse: { defaultMessage: ' Optimize how the agent responds to incidents', id: 'zORAuY' },
    addIncidentFilter: { defaultMessage: 'Add incident filter', id: 'rja0QH' },
    enterFilterName: { defaultMessage: 'Enter filter name', id: 'ESChST' },
    selectIncidentType: { defaultMessage: 'Select incident type', id: 'Ci3omK' },
    selectImpactedService: { defaultMessage: 'Select impacted service', id: 'Pp+wb3' },
    selectPriority: { defaultMessage: 'Select priority', id: '1vUyy0' },
    setUpComplete: { defaultMessage: 'Setup complete', id: 'jOkfJV' },
    goToHandler: { defaultMessage: 'Go to handler', id: 'GuBSUa' },
    created: { defaultMessage: 'Created', id: 'ORGv1Q' },
    incidentHandlerName: { defaultMessage: 'Incident handler name', id: '5q8lCX' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    incidentManagementTabDescription: {
        defaultMessage:
            'Optimize how the agent investigates and responds to incidents by adding incident handlers and custom handlers to support a broad range of scenarios.',
        id: '5K0php',
    },
});

export const IncidentManagementPlatformResources = defineMessages({
    disconnected: { defaultMessage: 'Choose a platform', id: '/2OpVO' },
    pagerDuty: { defaultMessage: 'PagerDuty', id: '6UyZlH' },
    azMonitor: { defaultMessage: 'Azure Monitor', id: '7Nz2Ev' },
});

export const IncidentManagementNotificationResources = defineMessages({
    saveTitle: { defaultMessage: 'Save incident management configuration', id: '35UE1s' },
    saveStarted: { defaultMessage: 'Saving incident management configuration', id: 'TyvDrC' },
    saveSucceeded: { defaultMessage: 'Successfully saved incident management configuration', id: 'NrGEOo' },
    saveFailed: { defaultMessage: 'Failed to save incident management configuration. Error: {errorMessage}', id: 'slxYbm' },
    deleteFilterTitle: { defaultMessage: 'Deleting incident filter', id: '5ISX55' },
    deleteFilterInProgress: { defaultMessage: 'Deleting incident filter', id: '5ISX55' },
    deleteFilterSuccess: { defaultMessage: 'Successfully deleted incident filter', id: 'hBeG71' },
    deleteFilterError: { defaultMessage: 'Failed to delete incident filter', id: 'OKwfcc' },
    createFilterTitle: { defaultMessage: 'Creating incident filter', id: 'Dw4Wnc' },
    createFilterInProgress: { defaultMessage: 'Creating incident filter', id: 'Dw4Wnc' },
    createFilterSuccess: { defaultMessage: 'Successfully created incident filter', id: 'sGMl3p' },
    createFilterError: { defaultMessage: 'Failed to create incident filter', id: 'EwesO0' },
    enableFilterTitle: { defaultMessage: 'Enabling incident filter', id: 'JQTsu5' },
    enableFilterInProgress: { defaultMessage: 'Enabling incident filter', id: 'JQTsu5' },
    enableFilterSuccess: { defaultMessage: 'Successfully enabled incident filter', id: 'dt7GgF' },
    enableFilterError: { defaultMessage: 'Failed to enable incident filter', id: 'FREJ+R' },
    disableFilterTitle: { defaultMessage: 'Disabling incident filter', id: 'aqmxBU' },
    disableFilterInProgress: { defaultMessage: 'Disabling incident filter', id: 'aqmxBU' },
    disableFilterSuccess: { defaultMessage: 'Successfully disabled incident filter', id: 'EOf1cN' },
    disableFilterError: { defaultMessage: 'Failed to disable incident filter', id: '/kxGpW' },
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
    apiKeyInvalid: { defaultMessage: 'The access key is not valid. Please try again.', id: 'u4hSyh' },
    apiKeyRequired: { defaultMessage: 'Access key is required.', id: 'QEhO+z' },
    apiKeyFailedToValidate: { defaultMessage: 'Failed to validate the access key. Please try again.', id: 'Y7Lbpf' },
});

export const PagerDutyResources = defineMessages({
    pagerDutyApiKey: { defaultMessage: 'REST API access key', id: 'AuFOi8' },
    description: {
        defaultMessage:
            'Connect to PagerDuty with an access key. To get more information, go to pagerduty.com and then to the API Access Keys section.',
        id: 'G6vaGg',
    },
    changeKey: { defaultMessage: 'Change key', id: 'pT3qMe' },
    disconnectConfirmationTitle: { defaultMessage: 'Disconnect PagerDuty?', id: 'cTY8WU' },
    disconnectConfirmationMessage: {
        defaultMessage:
            'This will permanently delete the connection to PagerDuty. The agent will no longer be able to manage tickets. Are you sure you want to delete this connection?',
        id: 'rGl1yu',
    },
    connectedMessage: { defaultMessage: 'PagerDuty added', id: 'i0mHNo' },
});

export const AzMonitorResources = defineMessages({
    description: {
        defaultMessage:
            'Connect to Azure Monitor so that the SRE Agent can automatically monitor notifications from resources in the resource groups it manages, without additional provisioning.',
        id: 'XgXHWP',
    },
    disconnectConfirmationTitle: { defaultMessage: 'Disconnect Azure Monitor?', id: 'blSyDZ' },
    disconnectConfirmationMessage: {
        defaultMessage:
            'This will permanently delete the connection to Azure Monitor. The agent will no longer be integrated with Azure Monitor notifications. Are you sure you want to delete this connection?',
        id: 'w/UZrq',
    },
    connectedMessage: { defaultMessage: 'Azure Monitor added', id: '14xQ4i' },
});

export const SettingsTabResources = defineMessages({
    incidentManagement: { defaultMessage: 'Incident management', id: 'T7WpWs' },
    accessControl: { defaultMessage: 'Access control (IAM)', id: '7w4v59' },
    basics: { defaultMessage: 'Basics', id: 'itC9lG' },
    grafanaDashboard: { defaultMessage: 'Grafana dashboard', id: '2zi2Yj' },
    managedResources: { defaultMessage: 'Managed resource groups', id: 'yilQrD' },
});

export const GrafanaDashboardResources = defineMessages({
    resourceName: { defaultMessage: 'Resource name', id: 'eqYdSS' },
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
        defaultMessage: 'You need permissions to set up a Grafana dashboard.',
        id: 'gnC7c5',
    },
    tooltipContent: {
        defaultMessage:
            'Once a Grafana dashboard is provisioned and linked, it may take some time for resources to fully populate the dashboard.',
        id: 'piCpTn',
    },
    description: {
        defaultMessage: `Azure Managed Grafana is a fully managed service for analytics and monitoring. To add the service and a Grafana dashboard, you need to create an Azure Managed Grafana resource. The necessary permissions will be automatically assigned so that the dashboard can display monitoring data. Azure Managed Grafana will incur costs in your subscription.`,
        id: '7qt3qA',
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
            'The name must begin with a letter, end with a letter or number, and contain only letters, numbers, and hyphens. It must be 2 to 23 characters long.',
        id: 'WuZcUT',
    },
    enterResourceName: { defaultMessage: 'Enter resource name', id: '3DzXFS' },
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
            'This logical map shows how your applications resources are connected across multiple resource groups, regions, and subscriptions. The agent analyzes these resources and organizes them into an app group based on the primary resource.',
        id: 'QkOLyp',
    },
});

export const GraphEdgeLabel = defineMessages({
    contains: { defaultMessage: 'Contains', id: '2tOWr2' },
    linkedTo: { defaultMessage: 'Linked to', id: 'Xa3AHo' },
    connectsTo: { defaultMessage: 'Connects to', id: 'gO/IxZ' },
    localAuth: { defaultMessage: 'Local auth', id: 'mq/sud' },
    managedIdentity: { defaultMessage: 'Managed identity', id: 'Ys9AIu' },
    hosts: { defaultMessage: 'Hosts', id: 'swRudj' },
    hostedOn: { defaultMessage: 'Hosted on', id: 'rpx6XY' },
    revisionOf: { defaultMessage: 'Revision of', id: 'X4Y2nv' },
    ownedBy: { defaultMessage: 'Owned by', id: 'boWXYt' },
    monitoredBy: { defaultMessage: 'Monitored by', id: 'rk4tYE' },
    isPartOf: { defaultMessage: 'Is part of', id: 'DMd1Ql' },
    backedBy: { defaultMessage: 'Backed by', id: '7wHL/l' },
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

export const IncidentHandlerCreateResources = defineMessages({
    generateHandler: { defaultMessage: 'Generate handler', id: 'QZoqFk' },
    reviewAndEdit: { defaultMessage: 'Review + Edit', id: 'AsJvG0' },
    priority: { defaultMessage: 'Priority', id: '8lCjAM' },
    dateCreated: { defaultMessage: 'Date Created', id: 'jY/3Cs' },
    title: { defaultMessage: 'Title', id: '9a9+ww' },
    incidentId: { defaultMessage: 'Incident ID', id: 'MB9ceM' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    tool: { defaultMessage: 'Tool', id: 'h6183G' },
    description: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    last15days: { defaultMessage: 'Last 15 days', id: '5l3nDr' },
    last30days: { defaultMessage: 'Last 30 days', id: 'Rfvi9/' },
    last60days: { defaultMessage: 'Last 60 days', id: 'KLYuRX' },
    last90days: { defaultMessage: 'Last 90 days', id: 'mgYBYo' },
    chooseIncidentTitle: { defaultMessage: 'Choose Incidents', id: 'f525yt' },
    chooseIncidentDescription: {
        defaultMessage: `These are previous incidents similar to the filter you set for incident type. Choose any you'd like the agent to learn from.`,
        id: 'XtZDRH',
    },
    chooseToolsTitle: { defaultMessage: 'Available Tools', id: 'bF6CsW' },
    chooseToolsDescription: {
        defaultMessage: `The agent uses these available tools to generate incident handler instructions, based on patterns it learned from the past incidents. You can remove any tools you don't want the agent to use.`,
        id: 'U4e+Yf',
    },
    customInstructionTitle: { defaultMessage: 'Custom instruction', id: '7KZ+mT' },
    customInstructionDescription: {
        defaultMessage:
            'An incident handler contains common instructions for how to handle an incident. Add custom instructions for specific conditions not covered by default response.',
        id: 'WC+0um',
    },
    customInstructionPlaceholder: { defaultMessage: 'Enter custom instructions', id: '25brjd' },
    next: { defaultMessage: 'Next', id: '9+Ddtu' },
    skip: { defaultMessage: 'Skip', id: '/4tOwT' },
    cancel: { defaultMessage: 'Cancel', id: '47FYwb' },
    previous: { defaultMessage: 'Previous', id: 'JJNc3c' },
    generate: { defaultMessage: 'Generate', id: 'Pc+tM3' },
    save: { defaultMessage: 'Save', id: 'jvo0vs' },
    handlerAddNotificationTitle: { defaultMessage: 'Add custom incident handler', id: 'DWon+H' },
    handlerAddNotificationDescription: { defaultMessage: 'Adding custom incident handler', id: 'h07fER' },
    handlerAddNotificationSuccess: { defaultMessage: 'The custom incident handler was successfully added', id: 'sVbKA0' },
    handlerAddNotificationError: { defaultMessage: 'Failed to add the custom incident handler. Error: {errorMessage}', id: '0Pt22l' },
    customHandlerName: { defaultMessage: 'Custom handler name', id: 'QtNVS/' },
    customHandlerDescription: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    incidentManagement: { defaultMessage: 'Incident management', id: 'T7WpWs' },
    newCustomHandler: { defaultMessage: 'New custom handler', id: 'DHc2gc' },
});
