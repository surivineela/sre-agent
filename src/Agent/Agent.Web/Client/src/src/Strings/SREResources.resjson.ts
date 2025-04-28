export enum SreAgentResources {
    actions = 'Actions',
    activeThreads = 'Active threads',
    allThreads = 'All threads',
    agent = 'Agent',
    agentDetails = 'Agent details',
    agents = 'Agents',
    apply = 'Apply',
    buildingKnowledgeGraph = 'Building knowledge graph',
    cancel = 'Cancel',
    client = 'Client',
    createAgent = 'Create agent',
    connectedToTeams = 'Connected to Teams',
    create = 'Create',
    delete = 'Delete',
    discard = 'Discard',
    endpoint = 'Endpoint',
    enterName = 'Enter name',
    failed = 'Failed',
    filterMessage = 'Showing the first 1000 results. Filter to narrow down the list.',
    fieldRequired = 'This field is required',
    getMoreInfo = 'Get more info',
    grafana = 'Grafana',
    incidents = 'Incidents',
    location = 'Location',
    managedResources = 'Managed resources',
    managedResourceGroups = 'Managed resource groups',
    name = 'Name',
    newThread = 'New thread',
    overview = 'Overview',
    pendingApprovals = 'Pending approvals',
    projectDetails = 'Project details',
    projectDetailsDescription = 'Select the subscription to manage deployed resources and costs. Use resources groups like folders to organize and manage all your resources.',
    refresh = 'Refresh',
    region = 'Region',
    regionPlaceHolder = 'Select region',
    resourceGroup = 'Resource group',
    resourceGroupName = 'Resource group name',
    save = 'Save',
    scope = 'Scope',
    selectResourceGroups = 'Select resource groups',
    selectResourceGroupsToMonitor = 'Select resource groups to monitor',
    search = 'Search',
    summary = 'Summary',
    status = 'Status',
    success = 'Success',
    startTime = 'Start Time',
    stop = 'Stop',
    sreAgentSpace = 'SRE Agent Space',
    subscription = 'Subscription',
    totalThreads = 'Total threads',
    tasks = 'Tasks',
    task = 'Task',
    threadCount = 'Thread count',
    logicAppName = 'Logic App name',
    yes = 'Yes',
    no = 'No',
}

export enum SreAgentTabs {
    activities = 'Activities',
    settings = 'Settings',
    managedResources = 'Managed resources',
}

export enum ActionsResources {
    actions = 'Actions',
    allStatuses = 'All statuses',
    inProgress = 'In progress',
    time = 'Time',
    status = 'Status',
    completed = 'Completed',
    failed = 'Failed',
    pending = 'Pending',
}

export enum Locations {
    centraluseuap = 'Central US EUAP',
}

export enum AccessControlResources {
    accessControl = 'Access control',
    accessControlDescription = 'Manage access to the SRE Agent resource by clicking on the link below.',
    openAccessControl = 'Open Access control',
}

export enum Activities {
    createThreadButtonText = 'New chat thread',
    chatPivotHeader = 'Chat',
    actionsPivotHeader = 'Actions',
    chatInputPlaceholder = 'I want to...',
    sreAgentDisplayName = 'Azure SRE Agent',
}

export enum Activities_ThreadHeader {
    deleteThreadTitle = 'Delete thread',
    deleteThreadInProgressDescription = 'Deleting thread with title {0}',
    deleteThreadSuccessDescription = 'Thread with title {0} deleted successfully',
    deleteThreadFailureDescription = 'Failed to delete thread with title {0} with error: {1}',
    deleteThreadDialogTitle = 'Delete thread?',
    deleteThreadDialogDescription = 'This will permanently delete the chat and all actions in this thread.',
}

export enum Actions_Headers {
    action = 'Action',
    time = 'Time',
    status = 'Status',
}
export enum Actions_Status {
    failed = 'Failed',
    completed = 'Completed',
    inProgress = 'In progress',
    pending = 'Pending',
}

export enum IncidentManagementResources {
    incidentManagement = 'Incident management',
    incidentManagementDescription = 'Automate incident response with AI-powered monitoring and resolution..',
    incidentPlatform = 'Incident platform',
}

export enum IncidentManagementPlatformResources {
    disconnected = 'Disconnected',
    pagerDuty = 'PagerDuty',
}

export enum IncidentManagementNotifications {
    saveTitle = 'Save incident management configuration',
    saveStarted = 'Saving incident management configuration',
    saveSucceeded = 'Successfully saved incident management configuration',
    saveFailed = 'Failed to save incident management configuration',
}

export enum IncidentManagementSaveErrors {
    managedConnectionFailure = 'Failed to create managed connection',
    logicAppCreateFailure = 'Failed to create logic app',
    logicAppDeleteFailure = 'Failed to delete logic app',
    configFailure = 'Failed to save incident management configuration',
}

export enum PagerDutyResources {
    pagerDutyDescription = 'Integrate with PagerDuty to enable automated incident detection, triage, and response. The Azure SRE Agent analyzes alert patterns, suggest remediation steps, and automatically resolve common issues without human intervention.',
    pagerDutyApiKey = 'PagerDuty API Key',
}

export enum Settings_Tabs {
    incidentManagement = 'Incident management',
    accessControl = 'Access control',
    agentDetails = 'Agent details',
    grafanaInsights = 'Grafana insights',
}

export enum GrafanaDashboardResources {
    instructions = 'Set up a custom Grafana dashboard to get visual insights into your infrastructure, with daily reports and resource-specific health metrics.',
    grafanaCreationTitle = 'Provisioning Grafana dashboard',
    grafanaCreationInProgress = 'Provisioning of your Grafana dashboard is in progress.',
    grafanaCreationSuccess = 'Grafana dashboard creation succeeded',
    grafanaCreationFailed = 'Failed to create the Grafana dashboard with the error: {0}',
    grafanaRoleAssignmentTitle = 'Provisioning Grafana role assignment',
    grafanaRoleAssignmentInProgress = 'Provisioning of your Grafana role assignment is in progress.',
    grafanaRoleAssignmentSuccess = 'Grafana role assignment creation succeeded',
    grafanaRoleAssignmentFailed = 'Failed to create the Grafana role assignment with the error: {0}',
    postCreationInstructions = `After the Grafana dashboard is finished provisioning, you'll need to generate an API key for you agent to communicate with it. Follow these steps:`,
    stepOneTitle = 'Step 1: Open Cloud Shell',
    stepOneInstructions = '- Click on the "Cloud Shell" button in the top right corner of the Azure portal.',
    stepTwoTitle = 'Step 2: Generate API key',
    stepTwoInstructions = '- Run "az account set --subscription {0}"',
    stepThreeTitle = 'Step 3: Generate an API key',
    stepThreeInstructions = '- Run: "az grafana api-key create --key {0} --name {1} --resource-group {2} --role admin --time-to-live 365d" (Note: If the CLI asks you to install an extension to support Grafana commands, type "Y" and hit enter)',
    stepFourTitle = 'Step 4: Copy the API key and paste it here',
    apiKey = 'API Key',
    linkGrafanaDashboardTitle = 'Link Grafana dashboard',
    linkGrafanaDashboardInProgress = 'Linking Grafana dashboard to SRE Agent in progress',
    linkGrafanaDashboardSuccess = 'Grafana dashboard linked successfully',
    linkGrafanaDashboardFailed = 'Failed to link Grafana dashboard',
    grafanaResource = 'Grafana resource',
    grafanaDashboardUrl = 'Grafana dashboard URL',
    createGrafanaDashboard = 'Create Grafana dashboard',
    createGrafanaDashboardSameResourceGroup = 'Create Grafana dashboard in the same resource group',
    generateApiKey = 'Generate API key',
    inProgress = '- in progress',
    completed = '- completed',
    firstStepInstructions = 'First, a Grafana resource needs to be created.',
    uniqueGrafanaResourceNameError = 'Grafana resource must be unique within the resource group.',
    invalidGrafanaResourceNameError = 'Grafana resource name must be between 2 to 23 characters long. They must begin with a letter and end with a letter or digit.',
}
