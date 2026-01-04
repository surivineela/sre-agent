import { defineMessages } from 'react-intl';

export const SreAgentResources = defineMessages({
    aiGeneratedHyphenated: { defaultMessage: 'AI-generated', id: 'vfrUDN' },
    all: { defaultMessage: 'All', id: 'zQvVDJ' },
    allAutonomyLevels: { defaultMessage: 'All autonomy levels', id: 'uI0Lmu' },
    addIdentity: { defaultMessage: 'Add identity', id: 'xUuESs' },
    azureSreAgent: { defaultMessage: 'Azure SRE Agent', id: 'Erci0g' },
    agentPermissionsLevel: { defaultMessage: 'Agent permissions level', id: '+bJIWo' },
    agentEndpoint: { defaultMessage: 'Agent endpoint', id: 's7DlV0' },
    agentSpace: { defaultMessage: 'SRE Agent Space', id: 'iv1ryQ' },
    back: { defaultMessage: 'Back', id: 'cyR7Kh' },
    collapse: { defaultMessage: 'Collapse', id: 'W/V6+Y' },
    deleteAgentTitle: { defaultMessage: 'Delete SRE Agent', id: 'ThSX0k' },
    deleteAgentDescription: {
        defaultMessage:
            'This will permanently delete the agent and all its chat and actions threads. Are you sure you want to delete this agent?',
        id: 'tTjB+b',
    },
    deleteAgentNotificationTitle: { defaultMessage: '{count, plural, one {Delete SRE Agent} other {Delete SRE Agents}}', id: 'Kfr1WB' },
    deleteAgentNotificationDescription: {
        defaultMessage: '{count, plural, one {Deleting SRE Agent {name}} other {Deleting SRE Agents}}',
        id: 'jIcHPc',
    },
    deleteAgentNotificationSuccess: {
        defaultMessage: '{count, plural, one {SRE Agent {name} deleted successfully} other {SRE Agents deleted successfully}}',
        id: '8yfUKp',
    },
    deleteAgentNotificationError: {
        defaultMessage:
            '{count, plural, one {Failed to delete SRE Agent {name}} other {Failed to delete SRE Agents}}{errorMessage, select, undefined {} other {: {errorMessage}}}',
        id: 'MGZggN',
    },

    deleteKustoToolNotificationTitle: {
        defaultMessage: '{count, plural, one {Delete Kusto tool} other {Delete Kusto tools}}',
        id: 'zpENeF',
    },
    deleteKustoToolNotificationDescription: {
        defaultMessage: '{count, plural, one {Deleting Kusto tool {name}} other {Deleting Kusto tools}}',
        id: '/8swfk',
    },
    deleteKustoToolNotificationSuccess: {
        defaultMessage: '{count, plural, one {Kusto tool {name} deleted successfully} other {Kusto tools deleted successfully}}',
        id: '7K6W8s',
    },
    deleteKustoToolNotificationError: {
        defaultMessage:
            '{count, plural, one {Failed to delete Kusto tool {name}} other {Failed to delete Kusto tools}}{errorMessage, select, undefined {} other {: {errorMessage}}}',
        id: 'yrDI3f',
    },

    deleteIncidentTriggerNotificationTitle: {
        defaultMessage: '{count, plural, one {Delete incident trigger} other {Delete incident triggers}}',
        id: 'TGc4Lh',
    },
    deleteIncidentTriggerNotificationDescription: {
        defaultMessage: '{count, plural, one {Deleting incident trigger {name}} other {Deleting incident triggers}}',
        id: '3hmrCJ',
    },
    deleteIncidentTriggerNotificationSuccess: {
        defaultMessage:
            '{count, plural, one {Incident trigger {name} deleted successfully} other {Incident triggers deleted successfully}}',
        id: 'AEgxqN',
    },
    deleteIncidentTriggerNotificationError: {
        defaultMessage:
            '{count, plural, one {Failed to delete incident trigger {name}} other {Failed to delete incident triggers}}{errorMessage, select, undefined {} other {: {errorMessage}}}',
        id: '5iE4cZ',
    },
    deleteIncidentTriggerConfirmationDescription: {
        defaultMessage: 'Are you sure you want to delete this incident trigger? This action cannot be undone.',
        id: 'hseqm+',
    },
    deleteIncidentTriggerMenuTitle: {
        defaultMessage: 'Delete Incident Trigger',
        id: 'Rtd2bs',
    },
    deleteScheduledTriggerMenuTitle: {
        defaultMessage: 'Delete Scheduled Trigger',
        id: 'b1DYE3',
    },

    deleteScheduledTaskNotificationTitle: {
        defaultMessage: '{count, plural, one {Delete scheduled task} other {Delete scheduled tasks}}',
        id: 'WuyjCf',
    },
    deleteScheduledTaskNotificationDescription: {
        defaultMessage: '{count, plural, one {Deleting scheduled task {name}} other {Deleting scheduled tasks}}',
        id: 'bjDKAe',
    },
    deleteScheduledTaskNotificationSuccess: {
        defaultMessage: '{count, plural, one {Scheduled task {name} deleted successfully} other {Scheduled tasks deleted successfully}}',
        id: 'rIbCjV',
    },
    deleteScheduledTaskNotificationError: {
        defaultMessage:
            '{count, plural, one {Failed to delete scheduled task {name}} other {Failed to delete scheduled tasks}}{errorMessage, select, undefined {} other {: {errorMessage}}}',
        id: 'G4gaOn',
    },

    edit: { defaultMessage: 'Edit', id: 'wEQDC6' },
    stopAgent: { defaultMessage: 'Stop agent', id: 'JKFypr' },
    stopAgentDescription: { defaultMessage: 'Temporarily stop the agent from all activities.', id: 'DFfvL6' },
    stoppingSreAgentTitle: { defaultMessage: 'Stopping SRE Agent', id: 'XDcbqQ' },
    stoppingSreAgentInProgress: { defaultMessage: 'Stopping SRE Agent {name} in progress', id: 'iMaY2Q' },
    stoppingSreAgentSuccess: { defaultMessage: 'SRE Agent {name} stopped successfully', id: '0DUgkZ' },
    stoppingSreAgentFailed: { defaultMessage: 'Failed to stop SRE Agent {name}', id: '4Xep39' },
    stoppingSreAgentFailedWithError: { defaultMessage: 'Failed to stop SRE Agent {name}: {error}', id: 'JnDKv3' },
    startAgent: { defaultMessage: 'Start agent', id: 'tu6JYo' },
    startAgentDescription: { defaultMessage: 'The agent will resume all activities.', id: 'c5hwBU' },
    startingSreAgentTitle: { defaultMessage: 'Starting SRE Agent', id: 'JSW9Xw' },
    startingSreAgentInProgress: { defaultMessage: 'Starting SRE Agent {name} in progress', id: 'FpUGgx' },
    startingSreAgentSuccess: { defaultMessage: 'SRE Agent {name} started successfully', id: 'F8v2Mz' },
    startingSreAgentFailed: { defaultMessage: 'Failed to start SRE Agent {name}', id: '8Zl3PA' },
    startingSreAgentFailedWithError: { defaultMessage: 'Failed to start SRE Agent {name}: {error}', id: '6tzuXg' },
    expand: { defaultMessage: 'Expand', id: '0oLj/t' },
    sreAgent: { defaultMessage: 'Azure SRE Agent', id: 'Erci0g' },
    add: { defaultMessage: 'Add', id: '2/2yg+' },
    remove: { defaultMessage: 'Remove', id: 'G/yZLu' },
    new: { defaultMessage: 'New', id: 'bW7B87' },
    next: { defaultMessage: 'Next', id: '9+Ddtu' },
    resources: { defaultMessage: 'Resources', id: 'c/KktL' },
    notApplicable: { defaultMessage: 'Not applicable', id: '61zy45' },
    NA: { defaultMessage: 'N/A', id: 'PW+sL4' },
    progress: { defaultMessage: 'Progress', id: 'sIMS7i' },
    extendedAgents: { defaultMessage: 'Extended agents', id: '387FD2' },
    slashCommands: { defaultMessage: 'Slash commands', id: '3/wF0G' },
    backToCommands: { defaultMessage: 'Back to commands', id: 'GCmxza' },
    loadingAgents: { defaultMessage: 'Loading agents…', id: 'b8PngH' },
    noAgentsFound: { defaultMessage: 'No agents found', id: '451B6Z' },
    none: { defaultMessage: 'None', id: '450Fty' },
    feedbackDialogTitle: {
        id: 'Nrc9ba',
        defaultMessage: 'Thank you for your feedback!',
    },
    approve: {
        id: 'WCaf5C',
        defaultMessage: 'Approve',
    },
    copy: {
        id: '4l6vz1',
        defaultMessage: 'Copy',
    },
    copied: {
        id: 'p556q3',
        defaultMessage: 'Copied',
    },
    copyToClipboard: {
        id: 'aCdAsI',
        defaultMessage: 'Copy to clipboard',
    },
    copyLinkToThread: {
        id: 'Zidpq7',
        defaultMessage: 'Copy link to thread',
    },
    kustoQueryTesterTitle: {
        id: 'dtQgfG',
        defaultMessage: 'Query Tester',
    },
    kustoQueryTesterSubtitle: {
        id: 'Zve9uZ',
        defaultMessage: 'Test your Kusto query before saving',
    },
    kustoQueryTesterParameterLabel: {
        id: '67UmDz',
        defaultMessage: 'Parameter syntax:',
    },
    kustoQueryTesterParameterUsage: {
        id: 'h/OQTK',
        defaultMessage: 'Use ##ParamName## in your query to define parameter placeholders.',
    },
    kustoQueryTesterParameterExample: {
        id: 'E9CWuE',
        defaultMessage: 'Example: where SubscriptionId == "##SubscriptionId##"',
    },
    kustoQueryTesterParameterNote: {
        id: '8LMIGa',
        defaultMessage: 'These will be replaced with actual values at runtime.',
    },
    kustoQueryTesterParameterValuesLabel: {
        id: 'KryvVM',
        defaultMessage: 'Parameter values (for testing)',
    },
    kustoQueryTesterParameterPlaceholder: {
        id: 'djuhEG',
        defaultMessage: 'Enter {type} value',
    },
    kustoQueryTesterResultsLabel: {
        id: 'mRJIzw',
        defaultMessage: 'Results ({count} {count, plural, one {row} other {rows}})',
    },
    kustoQueryTesterExecutionTime: {
        id: 'k0VNHX',
        defaultMessage: '{milliseconds}ms',
    },
    kustoQueryTesterNoResults: {
        id: 'BSQQkf',
        defaultMessage: 'Query returned no results',
    },
    pythonToolBuilderIntentPlaceholder: {
        id: 'wm4GRX',
        defaultMessage: 'Example: Parse JSON logs and extract error messages',
    },
    pythonToolBuilderTestRunning: {
        id: 'ORcS+j',
        defaultMessage: 'Testing…',
    },
    pythonToolBuilderNameLabel: {
        id: 'INiSE2',
        defaultMessage: 'Tool Name',
    },
    pythonToolBuilderDescriptionLabel: {
        id: 'Q8Qw5B',
        defaultMessage: 'Description',
    },
    pythonToolBuilderTimeoutLabel: {
        id: 'g6WtKF',
        defaultMessage: 'Timeout (seconds)',
    },
    // Python Tool Creator - Split Panel UI
    pythonToolCreatorAssistantTab: {
        id: 'wSAvhu',
        defaultMessage: 'Assistant',
    },
    pythonToolCreatorTestPlaygroundTab: {
        id: 'smIXdP',
        defaultMessage: 'Test playground',
    },
    pythonToolCreatorToolNamePlaceholder: {
        id: '12QT7S',
        defaultMessage: 'e.g., check_url_status',
    },
    pythonToolCreatorDescriptionPlaceholder: {
        id: '2pzmrJ',
        defaultMessage: 'What does this function do?',
    },
    pythonToolCreatorGenerateButton: {
        id: 'Pc+tM3',
        defaultMessage: 'Generate',
    },
    pythonToolCreatorGeneratingButton: {
        id: 'tB02Wz',
        defaultMessage: 'Generating…',
    },
    pythonToolCreatorTestButton: {
        id: 'xu6eM8',
        defaultMessage: 'Test',
    },
    pythonToolCreatorTestPending: {
        id: '2myhph',
        defaultMessage: 'Test the function to continue.',
    },
    pythonToolCreatorTestRunning: {
        id: 'Jdy/E7',
        defaultMessage: 'Testing function…',
    },
    pythonToolCreatorTestSuccess: {
        id: 'kjUC48',
        defaultMessage: 'Function test succeeded.',
    },
    pythonToolCreatorTestError: {
        id: 'fn9+0+',
        defaultMessage: 'Function test failed: {message}',
    },
    pythonToolCreatorFixWithAI: {
        id: 'zztZjz',
        defaultMessage: 'Fix with AI',
    },
    pythonToolCreatorTestMissingParams: {
        id: '++2B26',
        defaultMessage: 'Provide values for: {parameters}',
    },
    pythonToolCreatorTestPlaygroundWarning: {
        id: 'B2qjyO',
        defaultMessage: 'Test the tool to continue',
    },
    pythonToolCreatorParamFieldWarning: {
        id: 'Q7/1w4',
        defaultMessage: 'Required for testing',
    },
    pythonToolCreatorParameterValuesLabel: {
        id: 'KryvVM',
        defaultMessage: 'Parameter values (for testing)',
    },
    pythonToolCreatorParameterPlaceholder: {
        id: 'djuhEG',
        defaultMessage: 'Enter {type} value',
    },
    pythonToolCreatorResultsLabel: {
        id: 'oi3wVV',
        defaultMessage: 'Test results',
    },
    pythonToolCreatorExecutionTime: {
        id: 'piGAtN',
        defaultMessage: 'Executed in {milliseconds}ms',
    },
    pythonToolCreatorGenerateError: {
        id: 'Lq6q6U',
        defaultMessage: 'Failed to generate code: {message}',
    },
    pythonToolCreatorPromptLabel: {
        id: 'uGPikn',
        defaultMessage: 'Describe what the function should do',
    },
    pythonToolCreatorMainFunctionRequired: {
        id: 'x+Cs2X',
        defaultMessage: 'Function must contain a main() function',
    },
    pythonToolCreatorTimeoutMin: {
        id: 'q9vJvD',
        defaultMessage: 'Timeout must be at least 5 seconds',
    },
    pythonToolCreatorTimeoutMax: {
        id: 'cJucrk',
        defaultMessage: 'Timeout cannot exceed 900 seconds',
    },
    deleteToolTitle: {
        id: 'bn84V+',
        defaultMessage: 'Delete tool',
    },
    deleteSubagentTitle: {
        id: 'dqkJF6',
        defaultMessage: 'Delete subagent',
    },
    deleteToolNotificationError: {
        id: '/F7sHX',
        defaultMessage: 'Failed to delete tool {name}',
    },
    custom: {
        id: 'Sjo1P4',
        defaultMessage: 'Custom',
    },
    default: {
        id: 'lKv8ex',
        defaultMessage: 'Default',
    },
    deny: {
        id: 'htvX+Z',
        defaultMessage: 'Deny',
    },
    continue: {
        id: 'acrOoz',
        defaultMessage: 'Continue',
    },
    authorize: {
        id: 'QwnGVY',
        defaultMessage: 'Authorize',
    },
    canceled: {
        id: 'PFtMy9',
        defaultMessage: 'Canceled',
    },
    authorized: {
        id: 'NAepnj',
        defaultMessage: 'Authorized',
    },
    authorizedBy: {
        defaultMessage: 'Authorized by',
        id: 'vDbj1h',
    },
    canceledBy: {
        defaultMessage: 'Canceled by',
        id: 'boVrya',
    },
    authorizeUsingCreds: {
        id: 'KbCGsK',
        defaultMessage: "If you click 'Authorize', this operation will be executed on your behalf using your credentials.",
    },
    continueUsingCreds: {
        id: 'bKgVBL',
        defaultMessage: "If you click 'Continue', this operation will be executed using agent identity credentials.",
    },
    beingExecutedUsingCreds: {
        id: 'yVc78v',
        defaultMessage: 'This operation is being executed using the agent identity credentials.',
    },
    beingExecutedUsingApproverCreds: {
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
    confirm: {
        defaultMessage: 'Confirm',
        id: 'N2IrpM',
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
    requestError: { defaultMessage: 'Request error', id: 'UQUIP7' },
    insights: { defaultMessage: 'Insights', id: 'xK7rmd' },
    incidentDescriptionLabel: { defaultMessage: 'Incident description', id: 'muUj+F' },
    timeFrameLabel: { defaultMessage: 'Time frame', id: 'HljkEY' },
    affectedResourcesLabel: { defaultMessage: 'Affected resources', id: 'lP5P2a' },
    keyFindingsLabel: { defaultMessage: 'Key findings', id: 'KI/Heo' },
    detailsLabel: { defaultMessage: 'Details', id: 'Lv0zJu' },
    update: { defaultMessage: 'Update', id: 'BWpuKl' },
    actions: { defaultMessage: 'Actions', id: 'wL7VAE' },
    active: { defaultMessage: 'Active', id: '3a5wL8' },
    paused: { defaultMessage: 'Paused', id: 'C2iTEH' },
    acknowledged: { defaultMessage: 'Acknowledged', id: 'FnKIAW' },
    triggered: { defaultMessage: 'Triggered', id: 'Zqa4dQ' },
    closed: { defaultMessage: 'Closed', id: 'Fv1ZSz' },
    close: { defaultMessage: 'Close', id: 'rbrahO' },
    mitigated: { defaultMessage: 'Mitigated', id: 'dnXgff' },
    resolved: { defaultMessage: 'Resolved', id: 'W6nSYE' },
    activeThreads: { defaultMessage: 'Active threads', id: 'rFlkvY' },
    unread: { defaultMessage: 'Unread', id: 'jabB4C' },
    agent: { defaultMessage: 'Agent', id: 'QGVI63' },
    user: { defaultMessage: 'User', id: 'EwRIOm' },
    inProgress: { defaultMessage: 'In progress', id: 'q1WWIr' },
    agentDetails: { defaultMessage: 'Agent details', id: 'Wf6bDe' },
    agents: { defaultMessage: 'Agents', id: 'GBnvl1' },
    apply: { defaultMessage: 'Apply', id: 'EWw/tK' },
    buildingKnowledgeGraph: { defaultMessage: 'Building knowledge graph', id: '8Wiwkg' },
    client: { defaultMessage: 'Client', id: 'JX0502' },
    createAgent: { defaultMessage: 'Create agent', id: 'UrGI9K' },
    connectedToTeams: { defaultMessage: 'Connected to Teams', id: '7cS956' },
    create: { defaultMessage: 'Create', id: 'VzzYJk' },
    delete: { defaultMessage: 'Delete', id: 'K3r6DQ' },
    rename: { defaultMessage: 'Rename', id: 'iXNbPf' },
    renameFieldLabel: { defaultMessage: 'Rename thread', id: '4Orfd8' },
    threadTitleEmptyError: { defaultMessage: 'Thread title cannot be empty', id: '/N0Usl' },
    renamePermissionsError: { defaultMessage: 'You do not have permission to rename this thread.', id: 'kT9YDi' },
    generateInsights: { defaultMessage: 'Generate Session Insights', id: '6gD0ie' },
    generatingInsights: { defaultMessage: 'Generating insights...', id: '751M54' },
    generateInsightsPermissionsError: { defaultMessage: 'You do not have permission to generate insights for this thread.', id: '19xydZ' },
    discard: { defaultMessage: 'Discard', id: 'nmpevl' },
    disconnect: { defaultMessage: 'Disconnect', id: 'qj1uhz' },
    endpoint: { defaultMessage: 'Endpoint', id: 'ljmS5P' },
    enterName: { defaultMessage: 'Enter name', id: 'OJ2u8k' },
    failed: { defaultMessage: 'Failed', id: 'vXCeIi' },
    filterMessage: { defaultMessage: 'Showing the first 1000 results. Filter to narrow down the list.', id: 'u64mw7' },
    fieldRequired: { defaultMessage: 'This field is required', id: 'TKmub+' },
    info: { defaultMessage: 'Info', id: 'we4Lby' },
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
    other: { defaultMessage: 'Other', id: '/VnDMl' },
    select: { defaultMessage: 'Select', id: 'kQAf2d' },
    selectAll: { defaultMessage: 'Select all', id: '94Fg25' },
    refresh: { defaultMessage: 'Refresh', id: 'rELDbB' },
    region: { defaultMessage: 'Region', id: 'lnaWo/' },
    regionPlaceHolder: { defaultMessage: 'Select region', id: 'tshYzs' },
    resourceGroup: { defaultMessage: 'Resource group', id: '+uAdUZ' },
    resourceGroups: { defaultMessage: 'Resource groups', id: '/zQv2D' },
    resourceGroupName: { defaultMessage: 'Resource group name', id: 'xVPoso' },
    resourceType: { defaultMessage: 'Resource type', id: 'WHleoJ' },
    restart: { defaultMessage: 'Restart', id: '5kK+j9' },
    save: { defaultMessage: 'Save', id: 'jvo0vs' },
    scope: { defaultMessage: 'Scope', id: 'nso3Mj' },
    selectResourceGroups: { defaultMessage: 'Select resource groups', id: 'ftfFhS' },
    selectResourceGroupsToMonitor: { defaultMessage: 'Select resource groups to monitor', id: 'CfGC/2' },
    search: { defaultMessage: 'Search', id: 'xmcVZ0' },
    service: { defaultMessage: 'Service', id: 'n7yYXG' },
    summary: { defaultMessage: 'Summary', id: 'RrCui3' },
    incidentResearch: { defaultMessage: 'Incident research', id: 'JjNi2L' },
    incidentResearchInProgress: { defaultMessage: 'Incident research in progress...', id: 'aPWGNa' },
    investigationSteps: { defaultMessage: 'Investigation steps', id: 'ftvKVy' },
    validationSteps: { defaultMessage: 'Validation steps', id: 'YlGniM' },
    researchSteps: { defaultMessage: 'Research steps', id: 'OV6svF' },
    expandAll: { defaultMessage: 'Expand all', id: 't323u5' },
    collapseAll: { defaultMessage: 'Collapse all', id: 'v0fBOU' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    success: { defaultMessage: 'Success', id: 'xrKHS6' },
    startTime: { defaultMessage: 'Start Time', id: '5QYdPU' },
    stop: { defaultMessage: 'Stop', id: 'q/uwLT' },
    stopped: { defaultMessage: 'Stopped', id: '1LBny5' },
    sreAgentSpace: { defaultMessage: 'SRE Agent Space', id: 'iv1ryQ' },
    subscription: { defaultMessage: 'Subscription', id: 'R/6nsx' },
    systemAssigned: { defaultMessage: 'System assigned', id: 'yh4G7g' },
    coreApplicationGroups: { defaultMessage: 'Core application groups', id: 'I8iKnF' },
    coreApplicationGroup: { defaultMessage: 'Core application group', id: 'pf5nil' },
    primaryResourceName: { defaultMessage: 'Primary resource name equals', id: 'G+JlQs' },
    primaryResourceType: { defaultMessage: 'Primary resource type equals', id: 'Wn+PVy' },
    subscriptionEquals: { defaultMessage: 'Subscription equals', id: 'kHIJzq' },
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
    supportedServicesMessage: {
        defaultMessage:
            'To get optimal agent performance for diagnostics, metrics, knowledge, and more, use resource groups that include one or more of these Azure compute services: Azure Kubernetes Service, Functions, Container Apps, or Web Apps.',
        id: '0zGfOk',
    },
    elevatePermissionsMessage: {
        defaultMessage: 'Agent permissions are determined by its assigned RBAC roles.',
        id: 'XYIeUc',
    },
    learnMore: { defaultMessage: 'Learn more', id: 'TdTXXf' },
    startChat: { defaultMessage: 'Start chat', id: 'v8lolG' },
    resourceMap: { defaultMessage: 'Resource map', id: 'aB1tjk' },
    goToMap: { defaultMessage: 'Go to map', id: 'UPybTw' },
    totalResources: { defaultMessage: 'Total resources', id: 'wkLkeE' },
    autonomous: { defaultMessage: 'Autonomous mode', id: 'U73T9e' },
    autonomousWord: { defaultMessage: 'Autonomous', id: 'Sr5R7d' },
    autonomousDescription: { defaultMessage: 'Agent can execute actions automatically without approval', id: 'rjDAQw' },
    review: { defaultMessage: 'Review mode', id: '7Eq7I8' },
    reviewWord: { defaultMessage: 'Review', id: 'R+J5ox' },
    reviewDescription: { defaultMessage: 'Agent can propose actions but requires approval before execution', id: 'rd6YZk' },
    readonly: { defaultMessage: 'Read-only mode', id: 'eLvBmG' },
    readonlyWord: { defaultMessage: 'Read-only', id: 'djNL6D' },
    readonlyDescription: { defaultMessage: 'Agent can only view and analyze information without taking any actions', id: '9/ucRx' },
    agentModeUnknownDescription: { defaultMessage: 'Unknown agent mode', id: 'w9tk1A' },
    enterADescription: { defaultMessage: 'Enter a description', id: 'QAVYIG' },
    dirtyStateConfirmationTitle: {
        defaultMessage: 'Discard changes?',
        id: '41zpq3',
    },
    dirtyStateConfirmationMessage: {
        defaultMessage: 'Any unsaved changes will be lost.',
        id: 'OSMnsR',
    },
    keepWorking: {
        defaultMessage: 'Keep working',
        id: 'YJG5oU',
    },
    moreOptions: {
        defaultMessage: 'More options',
        id: 'IzCVhG',
    },
    threadInfo: {
        defaultMessage: 'Thread info',
        id: 'srG8e2',
    },
    reader: { defaultMessage: 'Reader', id: '3nhWFW' },
    privileged: { defaultMessage: 'Privileged', id: 'TDoBlx' },
    clickToShowValue: { defaultMessage: 'Click to show value', id: 'J/uqp9' },
    userAssigned: { defaultMessage: 'User assigned', id: 'VB97cf' },
    completed: { defaultMessage: 'Completed', id: '95stPq' },
    grantPermissions: { defaultMessage: 'Grant permissions', id: 'u4WKBH' },
    approveAction: { defaultMessage: 'Approve action', id: 'I80vfn' },
    running: { defaultMessage: 'Running', id: 'nDyaq/' },
    timestamps: { defaultMessage: 'Timestamps', id: 'sNutPl' },
    outputLogs: { defaultMessage: 'Output logs', id: 'ayDbML' },
    userPermsPending: { defaultMessage: 'Your permissions will be used temporarily to complete this action.', id: 'a6ZyBy' },
    userPermsRunning: { defaultMessage: 'The action is in progress using temporary permissions granted by {name}.', id: '28HRcH' },
    userPermsCompleted: { defaultMessage: 'The action was completed using temporary permissions granted by {name}.', id: 'WVTzuw' },
    canceledByUser: { defaultMessage: 'The action was canceled by {name}.', id: 'IiNwpr' },
    canceledAction: { defaultMessage: 'The action was canceled.', id: 'X8lXsV' },
    userPermsFailed: { defaultMessage: 'Temporary permissions were granted by {name}, but the action failed to run.', id: 'cubQfQ' },
    agentPermsPending: { defaultMessage: 'Agent permissions will be used to complete this action.', id: 'pSGxDG' },
    agentPermsRunning: { defaultMessage: 'The action is in progress with agent permissions.', id: 'wXKSRT' },
    agentPermsCompleted: { defaultMessage: 'The action was completed using agent permissions.', id: 'a8l/yJ' },
    agentPermsFailed: { defaultMessage: 'Agent permissions were used, but the action failed to run.', id: 'TFQBcG' },
    noPermissionIncidentManagement: {
        defaultMessage: 'You do not have permission to connect/disconnect incident management',
        id: 'PGJzE0',
    },
    noPermissionCheckIncidentManagementConnection: {
        defaultMessage: 'You do not have permission to check incident management connection.',
        id: 'WJj+TP',
    },
    learnMoreAboutPermissions: {
        defaultMessage: 'To learn more about permissions, click here.',
        id: 'Q6Lw7i',
    },
    noPermissionManagedResources: {
        defaultMessage: 'You do not have permission to add or delete managed resources',
        id: 'g+9csc',
    },
    noPermissionDeleteAgent: {
        defaultMessage: 'You do not have permission to delete this agent.',
        id: 'V9A1Xh',
    },
    noPermissionDataConnectors: {
        defaultMessage: 'You do not have permission to add or delete connectors.',
        id: 'pCRg92',
    },
    low: { defaultMessage: 'Low', id: '477I0g' },
    medium: { defaultMessage: 'Medium', id: 'ovJ26C' },
    high: { defaultMessage: 'High', id: 'AxMhQr' },
    created: { defaultMessage: 'Created', id: 'ORGv1Q' },
    modified: { defaultMessage: 'Modified', id: 'tOxzip' },
    source: { defaultMessage: 'Source', id: 'aH4De2' },
    agentId: { defaultMessage: 'Agent ID', id: 'Z7pfsl' },
    threadId: { defaultMessage: 'Thread ID', id: 'ggVnjB' },
    criticalActionsPresent: { defaultMessage: 'Critical actions present', id: 'Zkjd9I' },
    warningActionsPresent: { defaultMessage: 'Warning actions present', id: 'vYmpTr' },
    incident: { defaultMessage: 'Incident', id: 'zaYxwd' },
    idLabel: { defaultMessage: 'ID', id: 'qlcuNQ' },
    started: { defaultMessage: 'Started', id: 'TDUfVk' },
    selected: { defaultMessage: 'Selected', id: 'byP6IC' },
    unselected: { defaultMessage: 'Unselected', id: 'N/CtWu' },
    duration: { defaultMessage: 'Duration', id: 'IuFETn' },
    standardInput: { defaultMessage: 'Standard input', id: 'LyHqqV' },
    error: { defaultMessage: 'Error', id: 'KN7zKn' },
    oboTokenUsed: { defaultMessage: 'An on-behalf-of token will be used with the following scope', id: 'x3aQrV' },
    gotIt: { defaultMessage: 'Got it', id: 'NYTGIb' },
    timeRange: { defaultMessage: 'Time range', id: '74vgSJ' },
    start: { defaultMessage: 'Start', id: 'mOFG3K' },
    startDateAriaLabel: { defaultMessage: 'Start date', id: 'n5QvJy' },
    startDatePickerAriaLabel: { defaultMessage: 'Start date picker', id: 'YBiTCc' },
    startTimeAriaLabel: { defaultMessage: 'Start time', id: '/zFP1/' },
    end: { defaultMessage: 'End', id: '3JVa6k' },
    endDateAriaLabel: { defaultMessage: 'End date', id: 'Humfno' },
    endDatePickerAriaLabel: { defaultMessage: 'End date picker', id: 'Dy3cy0' },
    endTimeAriaLabel: { defaultMessage: 'End time', id: 'yc/tuy' },
    noResults: { defaultMessage: 'No results', id: 'jHJmjf' },
    pillFilterAriaLabel: {
        defaultMessage: 'Editor to filter the results by column value. {columnName}{delimiter} {filterValue}',
        id: '5j5HnL',
    },
    pillFilterRemoveAriaLabel: { defaultMessage: 'Remove {columnName} filter', id: '/ytgXm' },
    optionsListAriaLabel: { defaultMessage: '{fieldName} options', id: 'dDk23i' },
    dateRange: { defaultMessage: 'Date range:', id: 'rNvlCF' },
    dateRange1Day: { defaultMessage: '1 day', id: '+7PjfV' },
    dateRange1Week: { defaultMessage: '1 wk', id: 'CL+NTm' },
    dateRange1Month: { defaultMessage: '1 mo', id: 'zfM/75' },
    youDoNotHaveAccess: { defaultMessage: 'You do not have access', id: 'DnkQsX' },
    missingPermissionForAgent: { defaultMessage: 'You are missing the required permission "{permission}" for this agent.', id: 'ldy3CI' },
    errorDetails: { defaultMessage: 'Error details', id: 'qddSy6' },
    accessHelpInstruction: {
        defaultMessage: 'Copy the error details and send them to your administrator(s) to get access to this page.',
        id: 'NBxjGE',
    },
    copyErrorDetails: { defaultMessage: 'Copy error details', id: 'g/hfuE' },
    detailsResourceName: { defaultMessage: 'Resource name', id: 'eqYdSS' },
    detailsResourceGroupName: { defaultMessage: 'Resource group name', id: 'xVPoso' },
    detailsResourceId: { defaultMessage: 'Resource ID', id: 'iIoj97' },
    detailsPermission: { defaultMessage: 'Permission', id: 'Oz5LRn' },
    detailsAccess: { defaultMessage: 'Details', id: 'Lv0zJu' },
    detailsAccessNoAccess: { defaultMessage: 'No access', id: 'XLRt15' },
    equals: { defaultMessage: 'equals', id: 'Y2QRpS' },
    safe: { defaultMessage: 'Safe', id: 'Fr5LyM' },
    lowRisk: { defaultMessage: 'Low risk', id: 'jd2Xsp' },
    mediumRisk: { defaultMessage: 'Medium risk', id: 'ZwYyES' },
    highRisk: { defaultMessage: 'High risk', id: 'ox7DwN' },
    unknown: { defaultMessage: 'Unknown', id: '5jeq8P' },
    filterBy: { defaultMessage: 'Filter by', id: 'S57QRB' },
    selectFilter: { defaultMessage: 'Select a filter', id: 'iNgloh' },
    addFilter: { defaultMessage: 'Add filter', id: 'M/zZVx' },
    andMoreCount: { defaultMessage: 'and {count} more', id: 'oDI/Rp' },
    takeScreenshot: { defaultMessage: 'Take Screenshot', id: 'KvZ6B9' },
    closePanel: { defaultMessage: 'Close panel', id: 'RAjqKb' },
    collapsePanel: { defaultMessage: 'Collapse panel', id: 'BuziI2' },
    expandPanel: { defaultMessage: 'Expand panel', id: 'Abi/u/' },
    resizeDrawer: { defaultMessage: 'Resize drawer', id: 'Gl8fnJ' },
    selectTask: { defaultMessage: 'Select task', id: '0fhwgp' },
    dismissNotification: { defaultMessage: 'Dismiss notification', id: 'pe7UAe' },
    copyCron: { defaultMessage: 'Copy cron', id: 'dCrF9n' },
    copyRequest: { defaultMessage: 'Copy request', id: 'qdAlfY' },
    showMore: { defaultMessage: 'Show more', id: 'aWpBzj' },
    showLess: { defaultMessage: 'Show less', id: 'qyJtWy' },
    moreItems: { defaultMessage: 'More items', id: '235+J4' },
    scheduledTaskExecutionTitle: { defaultMessage: 'Scheduled Task Execution', id: 'T0/eoE' },
    executionDetailsAndRequest: { defaultMessage: 'Execution details and request', id: '543Ygw' },
    scheduleLabel: { defaultMessage: 'Schedule', id: 'hGQqkW' },
    cronExpressionLabel: { defaultMessage: 'Cron expression', id: 'YmslQP' },
    executionTimeLabel: { defaultMessage: 'Execution Time', id: '8fYK1J' },
    taskDescriptionLabel: { defaultMessage: 'Task Description', id: 'IZYDgM' },
    executionRequestLabel: { defaultMessage: 'Execution Request', id: 'uRYFcI' },
    // Investigation summary / report strings
    investigationErrorLoading: { defaultMessage: 'Error loading investigation', id: '5WWljt' },
    investigationFinalSummaryLabel: { defaultMessage: 'Final Summary:', id: 'JezZlU' },
    investigationStartingHypothesis: { defaultMessage: 'Starting investigation and forming hypothesis', id: 'a8vxDw' },
    investigationResults: { defaultMessage: 'Investigation Results', id: 'HEgmnC' },
    investigationFailedToParse: { defaultMessage: 'Failed to parse investigation data', id: '90THKF' },
    investigationFailedToProcess: { defaultMessage: 'Failed to process investigation message', id: 'gNhYpU' },
    webApps: { defaultMessage: 'Web Apps', id: 'QgFHei' },
    containerApps: { defaultMessage: 'Container Apps', id: '+TlNdm' },
    azureKubernetesServices: { defaultMessage: 'Azure Kubernetes Services', id: '+BcF2d' },
    databases: { defaultMessage: 'Databases', id: 'RYbE1p' },
    noPropertyChangesDetected: { defaultMessage: 'No property changes detected', id: 'VUHkVQ' },
    configurationChanges: { defaultMessage: 'Configuration Changes', id: 'UCYFxr' },
    previous: { defaultMessage: 'Previous', id: 'JJNc3c' },
    current: { defaultMessage: 'Current', id: 'fF376U' },
    correlationIdLabel: { defaultMessage: 'Correlation ID:', id: '6bkNTl' },
    noChangesFoundForCorrelation: { defaultMessage: 'No changes found for this correlation ID', id: 'OsIQyM' },
    correlationAnalysis: { defaultMessage: 'Correlation Analysis', id: 'kn+J82' },
    correlationAnalysisDescription: {
        defaultMessage: 'Highlighted points indicate significant events or anomalies in the data that warrant attention.',
        id: 'PI1EY1',
    },
    legend: { defaultMessage: 'Legend', id: 'iZuO+L' },
    highlightPoint: { defaultMessage: 'Highlight Point', id: '1VB9/h' },
    noMatches: { defaultMessage: 'No matches', id: '96GJ5w' },
    postgreSql: { defaultMessage: 'PostgreSQL', id: 'X5yqtT' },
    postgreSqlCommand: { defaultMessage: 'PostgreSQL Command', id: 'P04Zui' },
    executingEllipsis: { defaultMessage: 'Executing...', id: 'g8ctzH' },
    schedulePreview: { defaultMessage: 'Schedule Preview', id: 'dd6gV2' },
    nextRunsLocalTime: { defaultMessage: 'Next runs (local time):', id: 'lcOT8X' },
    never: { defaultMessage: 'Never', id: 'du1laW' },
    notScheduled: { defaultMessage: 'Not scheduled', id: 'pyXjlj' },
    scheduled: { defaultMessage: 'Scheduled', id: 'cXAlMR' },
    loadingScheduledTasks: { defaultMessage: 'Loading scheduled tasks...', id: 'iyOYgd' },
    createFirstScheduledTask: {
        defaultMessage: 'Create your first scheduled task to automatically run agent actions at regular intervals.',
        id: '4l/H3O',
    },
    unsupportedChartType: { defaultMessage: 'Unsupported chart type: {type}', id: 'bswtOq' },
    infoLabel: { defaultMessage: 'Info:', id: 't071V3' },
    correlationRangeHelp: {
        defaultMessage: 'Correlation ranges from -1 (inverse) to 1 (direct)',
        id: 'fk/+VC',
    },
    unexpectedErrorOccurred: { defaultMessage: 'An unexpected error occurred', id: '3IKub9' },
    getSupport: { defaultMessage: 'Get support', id: 'Km7x3v' },
    resourceId: { defaultMessage: 'Resource ID', id: 'iIoj97' },
    sessionId: { defaultMessage: 'Session ID', id: 'b0v+Pu' },
    correlationNoteHighlightedPoints: {
        defaultMessage:
            'Note: There {count, plural, one {is # highlighted point} other {are # highlighted points}} in this chart that may require attention.',
        id: 'gwSeXa',
    },
    correlationLabel: { defaultMessage: 'Correlation:', id: 'Q9Zp/n' },
    totalLabel: { defaultMessage: 'Total:', id: 'q4EmsW' },
    dataPointsLabel: { defaultMessage: 'Data Points', id: 'MydQzO' },
    highlightedPointFallback: { defaultMessage: 'Highlighted Point', id: 'XeBq0b' },
    correlationRelationshipDescription: {
        defaultMessage:
            'This chart shows the relationship between {y1} and {y2} over time. The correlation values indicate how strongly these two metrics influence each other.',
        id: 'rQAyD6',
    },
    assigned: { defaultMessage: 'Assigned', id: 'iZDRGO' },
    selectAllRowsAriaLabel: { defaultMessage: 'Select all rows', id: '8BaLs0' },
    selectRowAriaLabel: { defaultMessage: 'Select row', id: '4pJVaS' },
    lineChart: { defaultMessage: 'Line chart', id: '8oyl6b' },
    barChart: { defaultMessage: 'Bar chart', id: 'k+3+Dy' },
    loadingMoreRows: { defaultMessage: 'Loading more rows...', id: 'PSfARI' },
    warning: { defaultMessage: 'Warning', id: '3SVI5p' },
    undo: { defaultMessage: 'Undo', id: 'JkS37H' },
    removeItemWithName: { defaultMessage: 'Remove {name}', id: 'T0e6Lh' },
    // Session Insights
    sessionInsight: { defaultMessage: 'Session Insight', id: 'DYp/nK' },
    clickToViewSessionAnalysis: { defaultMessage: 'Click to view session analysis and insights', id: 'ttWUo6' },
    timeline: { defaultMessage: 'Timeline', id: 'zWkvNO' },
    agentPerformance: { defaultMessage: 'Agent Performance', id: 'urxxsP' },
    noInsightContentAvailable: { defaultMessage: 'No insight content available', id: 'cHKA8f' },
    insightNoMarkdownContent: { defaultMessage: 'This insight does not have markdown content.', id: 'TexFnY' },
    noSessionInsightsFound: { defaultMessage: 'No session insights found.', id: 'fuHqFL' },
    noInsightsAvailable: { defaultMessage: 'No insights available', id: 'trhBLG' },
    insightsNotGenerated: { defaultMessage: 'Insights have not been generated for this thread yet.', id: '/3OCn6' },
    noInsightSelected: { defaultMessage: 'No insight selected', id: 'A9cgAa' },
    threadsWithInsightsCount: { defaultMessage: 'Threads with Insights ({count})', id: 'SIW6OB' },
    noThreadsWithInsights: { defaultMessage: 'No threads with session insights found.', id: 'V9GGMu' },
    insightWasHelpful: { defaultMessage: 'This insight was helpful', id: 'BD+V8L' },
    insightNeedsImprovement: { defaultMessage: 'This insight needs improvement', id: '+XtuWl' },
    // Feedback
    threadFeedback: { defaultMessage: 'Thread Feedback', id: 'bICPAQ' },
    noFeedbackYet: { defaultMessage: 'No Feedback Yet', id: 'R4Blzb' },
    feedbackWillAppearHere: { defaultMessage: 'Feedback submitted on threads will appear here', id: 'GGr/yS' },
    noFeedbackSelected: { defaultMessage: 'No feedback selected', id: 'cesI6Z' },
    feedbackId: { defaultMessage: 'Feedback ID', id: 'T/nqbo' },
    submitted: { defaultMessage: 'Submitted', id: 'raexxM' },
    rating: { defaultMessage: 'Rating', id: 'ETRyBL' },
    noAdditionalComments: { defaultMessage: 'No additional comments provided', id: 'wBh+z3' },
    doYouWantToProceed: { defaultMessage: 'Do you want to proceed?', id: 'j0Ifob' },
    noIncidentMetricsToReport: { defaultMessage: 'No incident metrics to report', id: 'j0bMX6' },
    trySelectingADifferentDateRange: { defaultMessage: 'Try selecting a different date range.', id: 'jJ7kNS' },
    thisFeatureIsntAvailableInThisPortalYet: { defaultMessage: "This feature isn't available in this portal yet.", id: 'dQJz2m' },
    openInAzurePortal: { defaultMessage: 'Open in Azure Portal', id: '5NZIbS' },
    unknownStatus: { defaultMessage: 'Unknown status', id: 'wSLRfB' },
    sreAgentStoppedTitle: { defaultMessage: 'The agent is stopped', id: 'i6x7bU' },
    sreAgentStoppedDescription: {
        defaultMessage: 'When an agent is stopped, it cannot monitor or interact, but it still incurs a fixed cost.',
        id: '9jqvi3',
    },
    refineWithAi: { defaultMessage: 'Refine with AI', id: 'uwY8Nf' },
    refining: { defaultMessage: 'Refining...', id: 'ueVpay' },
    refineWithAiTooltip: { defaultMessage: 'Use AI to enhance and validate your task instructions', id: '17sQau' },
});

export const SreAgentTabResources = defineMessages({
    activities: { defaultMessage: 'Activities', id: 'UmEsZF' },
    settings: { defaultMessage: 'Settings', id: 'D3idYv' },
    resourceMapping: { defaultMessage: 'Resource mapping', id: 'TdeXH0' },
    incidentHandlers: { defaultMessage: 'Incident response plans', id: 'mV7WX3' },
    dailyReports: { defaultMessage: 'Daily reports', id: 'Z6rVz/' },
    logs: { defaultMessage: 'Logs', id: 'SNuQo7' },
    feedback: { defaultMessage: 'Give us feedback', id: 'aQPexO' },
    incidentManagement: { defaultMessage: 'Incident management', id: 'T7WpWs' },
    sessionInsights: { defaultMessage: 'Session Insights', id: '9WBhPZ' },
    scheduledTasks: { defaultMessage: 'Scheduled tasks', id: 'sy7vzf' },
});

export const ResourcePickerTabResources = defineMessages({
    selectTabTitle: { defaultMessage: 'Choose resource groups', id: 'zcyAwy' },
    reviewTabTitle: { defaultMessage: 'Review resource groups', id: 'LPprqA' },
    assignTabTitle: { defaultMessage: 'Roles and permissions', id: 'OVAsQ8' },
    reader: { defaultMessage: 'Reader', id: '3nhWFW' },
    containerAppsOperator: { defaultMessage: 'Container Apps Operator', id: '/WrP/v' },
    monitoringReader: { defaultMessage: 'Monitoring Reader', id: 'Sr4IbA' },
    logAnalyticsReader: { defaultMessage: 'Log Analytics Reader', id: 'sI+CCC' },
    kubernetesReader: { defaultMessage: 'Azure Kubernetes Service RBAC Reader', id: 'RrsyUh' },
    websiteContributor: { defaultMessage: 'Website Contributor/Operator', id: 'UV4Dx5' },
    writerOperator: { defaultMessage: 'Writer/Operator', id: 'oUbzA/' },
    permissionsForRoleAssignment: { defaultMessage: 'User Permissions', id: 'F4a/f4' },
    resourceGroupPermissionError: {
        defaultMessage:
            'Some of the selected resource groups do not have the required roleAssignments/write and Microsoft.ManagedIdentity/userAssignedIdentities/write permissions.',
        id: 'E60v6W',
    },
    resourceGroupMaxError: {
        defaultMessage:
            "You can select up to {max} resource groups for this agent. Agents can manage a total of {totalMax} resource groups, and your agent currently manages {current}. You've selected {count}.",
        id: 'HtVpjg',
    },
    failedToLoadResourceGroups: { defaultMessage: 'Failed to load resource groups.', id: 'anSi7M' },
    showRecommended: { defaultMessage: 'Show only recommended resource groups', id: 'ATYp8z' },
    resourceGroupMinMax: {
        defaultMessage:
            'You can manage up to {max} resource groups you have permissions on. Your agent currently manages {count} resource groups.',
        id: '3rvQhx',
    },
    resourceGroupSelected: {
        defaultMessage: '{count} resource group selected.',
        id: '13KTIN',
    },
    resourceGroupsSelected: {
        defaultMessage: '{count} resource groups selected.',
        id: 'G4mray',
    },
    recommendedResourceGroupTooltip: {
        defaultMessage:
            'This resource group is recommended for optimal agent performance. It includes one or more of these Azure compute services: Azure Kubernetes Service, Functions, Container Apps, Web Apps, Redis, Postgres SQL, CosmosDB, Virtual machines, or Storage accounts.',
        id: 'DasCIQ',
    },
    reviewTabDescription: {
        defaultMessage:
            'To assign the agent managed resource groups across subscriptions, you need Owner or User Access Administrator permissions on those resource groups. The agent resource and managed resource groups can be in different regions and subscriptions.',
        id: 'sRU00i',
    },
    readOnlyLock: { defaultMessage: 'Read-only lock', id: '6yuD0y' },
    denyAssignment: { defaultMessage: 'Deny assignment', id: 'ob6s4+' },
});

// Graph viewer (mermaid visualization & psql execution message) tooltips / labels
export const GraphViewerResources = defineMessages({
    fullscreen: { defaultMessage: 'Fullscreen', id: 'zvKOAu' },
    resetView: { defaultMessage: 'Reset View', id: '7M8VZy' },
    zoomOut: { defaultMessage: 'Zoom Out', id: 'uln7eT' },
    zoomIn: { defaultMessage: 'Zoom In', id: 'KQ9L9d' },
    downloadSvg: { defaultMessage: 'Download SVG', id: 'MI5gZ+' },
    downloadPng: { defaultMessage: 'Download PNG', id: 'EAO/K+' },
    clickToOpenFullscreen: { defaultMessage: 'Click to open fullscreen', id: 'Slnpm8' },
    copyCommand: { defaultMessage: 'Copy command', id: 'ifcOhH' },
    copyOutput: { defaultMessage: 'Copy output', id: '2os8R1' },
    tipLabel: { defaultMessage: 'Tip:', id: 'JT7be1' },
    tipInstructions: {
        defaultMessage: 'Click and drag to move • Scroll or use buttons to zoom • Press ESC to close',
        id: '1Pgd3P',
    },
});

export const ChangeDiffResources = defineMessages({
    changeDiffTitle: { defaultMessage: 'Change Diff', id: 'HtpD+z' },
});

export const ExecutionOutputResources = defineMessages({
    outputAndErrorAvailable: { defaultMessage: 'Output and error available', id: 'wuk3yS' },
    outputAvailable: { defaultMessage: 'Output available', id: 'Ck8Kax' },
    errorAvailable: { defaultMessage: 'Error available', id: 'vdazhF' },
});

export const PromptResources = defineMessages({
    myRecentPrompts: { defaultMessage: 'My recent prompts', id: 'PCrXDG' },
    suggestedPrompts: { defaultMessage: 'Suggested prompts', id: 'cn2YEB' },
    promptLibrary: { defaultMessage: 'Prompt library', id: 'zvLfRe' },
    bestPracticesPrompt: { defaultMessage: 'Can you audit best practices for my resource?', id: '4OUjTL' },
    notWorkingPrompt: { defaultMessage: "Why isn't my application working?", id: 'DlSXUR' },
    availabilityPrompt: { defaultMessage: "Can you analyze my resource's availability over the last 24 hours?", id: 'EI4WZI' },
    promptExamples: { defaultMessage: 'Prompt examples', id: 'LRJHcs' },
});

export const AgentTaskResources = defineMessages({
    deepInvestigation: { defaultMessage: 'Deep investigation', id: '2a+ttj' },
    deepInvestigationTurnedOnMessage: {
        defaultMessage: 'Deep investigation is turned on',
        id: 'cDjhgm',
    },
    deepInvestigationTurnedOffMessage: {
        defaultMessage: 'Deep investigation is turned off',
        id: 'QfEnqG',
    },
    deepInvestigationTooltip: {
        defaultMessage: 'View decision tree of potential root causes.',
        id: 'UpTFVv',
    },
    deepInvestigationNoPermissionTurnedOnMessage: {
        defaultMessage: 'You do not have permission to turn Deep Investigations off.',
        id: 'HSZaZc',
    },
    deepInvestigationNoPermissionTurnedOffMessage: {
        defaultMessage: 'You do not have permission to turn Deep Investigations on.',
        id: 'Ysew65',
    },
    conclusionNodeText: {
        defaultMessage: 'Conclusion',
        id: 'ZjlBPk',
    },
    deepInvestigationDescription: {
        defaultMessage: 'The agent investigates complex issues and forms hypotheses to validate potential root causes.',
        id: '1m4VPF',
    },
    deepInvestigationWarning: {
        defaultMessage: 'Deep investigations require significant time to run and might result in high AAU consumption.',
        id: 'vb6deF',
    },
    deepInvestigationDismissCheckboxLabel: {
        defaultMessage: "Don't show this message again",
        id: 'Vaj9nj',
    },
    learnMoreLinkText: {
        defaultMessage: 'Learn more about deep investigations',
        id: 'yRKsn1',
    },
    consumptionReminder: {
        defaultMessage: 'AAU consumption reminder',
        id: 'SeZ8eZ',
    },
    usageLearnMoreLinkText: {
        defaultMessage: 'Learn more about AAU consumption and cost',
        id: 'hGnjdE',
    },
});

export const ToDoPlanResources = defineMessages({
    noTodoPlanAvailable: {
        defaultMessage: 'No todo plan available',
        id: 'bri0SI',
    },
    todoPlanProgress: {
        defaultMessage: '{completed} of {total} completed',
        id: '9sDoY5',
    },
    todoPlanText: {
        defaultMessage: 'To-Do Plan',
        id: 'icyjmd',
    },
    todoPlanCloseTooltip: {
        defaultMessage: 'Close Todo Plans',
        id: '8wQLC0',
    },
    todoPlanOpenTooltip: {
        defaultMessage: 'Open Todo Plans',
        id: 'eCZ0j1',
    },
});

export const PermissionsResources = defineMessages({
    resourceGroupInfoBar: {
        defaultMessage:
            'It may take up to an hour for your agent to reflect these changes as it needs time to build the corresponding knowledge.',
        id: 'znSLJS',
    },
    roles: { defaultMessage: 'Roles', id: 'c35gM5' },
    role: { defaultMessage: 'Role', id: '1ZgrhW' },
    description: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    readOnlyModeDescription: {
        defaultMessage:
            'Your agent is in read-only mode. The following permissions are required for the agent to build knowledge about your resources. The contributor roles for specific services will not be used to perform any write operations.',
        id: 'P9odAr',
    },
    reviewModeDescription: {
        defaultMessage:
            'Your agent is in review mode. The following permissions are required for the agent to build knowledge about your resources. The agent will use some of these permissions to perform write operations with your approval based on your requests.',
        id: 'p/xAmY',
    },
    autonomousModeDescription: {
        defaultMessage:
            'Your agent is in autonomous mode. The following permissions are required for the agent to build knowledge about your resources. The agent will use some of these permissions to perform write operations autonomously based on your requests.',
        id: '/dxZCE',
    },
    rolesAndPermissionsDescription: { defaultMessage: 'The resources you picked have permissions for these roles.', id: 'OTGHHm' },
    noRolesAndPermissions: {
        defaultMessage: 'To view the roles and permissions the agent will have, choose resource groups for the agent to manage.',
        id: 'Qly58H',
    },
    contributor: { defaultMessage: 'Contributor', id: '+k5t/y' },
    contributorDescription: {
        defaultMessage:
            'Grants full access to manage all resources, but does not allow you to assign roles in Azure RBAC, manage assignments in Azure Blueprints, or share image galleries.',
        id: 'KzMCPo',
    },
    monitoringContributorDescription: { defaultMessage: 'Can read all monitoring data and update monitoring settings.', id: 'PrFU82' },
    containerAppsContributor: { defaultMessage: 'Container Apps Contributor', id: 'i5OUQE' },
    containerAppsContributorDescription: {
        defaultMessage: 'Full management of Container Apps, including creation, deletion, and updates.',
        id: 'Mnvpbx',
    },
    logAnalyticsReader: { defaultMessage: 'Log Analytics Reader', id: 'sI+CCC' },
    logAnalyticsReaderDescription: {
        defaultMessage:
            'View and search all monitoring data as well as view monitoring settings, including viewing the configuration of Azure diagnostics on all Azure resources.',
        id: 'Oom2N7',
    },
    websitesContributor: { defaultMessage: 'Website Contributor', id: 'xtkizV' },
    websitesContributorDescription: {
        defaultMessage: 'Manage websites, but not web plans. Does not allow you to assign roles.',
        id: '3zLsNR',
    },
    webPlanContributor: { defaultMessage: 'Web Plan Contributor', id: '1YPTmM' },
    webPlanContributorDescription: { defaultMessage: 'Lets you manage the web plans for websites, but not access to them.', id: 'jJE3xY' },
    reader: { defaultMessage: 'Reader', id: '3nhWFW' },
    readerDescription: { defaultMessage: 'View all resources, but does not allow you to make any changes.', id: 'LSaaxU' },
    containerAppsOperator: { defaultMessage: 'Container Apps Operator', id: '/WrP/v' },
    containerAppsOperatorDescription: { defaultMessage: 'Read, logstream and exec into Container Apps.', id: 'gn/nvJ' },
    azureKubernetesServiceRbacReader: { defaultMessage: 'Azure Kubernetes Service RBAC Reader', id: 'RrsyUh' },
    azureKubernetesServiceRbacReaderDescription: {
        defaultMessage:
            'Allows read-only access to see most objects in a namespace. It does not allow viewing roles or role bindings. This role does not allow viewing Secrets, since reading the contents of Secrets enables access to ServiceAccount credentials in the namespace, which would allow API access as any ServiceAccount in the namespace (a form of privilege escalation). Applying this role at cluster scope will give access across all namespaces.',
        id: 'gC4xMY',
    },
    azureKubernetesServiceClusterUserRole: { defaultMessage: 'Azure Kubernetes Service Cluster User Role', id: '5V+Txp' },
    azureKubernetesServiceClusterUserRoleDescription: { defaultMessage: 'List cluster user credential action.', id: 'Bcx/Gz' },
    azureKubernetesServiceClusterAdmin: { defaultMessage: 'Azure Kubernetes Service Cluster Admin', id: 'RStTJp' },
    azureKubernetesServiceClusterAdminDescription: { defaultMessage: 'List cluster admin credential action.', id: '4kCXiz' },
    azureKubernetesServiceRbacClusterAdmin: { defaultMessage: 'Azure Kubernetes Service RBAC Cluster Admin', id: 'YZIFAO' },
    azureKubernetesServiceRbacClusterAdminDescription: { defaultMessage: 'Lets you manage all resources in the cluster.', id: 'nBPow3' },
    azureMonitorMonitoringContributor: { defaultMessage: 'Monitoring Contributor', id: 'TAm7bB' },
    azureMonitorMonitoringContributorDescription: {
        defaultMessage: 'Read and write Azure Monitor monitoring settings and data.',
        id: 'mkhNnB',
    },
    applicationInsightsComponentContributor: { defaultMessage: 'Application Insights Component Contributor', id: 'VlBNux' },
    applicationInsightsComponentContributorDescription: { defaultMessage: 'Manage Application Insights components.', id: 'pqgzm0' },
    logAnalyticsContributor: { defaultMessage: 'Log Analytics Contributor', id: 'e0a3XV' },
    logAnalyticsContributorDescription: { defaultMessage: 'Read all monitoring data and edit monitoring settings.', id: '6zUWZ/' },
    postgreSqlContributor: { defaultMessage: 'PostgreSQL Contributor', id: 'gzx/k1' },
    postgreSqlContributorDescription: { defaultMessage: 'TODO', id: 'RVf7gC' },
    redisCacheContributor: { defaultMessage: 'Redis Cache Contributor', id: '8nBFW4' },
    redisCacheContributorDescription: { defaultMessage: 'Manage without access.', id: 'hO1Iog' },
    sqlDbContributor: { defaultMessage: 'SQL DB Contributor', id: 'hfIdUs' },
    sqlDbContributorDescription: {
        defaultMessage: "Manage without access. Can't manage security policies or parent SQL servers.",
        id: 'p5T+zv',
    },
    storageBlobDataContributor: { defaultMessage: 'Storage Blob Data Contributor', id: 'ND2EUR' },
    storageBlobDataContributorDescription: { defaultMessage: 'Read, write, and delete Azure Storage containers and blobs.', id: 'uRMqBi' },
    documentDbAccountContributor: { defaultMessage: 'DocumentDB Account Contributor', id: 'ABo5XY' },
    documentDbAccountContributorDescription: { defaultMessage: 'Can read Azure Cosmos DB account data.', id: 'vfndow' },
    storageBlobDataReader: { defaultMessage: 'Storage Blob Data Reader', id: 'Q3p3sz' },
    storageBlobDataReaderDescription: { defaultMessage: 'Allows for read access to Azure Storage blob containers and data.', id: '4ii5zj' },
    monitoringReader: { defaultMessage: 'Monitoring Reader', id: 'Sr4IbA' },
    monitoringReaderDescription: { defaultMessage: 'Can read all monitoring data.', id: 'xdMkcg' },
    storageAccountContributor: { defaultMessage: 'Storage Account Contributor', id: 'cTzjJg' },
    storageAccountContributorDescription: {
        defaultMessage:
            'Lets you manage storage accounts, including accessing storage account keys which provide full access to storage account data.',
        id: 'xXhn+h',
    },
    virtualMachineContributor: { defaultMessage: 'Virtual Machine Contributor', id: 'PXg8+x' },
    virtualMachineContributorDescription: {
        defaultMessage:
            'Lets you manage virtual machines, but not access to them, and not the virtual network or storage account they are connected to.',
        id: 'AeWmvh',
    },
    postgreSqlFlexibleServerLongTermRetentionBackupRole: {
        defaultMessage: 'PostgreSQL Flexible Server Long Term Retention Backup Role',
        id: 'WLw9E7',
    },
    postgreSqlFlexibleServerLongTermRetentionBackupRoleDescription: {
        defaultMessage: 'Manage long-term retention backups for PostgreSQL Flexible Server.',
        id: 'XtnlO8',
    },
    sqlManagedInstanceContributor: { defaultMessage: 'SQL Managed Instance Contributor', id: 'SuVToZ' },
    sqlManagedInstanceContributorDescription: { defaultMessage: 'Manage SQL Managed Instances.', id: '/8MshI' },
    sqlServerContributor: { defaultMessage: 'SQL Server Contributor', id: 'ss/NWY' },
    sqlServerContributorDescription: { defaultMessage: 'Manage SQL servers and their security policies.', id: '0Dhe75' },
    dataFactoryContributor: { defaultMessage: 'Data Factory Contributor', id: 'Wtf3vL' },
    dataFactoryContributorDescription: { defaultMessage: 'Manage Azure Data Factory instances.', id: '0te2Ra' },
    hdInsightOnAksClusterAdmin: { defaultMessage: 'HDInsight on AKS Cluster Admin', id: 'NcxGiD' },
    hdInsightOnAksClusterAdminDescription: { defaultMessage: 'Administer HDInsight clusters on AKS.', id: 'ixb1SP' },
    hdInsightOnAksClusterPoolAdmin: { defaultMessage: 'HDInsight on AKS Cluster Pool Admin', id: 'pAFuFx' },
    hdInsightOnAksClusterPoolAdminDescription: { defaultMessage: 'Administer HDInsight cluster pools.', id: 'WygVDv' },
    azureMlComputeOperator: { defaultMessage: 'AzureML Compute Operator', id: 'Wc5b/J' },
    azureMlComputeOperatorDescription: { defaultMessage: 'Operate Azure Machine Learning compute resources.', id: 'ciB+oy' },
    azureMlDataScientist: { defaultMessage: 'AzureML Data Scientist', id: 'l5sNim' },
    azureMlDataScientistDescription: { defaultMessage: 'Perform data science tasks in Azure Machine Learning.', id: 'i6cLLM' },
    cognitiveServicesContributor: { defaultMessage: 'Cognitive Services Contributor', id: 'Dff3ms' },
    cognitiveServicesContributorDescription: { defaultMessage: 'Manage Cognitive Services accounts.', id: 'wxlJSs' },
    cognitiveServicesOpenAiContributor: { defaultMessage: 'Cognitive Services OpenAI Contributor', id: 'HQhDfV' },
    cognitiveServicesOpenAiContributorDescription: { defaultMessage: 'Manage Azure OpenAI resources.', id: 'ZsLOg3' },
    cognitiveServicesCustomVisionContributor: { defaultMessage: 'Cognitive Services Custom Vision Contributor', id: '7+b0J0' },
    cognitiveServicesCustomVisionContributorDescription: { defaultMessage: 'Manage Custom Vision projects.', id: '/gCtdO' },
    cognitiveServicesLanguageWriter: { defaultMessage: 'Cognitive Services Language Writer', id: 'aqNZbD' },
    cognitiveServicesLanguageWriterDescription: { defaultMessage: 'Write access to Language service resources.', id: 'CMaDRa' },
    cognitiveServicesLuisWriter: { defaultMessage: 'Cognitive Services LUIS Writer', id: '64zSh3' },
    cognitiveServicesLuisWriterDescription: { defaultMessage: 'Write access to LUIS applications.', id: 'cRTR/A' },
    cognitiveServicesQnaMakerEditor: { defaultMessage: 'Cognitive Services QnA Maker Editor', id: 'rjgOC5' },
    cognitiveServicesQnaMakerEditorDescription: { defaultMessage: 'Edit QnA Maker knowledge bases.', id: 'mAyw3x' },
    cognitiveServicesSpeechContributor: { defaultMessage: 'Cognitive Services Speech Contributor', id: 'fFyuVF' },
    cognitiveServicesSpeechContributorDescription: { defaultMessage: 'Contribute to Speech service resources.', id: '6YXj/s' },
    healthcareAgentEditor: { defaultMessage: 'Healthcare Agent Editor', id: 't9I1dY' },
    healthcareAgentEditorDescription: { defaultMessage: 'Edit Healthcare APIs agent configurations.', id: 'Phji5P' },
    searchServiceContributor: { defaultMessage: 'Search Service Contributor', id: 'P3qxV2' },
    searchServiceContributorDescription: { defaultMessage: 'Manage Azure Search services.', id: 'ecG+OD' },
    azureDigitalTwinsDataOwner: { defaultMessage: 'Azure Digital Twins Data Owner', id: 'QIY3dQ' },
    azureDigitalTwinsDataOwnerDescription: { defaultMessage: 'Full access to Azure Digital Twins data.', id: '87v+WY' },
    deviceProvisioningServiceDataContributor: { defaultMessage: 'Device Provisioning Service Data Contributor', id: '5UfRjw' },
    deviceProvisioningServiceDataContributorDescription: { defaultMessage: 'Manage Device Provisioning Service data.', id: '7N87nD' },
    deviceUpdateAdministrator: { defaultMessage: 'Device Update Administrator', id: 'e3itcX' },
    deviceUpdateAdministratorDescription: { defaultMessage: 'Administer Device Update accounts.', id: 'hF5T80' },
    iotHubDataContributor: { defaultMessage: 'IoT Hub Data Contributor', id: 'xqwf/7' },
    iotHubDataContributorDescription: { defaultMessage: 'Full access to IoT Hub data.', id: 'R1RX2a' },
    iotHubRegistryContributor: { defaultMessage: 'IoT Hub Registry Contributor', id: 'lvNWLS' },
    iotHubRegistryContributorDescription: { defaultMessage: 'Manage IoT Hub device registry.', id: '2PnnDB' },
    iotHubTwinContributor: { defaultMessage: 'IoT Hub Twin Contributor', id: '5Sc5rT' },
    iotHubTwinContributorDescription: { defaultMessage: 'Manage IoT Hub device twins.', id: 'gZ5fZs' },
    apiManagementServiceContributor: { defaultMessage: 'API Management Service Contributor', id: 'rZaqtB' },
    apiManagementServiceContributorDescription: { defaultMessage: 'Manage API Management services.', id: 'dR8VUW' },
    apiManagementServiceOperatorRole: { defaultMessage: 'API Management Service Operator Role', id: 'yo67XU' },
    apiManagementServiceOperatorRoleDescription: { defaultMessage: 'Operate API Management services.', id: 'xmsrBu' },
    apiManagementWorkspaceContributor: { defaultMessage: 'API Management Workspace Contributor', id: 'EdEthU' },
    apiManagementWorkspaceContributorDescription: { defaultMessage: 'Manage API Management workspaces.', id: 'd2GmfJ' },
    appConfigurationContributor: { defaultMessage: 'App Configuration Contributor', id: 'IEFW3Y' },
    appConfigurationContributorDescription: { defaultMessage: 'Manage App Configuration stores.', id: '2LQx4N' },
    azureServiceBusDataOwner: { defaultMessage: 'Azure Service Bus Data Owner', id: 'H3ooTI' },
    azureServiceBusDataOwnerDescription: { defaultMessage: 'Full access to Azure Service Bus data.', id: 'nrAnZy' },
    logicAppContributor: { defaultMessage: 'Logic App Contributor', id: 'C3PGqj' },
    logicAppContributorDescription: { defaultMessage: 'Manage Logic Apps.', id: 'ZNMCbm' },
    workbookContributor: { defaultMessage: 'Workbook Contributor', id: '4MeTpL' },
    workbookContributorDescription: { defaultMessage: 'Manage Azure Monitor workbooks.', id: 'Z7Aj91' },
    azureCenterForSapSolutionsAdministrator: { defaultMessage: 'Azure Center for SAP solutions administrator', id: 'Yp7NCi' },
    azureCenterForSapSolutionsAdministratorDescription: { defaultMessage: 'Administer SAP workloads on Azure.', id: 'wbigtI' },
    costManagementContributor: { defaultMessage: 'Cost Management Contributor', id: 'oQ/TUW' },
    costManagementContributorDescription: { defaultMessage: 'Manage cost analysis and budgets.', id: 'sRb5P9' },
    hdInsightClusterOperator: { defaultMessage: 'HDInsight Cluster Operator', id: 'xGPsyJ' },
    hdInsightClusterOperatorDescription: { defaultMessage: 'Lets you read and modify HDInsight cluster configurations.', id: 'XJ+Lww' },
    cognitiveServicesCustomVisionReader: { defaultMessage: 'Cognitive Services Custom Vision Reader', id: 'u0xiAh' },
    cognitiveServicesCustomVisionReaderDescription: {
        defaultMessage: "Read-only actions in the project. Readers can't create or update the project.",
        id: 'eBvTo6',
    },
    cognitiveServicesDataReader: { defaultMessage: 'Cognitive Services Data Reader', id: 'SCjiXP' },
    cognitiveServicesDataReaderDescription: { defaultMessage: 'Lets you read Cognitive Services data.', id: 'IQaKwH' },
    cognitiveServicesLanguageReader: { defaultMessage: 'Cognitive Services Language Reader', id: '8NHZQV' },
    cognitiveServicesLanguageReaderDescription: {
        defaultMessage: 'Has access to Read and Test functions under Language portal.',
        id: 'vbdOl/',
    },
    cognitiveServicesLuisReader: { defaultMessage: 'Cognitive Services LUIS Reader', id: 'Pwen/L' },
    cognitiveServicesLuisReaderDescription: { defaultMessage: 'Has access to Read and Test functions under LUIS.', id: 'aGL+Qo' },
    cognitiveServicesQnaMakerReader: { defaultMessage: 'Cognitive Services QnA Maker Reader', id: 'Yk6Co7' },
    cognitiveServicesQnaMakerReaderDescription: { defaultMessage: "Let's you read and test a KB only.", id: 'Dx3Xj3' },
    cognitiveServicesUsagesReader: { defaultMessage: 'Cognitive Services Usages Reader', id: 'c1EiBR' },
    cognitiveServicesUsagesReaderDescription: { defaultMessage: 'Minimal permission to view Cognitive Services usages.', id: 'GoSIa4' },
    searchIndexDataReader: { defaultMessage: 'Search Index Data Reader', id: 'j5Yfyr' },
    searchIndexDataReaderDescription: { defaultMessage: 'Grants read access to Azure Cognitive Search index data.', id: 'pSfQh6' },
    azureDigitalTwinsDataReader: { defaultMessage: 'Azure Digital Twins Data Reader', id: 'hrdmcd' },
    azureDigitalTwinsDataReaderDescription: { defaultMessage: 'Read-only role for Digital Twins data-plane properties.', id: 'CFJ1OD' },
    deviceProvisioningServiceDataReader: { defaultMessage: 'Device Provisioning Service Data Reader', id: 'bN0x+w' },
    deviceProvisioningServiceDataReaderDescription: {
        defaultMessage: 'Allows for full read access to Device Provisioning Service data-plane properties.',
        id: 'dQ5/dh',
    },
    deviceUpdateReader: { defaultMessage: 'Device Update Reader', id: 't3D4PH' },
    deviceUpdateReaderDescription: {
        defaultMessage: 'Gives you read access to management and content operations, but does not allow making changes.',
        id: '+/nU/s',
    },
    iotHubDataReader: { defaultMessage: 'IoT Hub Data Reader', id: 'd8dECB' },
    iotHubDataReaderDescription: { defaultMessage: 'Allows for full read access to IoT Hub data-plane properties.', id: 'KAYpG9' },
    apiManagementServiceReader: { defaultMessage: 'API Management Service Reader Role', id: '4TnjxL' },
    apiManagementServiceReaderDescription: { defaultMessage: 'Read-only access to service and APIs.', id: '8aHHLj' },
    apiManagementWorkspaceReader: { defaultMessage: 'API Management Workspace Reader', id: 'XYSJsS' },
    apiManagementWorkspaceReaderDescription: {
        defaultMessage: 'Has read-only access to entities in the workspace. This role should be assigned on the workspace scope.',
        id: 'UZS12+',
    },
    appConfigurationReader: { defaultMessage: 'App Configuration Reader', id: 'pDUFq5' },
    appConfigurationReaderDescription: {
        defaultMessage: 'Grants permission for read operations for App Configuration resources.',
        id: 'ENnkRe',
    },
    logicAppOperator: { defaultMessage: 'Logic App Operator', id: 'T5+AEf' },
    logicAppOperatorDescription: { defaultMessage: 'Lets you read, enable and disable logic app.', id: '01lT1B' },
    workbookReader: { defaultMessage: 'Workbook Reader', id: 'hPj49L' },
    workbookReaderDescription: { defaultMessage: 'Can read workbooks.', id: '+l+XEu' },
    azureCenterForSapSolutionsReader: { defaultMessage: 'Azure Center for SAP solutions reader', id: 'BKSv0l' },
    azureCenterForSapSolutionsReaderDescription: {
        defaultMessage: 'This role provides read access to all capabilities of Azure Center for SAP solutions.',
        id: 'NAAdaF',
    },
    costManagementReader: { defaultMessage: 'Cost Management Reader', id: '5wGcN7' },
    costManagementReaderDescription: { defaultMessage: 'Can view cost data and configuration (e.g. budgets, exports).', id: '3t/IhV' },
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
        defaultMessage:
            'To help monitor and respond to activities and incidents in your Azure resources, the agent needs access to the specific subscriptions, resources groups, and services that you allow.',
        id: 'oH9YKC',
    },
    openAccessControl: { defaultMessage: 'Go to Access control', id: 'fUgypu' },
});

export const SupportResources = defineMessages({
    description: {
        defaultMessage: 'Visit Support + Troubleshooting to access support resources and troubleshoot issues related to your SRE Agent.',
        id: 'ghwElt',
    },
    buttonText: { defaultMessage: 'Go to Support + Troubleshooting', id: '2l5w5P' },
});

export const RbacWarningBannerResources = defineMessages({
    rbacWarningMessage: {
        defaultMessage:
            'Users without one of the required Azure role-based access control (RBAC) roles will not have access to Azure SRE Agent.',
        id: 'wUnPMf',
    },
    assignRole: { defaultMessage: 'Assign yourself the SRE Agent Admin role.', id: 'y79eIb' },
    genericWarningText: { defaultMessage: 'You have warnings regarding your agent.', id: 'spEwRf' },
    learnMore: { defaultMessage: 'Click here to learn more', id: 'xEjqoV' },
    learnMoreAboutRbac: { defaultMessage: 'Click here to learn more about SRE Agent RBAC', id: 'qhApmK' },
    dismissBanner: { defaultMessage: 'Dismiss', id: 'TDaF6J' },
    addAdminNotificationTitle: { defaultMessage: 'Add SRE Agent Administrator role', id: 'MxYLKu' },
    addAdminNotificationDescription: {
        defaultMessage: 'Adding SRE Agent Administrator role to your agent {name}',
        id: 'jfgCRQ',
    },
    addAdminNotificationSuccess: {
        defaultMessage: 'Successfully added SRE Agent Administrator role to your agent {name}',
        id: 'csu95N',
    },
    addAdminNotificationError: {
        defaultMessage: 'Failed to add SRE Agent Administrator role to your agent {name}',
        id: 'LUSiug',
    },
    addAdminNotificationErrorWithMessage: {
        defaultMessage: 'Failed to add SRE Agent Administrator role to your agent {name}. Error: {error}',
        id: 'krH81A',
    },
    muteWarnings: {
        defaultMessage: 'Mute all warnings',
        id: 'pqedsA',
    },
    muteThisWarning: {
        defaultMessage: 'Mute this warning',
        id: 'yJJ/jv',
    },
    goToAgentConsumption: {
        defaultMessage: 'Go to Agent Consumption',
        id: 'j1pB0w',
    },
    usageReachedLimitMessage: {
        defaultMessage:
            'The agent has reached the active flow AAU limit. It now runs only in the always-on flow and is unavailable for chat and acrtions.',
        id: 'rdsirP',
    },
    usageApproachingLimitMessage: {
        defaultMessage:
            'The agent has used 90% of the active flow AAUs. If the limit is reached, the agent will run only in always-on flow and be unavailable for chat and actions.',
        id: 'Cfvmd0',
    },
});

export const IdentityResources = defineMessages({
    goToIdentity: { defaultMessage: 'Go to Identity', id: '8/TArm' },
    identityDescription: {
        defaultMessage: 'Manage the identities associated with your agent which may be used to communicate with other services',
        id: 'b7D4Lh',
    },
});

export const ActivitiesResources = defineMessages({
    createThreadButtonText: { defaultMessage: 'New chat thread', id: 'TkWiD5' },
    createThreadNoPermissionTooltip: {
        defaultMessage: 'You do not have permission to create new chat threads.',
        id: 'RU9VOd',
    },
    sendMessageNoPermissionTooltip: {
        defaultMessage: 'You do not have permission to send a message to this agent.',
        id: 't/atZW',
    },
    sendMessageAriaLabel: {
        defaultMessage: 'Send',
        id: '9WRlF4',
    },
    favoriteThreadNoPermissionTooltip: {
        defaultMessage: 'You do not have permission to favorite or unfavorite threads',
        id: 'Amv/KL',
    },
    approveActionNoPermissionTooltip: {
        defaultMessage: 'You do not have permission to approve this action.',
        id: 'YigUHc',
    },
    chatPivotHeader: { defaultMessage: 'Chat', id: 'WTrOy3' },
    actionsPivotHeader: { defaultMessage: 'Actions', id: 'wL7VAE' },
    chatInputPlaceholder: { defaultMessage: 'Ask a question or enter a slash(/) to use a command', id: 'ynVolH' },
    slashCommandExtendedAgentFeedback: {
        defaultMessage: 'Ready to start a new chat with extended agent “{agentName}”. Your next message will launch it.',
        id: 'WzNnqa',
    },
    slashCommandExtendedAgentTagLabel: {
        defaultMessage: 'Agent: {agentName}',
        id: 'EdZ3pj',
    },
    slashCommandExtendedAgentRemoveButtonLabel: {
        defaultMessage: 'Remove starter agent',
        id: 'V5AOl5',
    },
    slashCommandClearedFeedback: {
        defaultMessage: 'Cleared the composer. Type a new question to begin a fresh chat.',
        id: '6KOA0x',
    },
    slashCommandExtendedAgentCleared: {
        defaultMessage: 'Removed the starter agent. Next messages will stay in this thread.',
        id: 'XKWCS3',
    },
    chatInputAriaLabel: { defaultMessage: 'Chat input', id: 'yFU6JN' },
    noSearchResult: { defaultMessage: 'No results', id: 'jHJmjf' },
    noSearchResultWithSearchText: { defaultMessage: 'No results for "{searchText}"', id: 'qvTohT' },
    extendedAgentShortcutDescription: { defaultMessage: 'Pick a subagent to assist you with tasks.', id: 'yRTBrP' },
    extendedAgentShortcutPlaceholder: { defaultMessage: 'Search for a subagent by name', id: 'hqoTIz' },
    emptyExtendedAgentMessages: { defaultMessage: 'No extended agents available', id: '0iLXks' },
    clearShortcutDescription: { defaultMessage: 'Start a new chat thread.', id: 'GkJbiq' },
    compactShortcutDescription: { defaultMessage: 'Agent responses are more concise in this mode.', id: 'lDx7s5' },
    incidentsShortcutDescription: { defaultMessage: 'List all incidents', id: 'n/WueE' },
    incidentsShortcutPlaceholer: { defaultMessage: 'Search for an incident by name', id: 'D9WytX' },
    resourceShortcutDescription: { defaultMessage: 'List all agent-managed resources', id: 'lbah5J' },
    resourceShortcutPlaceholder: { defaultMessage: 'Search for a resource by name', id: 'E7aYCD' },
    rememberShortcutDescription: { defaultMessage: 'Save information for the agent to remember.', id: 'ca8tFg' },
    retrieveShortcutDescription: { defaultMessage: 'Retrieve previously saved information.', id: 'DadUqS' },
    rememberShortcutPlaceholder: { defaultMessage: 'Type what you want me to remember...', id: 'sXnnRR' },
    retrieveShortcutPlaceholder: { defaultMessage: 'What would you like to retrieve?', id: 'B14lk3' },
    memoryRememberBadge: { defaultMessage: 'Saved to memory', id: '/fkwgv' },
    memoryRetrieveBadge: { defaultMessage: 'Retrieved from memory', id: 'DYeZrX' },
    removeAttachmentButtonAriaLabel: { defaultMessage: 'Remove attachment', id: '6SKklv' },
    removeExtendedAgentAriaLabel: { defaultMessage: 'Remove extended agent: {agentName}', id: 'uMF+CT' },
    knowledgeGraphBuildStatus: {
        defaultMessage: 'Building knowledge about your resources... {percent}% done. You can chat about other topics in the meantime.',
        id: '2Rfala',
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
    connectionErrorTitle: {
        defaultMessage: 'Connection Error.',
        id: '/wdD42',
    },
    reconnecting: {
        defaultMessage: 'Reconnecting to the agent...',
        id: 'sd1Vy+',
    },
    favoriteThreadListTitle: {
        defaultMessage: 'Favorites',
        id: 'SMrXWc',
    },
    regularThreadListTitle: {
        defaultMessage: 'Chats',
        id: 'ABAQyo',
    },
    removeFromFavorites: {
        defaultMessage: 'Remove from favorites',
        id: 'eG1C0k',
    },
    addToFavorites: {
        defaultMessage: 'Add to favorites',
        id: 'tWX1j9',
    },
    threadsLoadingSkeletonAriaLabel: {
        defaultMessage: 'Loading chat threads',
        id: 'puQ/kF',
    },
    reasoning: {
        defaultMessage: 'Reasoning',
        id: 'Aw3qRf',
    },
    thinking: {
        defaultMessage: 'Thinking',
        id: 'AHQWDT',
    },
    thoughtProcess: {
        defaultMessage: 'Thought process',
        id: 'zl6fNb',
    },
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
        defaultMessage: 'This will permanently delete the chat and all actions in this thread. Are you sure you want to delete?',
        id: 'u5uL6R',
    },
    deleteThreadNoPermissionTooltip: {
        defaultMessage: 'You do not have permission to delete this thread',
        id: 'CVAKJE',
    },
    deleteReportTitle: { defaultMessage: "Deleting report ''{title}''", id: 'EI/cfO' },
    deleteReportInProgressDescription: { defaultMessage: 'Deleting report', id: 'rVtn/e' },
    deleteReportSuccessDescription: { defaultMessage: 'Report was deleted successfully', id: 'YBXVNU' },
    deleteReportFailureDescription: {
        defaultMessage: 'Failed to delete report with error: {errorMessage}',
        id: 'exofc7',
    },
    deleteReportDialogTitle: { defaultMessage: 'Delete report?', id: 'RpKuX5' },
    deleteReportDialogDescription: {
        defaultMessage: 'This will permanently delete this report and its information. Are you sure you want to delete this report?',
        id: 'g7HOVb',
    },
    deleteReportNoPermissionTooltip: {
        defaultMessage: 'You do not have permission to delete this report',
        id: '5T/jmb',
    },

    deleteIncidentTitle: { defaultMessage: "Deleting incident ''{title}''", id: 'pnlHOZ' },
    deleteMultipleIncidentsTitle: { defaultMessage: 'Deleting {count} incidents', id: 'zeJdWA' },
    deleteIncidentInProgressDescription: { defaultMessage: 'Deleting incident', id: 'XbNTkN' },
    deletingIncident: { defaultMessage: 'Deleting incident {title}', id: 'jw1Wpc' },
    deletingIncidents: { defaultMessage: 'Deleting incidents {titles}', id: 'BhRlDa' },
    deleteIncidentSuccessDescription: { defaultMessage: 'Incident was deleted successfully', id: 'yNCGjc' },
    deleteIncidentFailureDescription: {
        defaultMessage: 'Failed to delete incident with error: {errorMessage}',
        id: 'lDMkTI',
    },
    deleteIncidentDialogTitle: { defaultMessage: 'Delete incident?', id: '+fXByQ' },
    deleteIncidentDialogDescription: {
        defaultMessage:
            'This will permanently delete this incident and the related incident thread from Azure SRE Agent. Are you sure you want to delete this incident?',
        id: '5wPI+O',
    },
    deleteIncidentNoPermissionTooltip: {
        defaultMessage: 'You do not have permission to delete this incident',
        id: 'zCA8jl',
    },
    renameThreadTitle: { defaultMessage: 'Rename thread title', id: 'zE+jtE' },
    renameThreadInProgressDescription: { defaultMessage: "Renaming the thread title to ''{title}''", id: 'zMX3td' },
    renameThreadSuccessDescription: { defaultMessage: "Thread title was renamed successfully to ''{title}''", id: 'XPR3X/' },
    renameThreadFailureDescription: {
        defaultMessage: 'Failed to rename thread with error: {errorMessage}',
        id: 'pFnETG',
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

export const IncidentHandlerCreateResources = defineMessages({
    generateCustomHandler: { defaultMessage: 'Generate custom response plan', id: 'm0kcbz' },
    reviewAndEdit: { defaultMessage: 'Review + edit', id: 'nFOo9o' },
    priority: { defaultMessage: 'Priority', id: '8lCjAM' },
    dateCreated: { defaultMessage: 'Date created', id: 'Yjk5Ow' },
    title: { defaultMessage: 'Title', id: '9a9+ww' },
    incidentId: { defaultMessage: 'Incident ID', id: 'MB9ceM' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    tool: { defaultMessage: 'Tool', id: 'h6183G' },
    description: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    last1day: { defaultMessage: 'Last day', id: 'boa1qH' },
    last7days: { defaultMessage: 'Last 7 days', id: 'irFBKn' },
    last15days: { defaultMessage: 'Last 15 days', id: '5l3nDr' },
    last30days: { defaultMessage: 'Last 30 days', id: 'Rfvi9/' },
    last60days: { defaultMessage: 'Last 60 days', id: 'KLYuRX' },
    last90days: { defaultMessage: 'Last 90 days', id: 'mgYBYo' },
    chooseIncidentsTitle: { defaultMessage: 'Choose incidents', id: 'aj2txf' },
    chooseIncidentDescription: {
        defaultMessage: 'These previous incidents match the selected incident type. The agent can learn from a maximum of 5 incidents.',
        id: 'yDh8i7',
    },
    availableToolsTitle: { defaultMessage: 'Available tools', id: 'iukUKz' },
    availableToolsDescription: {
        defaultMessage: `The agent uses these available tools to generate incident response plan instructions, based on patterns it learned from the past incidents. You can remove any tools you don't want the agent to use.`,
        id: 'c25ipa',
    },
    addCustomInstructionTitle: { defaultMessage: 'Instruction generation guidance', id: 'eEu4cm' },
    addCustomInstructionDescription: {
        defaultMessage: 'Guidance might include resolution steps, specific instructions, or other relevant context.',
        id: 'yiAEBc',
    },
    customInstructionPlaceholder: { defaultMessage: 'Enter instructions', id: 'AbpmRv' },
    customInstructionsAriaLabel: { defaultMessage: 'Custom response guidance', id: 'et+X02' },
    reviewCustomInstructionsTitle: { defaultMessage: 'Custom response guidance', id: 'et+X02' },
    reviewCustomInstructionsDescription: {
        defaultMessage: 'This is the prompt the agent will use. It includes your custom response guidance. Review and edit if needed.',
        id: 'eackGq',
    },
    reviewToolsTitle: { defaultMessage: 'Tools selected for incident response', id: 'unxs9C' },
    reviewToolsDescription: {
        defaultMessage:
            'The tool list is generated from the custom response guidance. If you modify the guidance, select Regenerate to update the tool list. Once regenerated, the previous list cannot be restored. To add or remove tools, select Manage tools.',
        id: '0mx8D9',
    },
    maximumToolsErrorMessage: { defaultMessage: 'A response plan can use a maximum of {maxTools} tools.', id: 'JkbJ/T' },
    regenerateTools: { defaultMessage: 'Regenerate tools list', id: 'd6hQf3' },
    regenerateToolsConfirmationTitle: { defaultMessage: 'Regenerate tools list', id: 'd6hQf3' },
    regenerateToolsConfirmationMessage: {
        defaultMessage: 'This will overwrite your tool selections. Are you sure you want to continue?',
        id: '0VuGey',
    },
    manageTools: { defaultMessage: 'Manage tools', id: 'ybIDiK' },
    testHandlerTitle: { defaultMessage: 'Test incident response', id: 'rwwXv0' },
    incidentLabel: { defaultMessage: 'Incident', id: 'zaYxwd' },
    incidentPlaceholder: { defaultMessage: 'Select or search for an incident', id: 'XRdOZL' },
    testHandlerEmptyMessage: {
        defaultMessage: 'Select an incident and run the test to see the results here.',
        id: 'MhTfXm',
    },
    testHandlerRunButton: { defaultMessage: 'Run test', id: 'mZ0R9v' },
    testHandlerRunFailure: {
        defaultMessage: 'Failed to run the test. Error: {errorMessage}',
        id: '5X0whH',
    },
    testHandlerRunIncidentNotFound: {
        defaultMessage: 'Incident with ID "{incidentId}" was not found',
        id: 'hEXUFk',
    },
    next: { defaultMessage: 'Next', id: '9+Ddtu' },
    skip: { defaultMessage: 'Skip', id: '/4tOwT' },
    cancel: { defaultMessage: 'Cancel', id: '47FYwb' },
    previous: { defaultMessage: 'Previous', id: 'JJNc3c' },
    back: { defaultMessage: 'Back', id: 'cyR7Kh' },
    generate: { defaultMessage: 'Generate', id: 'Pc+tM3' },
    save: { defaultMessage: 'Save', id: 'jvo0vs' },
    customHandlerAddNotificationTitle: { defaultMessage: 'Add custom incident response plan', id: 'wezZny' },
    customHandlerAddNotificationDescription: { defaultMessage: 'Adding custom incident response plan', id: 'T8DCV7' },
    customHandlerAddNotificationSuccess: { defaultMessage: 'The custom incident response plan was successfully added', id: '8F5cJQ' },
    customHandlerAddNotificationError: {
        defaultMessage: 'Failed to add the custom incident response plan. Error: {errorMessage}',
        id: 'nNwFuy',
    },
    customHandlerUpdateNotificationTitle: { defaultMessage: 'Update custom incident response plan', id: 'Nf85D+' },
    customHandlerUpdateNotificationDescription: { defaultMessage: 'Updating custom incident response plan', id: 'Dh9oJp' },
    customHandlerUpdateNotificationSuccess: { defaultMessage: 'The custom incident response plan was successfully updated', id: '4+Jgpb' },
    customHandlerUpdateNotificationError: {
        defaultMessage: 'Failed to update the custom incident response plan. Error: {errorMessage}',
        id: 'tbTzZV',
    },
    customHandlerDeleteConfirmationTitle: { defaultMessage: 'Delete custom incident response plan', id: 'b2kjHU' },
    customHandlerDeleteConfirmationMessage: { defaultMessage: 'Are you sure you want to delete the custom response plan?', id: 'lawXja' },
    customHandlerDeleteNotificationTitle: { defaultMessage: 'Delete custom incident response plan', id: 'b2kjHU' },
    customHandlerDeleteNotificationDescription: { defaultMessage: 'Deleting custom incident response plan', id: 'y+SPRf' },
    customHandlerDeleteNotificationSuccess: { defaultMessage: 'The custom incident response plan was successfully deleted', id: 'qyTQAA' },
    customHandlerDeleteNotificationError: {
        defaultMessage: 'Failed to delete the custom incident response plan. Error: {errorMessage}',
        id: 'Hh0q8u',
    },
    customHandlerName: { defaultMessage: 'Custom response plan name', id: 'Lq4eva' },
    customHandlerDescription: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    incidentManagement: { defaultMessage: 'Incident management', id: 'T7WpWs' },
    newCustomHandler: { defaultMessage: 'New custom response plan', id: 'iqdd9H' },
    editCustomHandler: { defaultMessage: 'Edit custom response plan', id: 'LyuutK' },
    newIncidentHandler: { defaultMessage: 'New incident response plan', id: 'uEYYty' },
    editIncidentHandler: { defaultMessage: 'Edit incident response plan', id: 'Kkc3/u' },
    regenerate: { defaultMessage: 'Regenerate', id: '6PgVSe' },
    regenerateTooltip: {
        defaultMessage: 'Regenerate will overwrite any edits made within the generated section of code.',
        id: 'DVVbGP',
    },
    export: { defaultMessage: 'Export', id: 'SVwJTM' },
    customHandlerCreateDescription: {
        defaultMessage:
            'With incident response plans, the agent chooses the tools it needs to manage common types of incidents. Custom response plans replace this capability by giving the agent specific instructions that you provide.',
        id: 'JRLN0D',
    },
    responsePlanDetails: { defaultMessage: 'Response plan details', id: 'XHoPAJ' },
    customInstructions: { defaultMessage: 'Custom instructions', id: 'D7U9Zo' },
    selectedIncidents: { defaultMessage: 'Selected incidents', id: 'DX7w9O' },
    selectedIncidentsEmptyText: { defaultMessage: 'No incidents selected', id: 'QQX4Pv' },
    filterStep: { defaultMessage: 'Create incident response plan', id: 'Z9pRs5' },
    previewIncidentsStep: { defaultMessage: 'Preview incidents', id: '69FfbB' },
    previewIncidentsDescription: {
        defaultMessage:
            'These incidents match your filter criteria. If an incident is missing, go back to the previous step and modify the filter parameters.',
        id: '6LZ2qn',
    },
    incidentsAndGuidanceStep: { defaultMessage: 'Add instructions', id: 'HjTHxo' },
    reviewAndTestStep: { defaultMessage: 'Review + test', id: '3PxUNi' },
    deployStep: { defaultMessage: 'Deploy incident response plan', id: 'p21QSW' },
    filterParametersTitle: { defaultMessage: 'Choose filter parameters', id: 'JwHSD6' },
    filterParametersDescription: {
        defaultMessage:
            'Filters define which incidents the incident response plan applies to. These apply to the list of incidents previewed in the next step.',
        id: 'LZyV4T',
    },
    enableDeepInvestigationTitle: {
        defaultMessage: 'Choose whether to run deep investigations alongside regular investigations',
        id: 'DJN+DZ',
    },
    enableDeepInvestigationDescription: { defaultMessage: 'Run deep investigation autonomously', id: 's7HLdg' },
    addCustomResponseGuidanceTitle: { defaultMessage: 'Add custom response guidance (optional)', id: 'koWZK8' },
    addCustomResponseGuidanceDescription: {
        defaultMessage: 'This guidance helps generate the tools list and the final prompt the agent uses during incident handling.',
        id: 'vao/Lv',
    },
    addCustomResponseGuidanceLabel: { defaultMessage: 'Add guidance', id: 'eZMksq' },
    includedIncidentsLabel: { defaultMessage: 'Choose how to set up this incident response plan', id: 'gKi3tP' },
    includedIncidentsFutureOnly: { defaultMessage: 'Apply only to incidents triggered after the response plan is created', id: 'd1Cewp' },
    includedIncidentsPastAndFuture: { defaultMessage: 'Apply to all current and future active incidents', id: '0w0iFy' },
    deepInvestigationDialogTitle: { defaultMessage: 'Turn on deep investigation?', id: '4IAjnN' },
    deepInvestigationDialogContent: {
        defaultMessage:
            'A deep investigation will be run for every incident handled in this response plan. Each deep investigation requires significant time to run and might result in high AAU consumption.',
        id: 'J2Lcsz',
    },
    deepInvestigationDialogCheckboxLabel: {
        defaultMessage: 'Yes, turn on deep investigation',
        id: 'tFoDh3',
    },
});

export const IncidentManagementResources = defineMessages({
    incidentManagement: { defaultMessage: 'Incident management', id: 'T7WpWs' },
    incidentManagementDescription: {
        defaultMessage: `Add an incident platform so that the agent can help respond to incidents in real time. To change to a different platform, you'll need to delete the connection to the current one.`,
        id: 'D/TfIH',
    },
    editIncidentHandlerDescription: {
        defaultMessage:
            'Changes to this incident response plan might affect how incidents are processed and also any custom response plans.',
        id: 'i6RxOt',
    },
    createIncidentHandlerDescription: {
        defaultMessage:
            'An incident response plan defines which incidents the agent should handle by applying your filter criteria, ensuring responses to the required set of incidents.',
        id: '4lEOsR',
    },
    refresh: { defaultMessage: 'Refresh', id: 'rELDbB' },
    incidentPlatform: { defaultMessage: 'Incident platform', id: 'EZBG/A' },
    newIncidentHandler: { defaultMessage: 'New incident response plan', id: 'uEYYty' },
    incidentHandler: { defaultMessage: 'Incident response plan', id: 'mky0K0' },
    createIncidentHandler: { defaultMessage: 'Create incident response plan', id: 'Z9pRs5' },
    editIncidentHandler: { defaultMessage: 'Edit incident response plan', id: 'Kkc3/u' },
    incidentHandlerNamePlaceholder: { defaultMessage: 'Enter a response plan name', id: 'MOsoTa' },
    customHandler: { defaultMessage: 'Custom response plan', id: '+S4WAz' },
    id: { defaultMessage: 'ID', id: 'qlcuNQ' },
    severity: { defaultMessage: 'Severity', id: 'vCAhII' },
    dateModified: { defaultMessage: 'Date modified', id: 'KyDsjH' },
    allSeverity: { defaultMessage: 'All severity', id: 'zGhyFV' },
    title: { defaultMessage: 'Title', id: '9a9+ww' },
    titlePlaceholder: { defaultMessage: 'Enter title keywords', id: 'sH0O5v' },
    owningTeam: { defaultMessage: 'Owning team', id: '0BVwlv' },
    owningTeamId: { defaultMessage: 'Owning team ID', id: 'SezWLK' },
    owningIcmTeamPlaceholder: { defaultMessage: 'Search Icm team', id: 'lCe4Sh' },
    owningTeamIdPlaceholder: { defaultMessage: 'Enter owning team ID', id: '5S1JBf' },
    monitorId: { defaultMessage: 'Monitor ID', id: 'Hv7pA2' },
    monitorIdPlaceholder: { defaultMessage: 'Enter monitor ID', id: 'VYjDXQ' },
    createdBy: { defaultMessage: 'Created by', id: 'p4mBmL' },
    createdByPlaceholder: { defaultMessage: 'Enter created by alias', id: '//ter/' },
    incidentType: { defaultMessage: 'Incident type', id: 'Udeffr' },
    impactedService: { defaultMessage: 'Impacted service', id: 'fdCjVS' },
    alertId: { defaultMessage: 'Alert ID', id: 'k8ZNgH' },
    alertTitle: { defaultMessage: 'Alert title', id: 'pr9fPP' },
    alertStatus: { defaultMessage: 'Alert status', id: '1hu8Uv' },
    alertCreated: { defaultMessage: 'Alert created', id: '4HV6SH' },
    titleContains: { defaultMessage: 'Title contains', id: 'brxlTt' },
    setUp: { defaultMessage: 'Set up', id: 'rrGMSx' },
    getAllIncidents: { defaultMessage: 'Get all incidents', id: 'JgQ1gX' },
    filterIncidents: { defaultMessage: 'Filter incidents', id: 'PJ2FQv' },
    priority: { defaultMessage: 'Priority', id: '8lCjAM' },
    allIncidentTypes: { defaultMessage: 'All incident types', id: 'G8H3+s' },
    allImpactedServices: { defaultMessage: 'All impacted services', id: 'MlX0aZ' },
    allPriorities: { defaultMessage: 'All priorities', id: 'uCkn4+' },
    baseIncident: { defaultMessage: 'Base incident', id: 'UjETJe' },
    all: { defaultMessage: 'All', id: 'zQvVDJ' },
    last30Days: { defaultMessage: 'Last 30 days', id: 'Rfvi9/' },
    last14Days: { defaultMessage: 'Last 14 days', id: 'BVVhyH' },
    last7Days: { defaultMessage: 'Last 7 days', id: 'irFBKn' },
    last3Days: { defaultMessage: 'Last 3 days', id: 'gUQeKV' },
    last24Hours: { defaultMessage: 'Last 24 hours', id: '8O9cAb' },
    last12Hours: { defaultMessage: 'Last 12 hours', id: 'S1gYH9' },
    last6Hours: { defaultMessage: 'Last 6 hours', id: 'ZVyc4+' },
    lastHour: { defaultMessage: 'Last hour', id: 's8HBot' },
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
    chooseIncidentType: { defaultMessage: 'Choose incident type', id: 'm57OnF' },
    chooseImpactedService: { defaultMessage: 'Choose impacted service', id: 'DmtYK4' },
    choosePriority: { defaultMessage: 'Choose priority', id: 'jA2jt7' },
    chooseSeverity: { defaultMessage: 'Choose severity', id: 'rKq4sw' },
    setUpComplete: { defaultMessage: 'Setup complete', id: 'jOkfJV' },
    connected: { defaultMessage: 'Connected', id: 'IvjoDS' },
    notConnected: { defaultMessage: 'Not connected', id: 'PuU15u' },
    waitingForConnectivity: { defaultMessage: 'Waiting for connectivity', id: 'RXSky0' },
    goToHandler: { defaultMessage: 'Go to response plan', id: '4d5TjZ' },
    created: { defaultMessage: 'Created', id: 'ORGv1Q' },
    incidentHandlerName: { defaultMessage: 'Incident response plan name', id: 'W99XAG' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    incidentManagementTabDescription: {
        defaultMessage:
            'Optimize how the agent investigates and responds to incidents by adding incident response plans and custom response plans to support a broad range of scenarios.',
        id: 'rkqVE1',
    },
    filterDeleteConfirmationTitle: { defaultMessage: 'Delete incident response plan', id: 'h+7264' },
    filterDeleteConfirmationMessage: {
        defaultMessage:
            'This will permanently delete the incident response plan, which might affect how incidents are processed and also any custom response plans. Are you sure you want to delete?',
        id: '15GWkT',
    },
    filterDisableConfirmationTitle: { defaultMessage: 'Turn off incident response plan', id: 'bvvGgu' },
    filterDisableConfirmationMessage: {
        defaultMessage:
            'Turning off this incident response plan might affect how incidents are processed and also any custom response plans. Are you sure you want to turn it off?',
        id: 'd3MtwZ',
    },
    quickstartHandler: { defaultMessage: 'Quickstart response plan', id: 'xSsto+' },
    quickstartHandlerInfoMessage: {
        defaultMessage:
            'After the platform is connected, create incident response plans and custom response plans so that the agent can respond to incidents.',
        id: 'swVMrk',
    },
    autonomyLevel: { defaultMessage: 'Autonomy level', id: 'Sdc+Dp' },
    agentAutonomyLevel: { defaultMessage: 'Agent autonomy level', id: 'AC5nsM' },
    autonomousDefault: { defaultMessage: 'Autonomous (Default)', id: 'Ypp6em' },
    reviewWord: { defaultMessage: 'Review', id: 'R+J5ox' },
    autonomousWord: { defaultMessage: 'Autonomous', id: 'Sr5R7d' },
    autonomyLevelReviewDescription: {
        defaultMessage:
            'The semiautonomous mode. The agent diagnoses incidents, then mitigates or modifies resources only after its proposed actions are reviewed and approved.',
        id: 'dhqlRa',
    },
    autonomyLevelAutonomousDescription: {
        defaultMessage:
            'The fully autonomous mode. With the required permissions, the agent analyzes incidents and independently performs mitigation or resource modifications.',
        id: 'XKhlZF',
    },
    incidentManagementLoadFailure: {
        defaultMessage: 'Failed to load incident management configuration. Error: {errorMessage}',
        id: '0TkVxX',
    },
    handler: { defaultMessage: 'Response plan', id: '1rAiXS' },
    agentStatus: { defaultMessage: 'Agent status', id: '5XRsWa' },
    priorities: { defaultMessage: 'Priorities', id: '/Br0/Z' },
    severities: { defaultMessage: 'Severities', id: '7vl63m' },
    pendingUserInput: { defaultMessage: 'Pending user input', id: 'Wkup5T' },
    inProgress: { defaultMessage: 'In progress', id: 'q1WWIr' },
    completed: { defaultMessage: 'Completed', id: '95stPq' },
    mitigatedByAgent: {
        defaultMessage: 'Mitigated by agent',
        id: 'D4T2Px',
    },
    resolvedByAgent: {
        defaultMessage: 'Resolved by agent',
        id: 'R7DP3y',
    },
    allStatuses: { defaultMessage: 'All statuses', id: 'fvK8Qi' },
    allActions: { defaultMessage: 'All actions', id: 'jK9a9x' },
    noPermissionNewIncidentHandler: { defaultMessage: 'You do not have permission to create response plans.', id: 'Tq2F2Y' },
    noPermissionDeleteIncidentHandler: { defaultMessage: 'You do not have permission to delete response plans.', id: 'fEYQWU' },
    noPermissionTurnOffIncidentHandler: { defaultMessage: 'You do not have permission to turn response plans off.', id: 'vPX3R3' },
    noPermissionTurnOnIncidentHandler: { defaultMessage: 'You do not have permission to turn response plans on.', id: 'xVnsKO' },
    noPermissionEditIncidentHandler: { defaultMessage: 'You do not have permission to edit response plans.', id: 'jXIYLG' },
    incident: { defaultMessage: 'Incident', id: 'zaYxwd' },
    incidentId: { defaultMessage: 'Incident ID', id: 'MB9ceM' },
    handlerConfiguration: { defaultMessage: 'Response plan configuration', id: 'QdDYGR' },
    responsePlanDetails: { defaultMessage: 'Response plan details', id: 'XHoPAJ' },
    mitigated: { defaultMessage: 'Mitigated', id: 'dnXgff' },
    resolved: { defaultMessage: 'Resolved', id: 'W6nSYE' },
    active: { defaultMessage: 'Active', id: '3a5wL8' },
    unknown: { defaultMessage: 'Unknown', id: '5jeq8P' },
    fullPage: { defaultMessage: 'Full page', id: 'Pcf4MK' },
    viewTrace: { defaultMessage: 'View trace', id: 'Hgs+WM' },
    closePanel: { defaultMessage: 'Close panel', id: 'RAjqKb' },
    noIncidentsFound: { defaultMessage: 'No incidents found', id: '312q4w' },
    expandNavigation: { defaultMessage: 'Expand navigation', id: '3wVEAO' },
    collapseNavigation: { defaultMessage: 'Collapse navigation', id: 'IoApza' },
    incidentThreadsMovedTitle: { defaultMessage: 'Incident threads have moved', id: 'R7g7UT' },
    incidentThreadsMovedDescription: {
        defaultMessage: 'Find incident threads and incident response plans together on the Incident Management tab.',
        id: 'z8ZYoc',
    },
    selectedOutOfTotal: { defaultMessage: '{selectedCount} of {totalCount}', id: '01sZoP' },
    metrics: { defaultMessage: 'Metrics', id: 'HNBpJ4' },
    responsePlans: { defaultMessage: 'Response plans', id: 'DeP+ZM' },
    totalIncidents: { defaultMessage: 'Total incidents', id: '2FLZrG' },
    incidentsReviewed: { defaultMessage: 'Incidents reviewed', id: 'KJuQJ3' },
    incidentsNotHandledByResponsePlanCriteria: { defaultMessage: 'Incidents not handled by response plan criteria', id: 'MIxpkh' },
    pendingUserAction: { defaultMessage: 'Pending user action', id: 'rM7Pbj' },
    incidentSummary: { defaultMessage: 'Incident summary', id: '2Smahe' },
    incidentsThatRequireAttention: { defaultMessage: 'Incidents that require attention', id: '5gEh2M' },
    mitigatedByUser: { defaultMessage: 'Mitigated by user', id: '5cRddy' },
    acrossAllIncidentsInPeriod: { defaultMessage: 'Across all incidents in {platform}', id: 'TKp3Ld' },
    incidentsMitigatedByAgent: { defaultMessage: 'Incidents mitigated by agent', id: 'd+XOMl' },
    incidentsMitigatedByUser: { defaultMessage: 'Incidents mitigated by user', id: 'makWrV' },
    responsePlanName: { defaultMessage: 'Response plan name', id: 'BT9p8f' },
    incidentResponsePlan: { defaultMessage: 'Incident response plan', id: 'mky0K0' },
    noResponsePlansFound: { defaultMessage: 'No response plans found', id: 'W/siYq' },
    customPlan: { defaultMessage: 'Custom plan', id: 'NW3Qi+' },
    filterByResponsePlanName: { defaultMessage: 'Filter by response plan name', id: 'mg8UeB' },
    allCustomPlans: { defaultMessage: 'All custom plans', id: 'tbPq1d' },
    viewPlan: { defaultMessage: 'View details', id: 'MnpUD7' },
    usingThisResponsePlan: { defaultMessage: 'Using this response plan', id: 'aF4iw+' },
    responsePlanSaved: { defaultMessage: 'Response plan saved successfully', id: 'kvKWEJ' },
    responsePlanSaveFailed: { defaultMessage: 'Failed to save response plan', id: '2Mm/ie' },
    rootCauseAnalysis: { defaultMessage: 'Root cause analysis', id: '1YF+AW' },
    rootCauseAnalysisDescription: { defaultMessage: "The agent's analysis of the causes that triggered the incident.", id: 'JYbAwr' },
    allSeverityLevels: { defaultMessage: 'All severity levels', id: 'ffTMIX' },
    allMitigatedBy: { defaultMessage: 'All mitigated by', id: '5w4pim' },
    incidentTitle: { defaultMessage: 'Incident title', id: '2Oxmp9' },
    incidentStatus: { defaultMessage: 'Incident status', id: 'uld/2m' },
    severityLevel: { defaultMessage: 'Severity level', id: 'LwwzP6' },
    incidentCreated: { defaultMessage: 'Incident created', id: 'SFUZOQ' },
    mitigatedBy: { defaultMessage: 'Mitigated by', id: 'YgyuGj' },
    filterByIncidentIdOrTitle: { defaultMessage: 'Filter by incident id or title', id: 'dF2h2y' },
    topCategories: { defaultMessage: 'Top categories', id: 'hZY54D' },
    incidents: { defaultMessage: 'Incidents', id: 'mtr3R4' },
    noRcaCategoriesFound: { defaultMessage: 'No root cause analysis categories found', id: 'jRtus7' },
    assistedByAgent: { defaultMessage: 'Assisted by agent', id: 'ryPHA8' },
    incidentsAssistedByAgent: { defaultMessage: 'Incidents assisted by agent', id: 'SyPo+k' },
    deleteIncidentThreadConfirmation: { defaultMessage: 'Are you sure you want to delete the selected incident thread?', id: 'qs5hhk' },
    deleteIncidentThreadsConfirmation: { defaultMessage: 'Are you sure you want to delete the selected incident threads?', id: 'us/IXr' },
    filterByAutonomyLevel: { defaultMessage: 'Filter by autonomy level', id: 'OIWzWF' },
    filterByCustomPlan: { defaultMessage: 'Filter by custom plan', id: 'coPQxN' },
    filterBySeverityLevel: { defaultMessage: 'Filter by severity level', id: '+Hx789' },
    filterByMitigatedBy: { defaultMessage: 'Filter by mitigated by', id: 'mm/uFP' },
    generating: { defaultMessage: 'Generating…', id: 'tB02Wz' },
    platformEmptyStateTitle: { defaultMessage: 'Optimize alert response with an incident platform', id: 'V5w7/I' },
    platformEmptyStateMessage: {
        defaultMessage: 'Connect an incident management platform so the agent will collect, analyze, and respond to alerts.',
        id: 'p8Ubox',
    },
    platformEmptyStateLearnMore: { defaultMessage: 'Learn more about incident platform integration', id: 'fjOQVZ' },
    platformEmptyStateButtonText: { defaultMessage: 'Connect an incident platform', id: 'ItmLyp' },
    handlersEmptyStateTitle: { defaultMessage: 'Add a response plan to automate incident handling', id: '1CcjQj' },
    handlersEmptyStateMessage: {
        defaultMessage: 'Define how incidents are detected and reviewed, and the instructions for how the agent responds.',
        id: 'FPdoJ3',
    },
    handlersEmptyStateLearnMore: { defaultMessage: 'Learn more about response plans', id: 'HQ4NJ3' },
    handlersEmptyStateButtonText: { defaultMessage: 'Add a response plan', id: 'GkKcbX' },
    rcaCategory: { defaultMessage: 'RCA category', id: 'hqWr3L' },
    rcaCategoryLabel: { defaultMessage: 'Category', id: 'ccXLVi' },
    relatedIncidents: { defaultMessage: 'Related incidents', id: '+Kcoxv' },
    close: { defaultMessage: 'Close', id: 'rbrahO' },
    whatHappened: { defaultMessage: 'What happened', id: 'Xup5P8' },
    incidentTeamSearchAssignableOnly: { defaultMessage: 'Only teams that allow incident assignment', id: 'Q0B/Lb' },
    incidentTeamSearchWithOncallRotation: { defaultMessage: 'Only teams with an on-call rotation', id: 'dLWrbO' },
});

export const TriggerIncidentManagementResources = defineMessages({
    triggerAgent: { defaultMessage: 'Trigger agent', id: 'wAZ9A7' },
    search: { defaultMessage: 'Search', id: 'xmcVZ0' },
    submit: { defaultMessage: 'Submit', id: 'wSZR47' },
    incidentProcessSuccess: {
        defaultMessage: 'Incident {incidentId} successfully processed.',
        id: '7e84yC',
    },
    incidentProcessSuccessWithThread: {
        defaultMessage: 'Incident {incidentId} successfully processed. (Thread ID: {threadId})',
        id: 'LZXqGi',
    },
    incidentProcessFailure: {
        defaultMessage: 'Incident {incidentId} cannot be processed. {message}',
        id: 'B2HRS5',
    },
    incidentProcessFailureWithThread: {
        defaultMessage: 'Incident {incidentId} cannot be processed. {message} (Thread ID: {threadId})',
        id: 'I85tfP',
    },
    incidentId: { defaultMessage: 'Incident ID', id: 'MB9ceM' },
    incidentProperties: { defaultMessage: 'Incident properties', id: 'UC9FAP' },
    enterIncidentId: { defaultMessage: 'Enter incident ID', id: 'wTdxhX' },
    incidentCreateTimeRange: { defaultMessage: 'Incident created time range', id: 'DlPyyy' },
    searchBy: { defaultMessage: 'Search by', id: 'Vd/5xJ' },
    showingTopResults: { defaultMessage: 'Showing top {count} results', id: 'aT1kVu' },
});

export const IncidentManagementPlatformResources = defineMessages({
    disconnected: { defaultMessage: 'Choose a platform', id: '/2OpVO' },
    pagerDuty: { defaultMessage: 'PagerDuty', id: '6UyZlH' },
    azMonitor: { defaultMessage: 'Azure Monitor', id: '7Nz2Ev' },
    icm: { defaultMessage: 'Microsoft IcM', id: '0D+7fr' },
    serviceNow: { defaultMessage: 'ServiceNow', id: 'zg0rpo' },
});

export const IncidentManagementNotificationResources = defineMessages({
    saveTitle: { defaultMessage: 'Saving incident management configuration', id: 'TyvDrC' },
    saveInProgress: { defaultMessage: 'Saving incident management configuration', id: 'TyvDrC' },
    saveSucceeded: { defaultMessage: 'Saved incident management configuration', id: 'yxXhfZ' },
    saveFailed: { defaultMessage: 'Failed to save incident management configuration. Error: {errorMessage}', id: 'slxYbm' },
    connectionToPlatformTitle: { defaultMessage: 'Connecting to {platformName} ...', id: 'b2PyOl' },
    connectionToPlatformInProgress: { defaultMessage: 'Connecting to {platformName} ...', id: 'b2PyOl' },
    connectionToPlatformSuccess: { defaultMessage: 'Connected to {platformName}', id: 'X4oOy4' },
    connectionToPlatformFailed: { defaultMessage: 'Failed to connect to {platformName}', id: '8v0Pyn' },
    createDefaultHandlerTitle: { defaultMessage: 'Creating default incident response plan', id: 't3wAiN' },
    createDefaultHandlerInProgress: { defaultMessage: 'Creating default incident response plan', id: 't3wAiN' },
    createDefaultHandlerSuccess: { defaultMessage: 'Created default incident response plan', id: 'AmnkPD' },
    createDefaultHandlerFailed: { defaultMessage: 'Failed to create default incident response plan. Error: {errorMessage}', id: 'ysBuRt' },
    deleteFilterTitle: { defaultMessage: 'Deleting incident response plan', id: 'it8L1Q' },
    deleteFilterInProgress: { defaultMessage: 'Deleting incident response plan', id: 'it8L1Q' },
    deleteFilterSuccess: { defaultMessage: 'Deleted incident response plan', id: '4ZoIgV' },
    deleteFilterError: { defaultMessage: 'Failed to delete incident response plan', id: 'zEg90u' },
    createFilterTitle: { defaultMessage: 'Creating incident response plan', id: 'H5vHIu' },
    createFilterInProgress: { defaultMessage: 'Creating incident response plan', id: 'H5vHIu' },
    createFilterSuccess: { defaultMessage: 'Created incident response plan', id: 'LLt8Ve' },
    createFilterError: { defaultMessage: 'Failed to create incident response plan', id: 'xrnaaX' },
    updateFilterTitle: { defaultMessage: 'Updating incident response plan', id: '5Wtjeo' },
    updateFilterInProgress: { defaultMessage: 'Updating incident response plan', id: '5Wtjeo' },
    updateFilterSuccess: { defaultMessage: 'Updated incident response plan', id: '91775r' },
    updateFilterError: { defaultMessage: 'Failed to update incident response plan', id: 'smNgqR' },
    enableFilterTitle: { defaultMessage: 'Enabling incident response plan', id: 'vs8s5B' },
    enableFilterInProgress: { defaultMessage: 'Enabling incident response plan', id: 'vs8s5B' },
    enableFilterSuccess: { defaultMessage: 'Enabled incident response plan', id: 'bxcc6L' },
    enableFilterError: { defaultMessage: 'Failed to enable incident response plan', id: 'I7aBF4' },
    disableFilterTitle: { defaultMessage: 'Disabling incident response plan', id: 'NMSoXQ' },
    disableFilterInProgress: { defaultMessage: 'Disabling incident response plan', id: 'NMSoXQ' },
    disableFilterSuccess: { defaultMessage: 'Disabled incident response plan', id: 'YJ+mk9' },
    disableFilterError: { defaultMessage: 'Failed to disable incident response plan', id: 'tHCvDB' },
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
    addNotificationError: { defaultMessage: 'Failed to assign roles to managed resource groups with error: {error}', id: 'nCbZ6p' },
    locationsSelected: { defaultMessage: '{0} locations selected', id: 'JbB2Vb' },
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
    subscriptionsSelected: { defaultMessage: '{0} subscriptions selected', id: 'G5JMSn' },
    selectSubscription: { defaultMessage: 'Select subscription', id: 'sAireQ' },
    noResults: { defaultMessage: 'No results', id: 'jHJmjf' },
    filterItems: { defaultMessage: 'Filter items', id: 'F9LrJA' },
    loading: { defaultMessage: 'Loading...', id: 'gjBiyj' },
    subscriptionsLoadFailure: { defaultMessage: 'Failed to load subscriptions.', id: 'EKfWmx' },
    region: { defaultMessage: 'Region', id: 'lnaWo/' },
    resourceGroup: { defaultMessage: 'Resource group', id: '+uAdUZ' },
    refresh: { defaultMessage: 'Refresh', id: 'rELDbB' },
    deleteResourceGroupAriaLabel: { defaultMessage: 'Delete resource group {resourceGroupName}', id: 'XG++pU' },
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
    serviceNowEndpointRequired: { defaultMessage: 'ServiceNow endpoint is required.', id: 'FitglU' },
    serviceNowUsernameRequired: { defaultMessage: 'Username is required.', id: 'aAADUG' },
    serviceNowPasswordRequired: { defaultMessage: 'Password is required.', id: 'pRvgsc' },
    serviceNowInvalidCredentials: { defaultMessage: 'Invalid username or password. Please check your credentials.', id: '1YN49J' },
    serviceNowConnectionError: {
        defaultMessage: 'Unable to connect to ServiceNow endpoint. Please verify the endpoint URL.',
        id: 'GUx4Ld',
    },
    serviceNowFailedToValidate: { defaultMessage: 'Failed to validate ServiceNow settings. Please try again.', id: 'J3Y8Sm' },
    icmOwningTeamIdRequired: { defaultMessage: 'Owning team ID is required when adding a default response plan.', id: 'mGh+K0' },
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
            'This will permanently delete the connection to PagerDuty. The agent will no longer be able to manage tickets. Are you sure you want to disconnect?',
        id: 'r2x5ZI',
    },
    changePlatformConfirmationTitle: { defaultMessage: 'Disconnect PagerDuty?', id: 'cTY8WU' },
    changePlatformConfirmationMessage: {
        defaultMessage:
            'To change the incident platform, you need to disconnect from PagerDuty. The agent will no longer manage tickets. Are you sure you want to disconnect?',
        id: 'CnouOe',
    },
    notConnectedMessage: { defaultMessage: 'PagerDuty is not connected.', id: 'hHz/bk' },
    connectedMessage: { defaultMessage: 'PagerDuty is connected.', id: '23nct0' },
    connectedMessageWithoutHandlers: {
        defaultMessage: 'PagerDuty connected. Your next step is to set up incident response plans.',
        id: 'Vtdg82',
    },
    connectingMessage: { defaultMessage: 'Connecting to PagerDuty ...', id: 'ey7R11' },
    quickstartHandlerDescription: {
        defaultMessage: 'Add a default incident response plan for the agent to use for P1 base incidents.',
        id: 'm6Ctmt',
    },
    connectionFailureMessage: {
        defaultMessage: 'Connection to PagerDuty failed. Please check your access key and try again.',
        id: '0R+Pet',
    },
    p1: { defaultMessage: 'P1', id: 'QBeCBS' },
    p2: { defaultMessage: 'P2', id: 'PwqiFa' },
    p3: { defaultMessage: 'P3', id: '+Aon83' },
    p4: { defaultMessage: 'P4', id: 'pAdN7z' },
    p5: { defaultMessage: 'P5', id: '94zF5I' },
});

export const AzMonitorResources = defineMessages({
    description: {
        defaultMessage:
            'Connect to Azure Monitor so that the agent can automatically monitor notifications from the resource groups it manages, without additional provisioning.',
        id: 'iDSWYk',
    },
    disconnectConfirmationTitle: { defaultMessage: 'Disconnect Azure Monitor?', id: 'blSyDZ' },
    disconnectConfirmationMessage: {
        defaultMessage:
            'This will permanently delete the connection to Azure Monitor. The agent will no longer be integrated with Azure Monitor notifications. Are you sure you want to delete this connection?',
        id: 'w/UZrq',
    },
    changePlatformConfirmationTitle: { defaultMessage: 'Disconnect Azure Monitor?', id: 'blSyDZ' },
    changePlatformConfirmationMessage: {
        defaultMessage:
            'To change the incident platform, you need to disconnect from Azure Monitor. The agent will no longer receive Azure Monitor notifications. Are you sure you want to disconnect?',
        id: 'obRqDQ',
    },
    notConnectedMessage: { defaultMessage: 'Azure Monitor is not connected.', id: '52hqVX' },
    connectedMessage: { defaultMessage: 'Azure Monitor is connected', id: 'Pm5Z9t' },
    connectedMessageWithoutHandlers: {
        defaultMessage: 'Azure Monitor connected. Your next step is to set up incident response plans.',
        id: 'b8MiMo',
    },
    connectingMessage: { defaultMessage: 'Connecting to Azure Monitor ...', id: 'lqgE72' },
    quickstartHandlerDescription: {
        defaultMessage: 'Add a default incident response plan for the agent to use for Sev3 alerts.',
        id: 'eUEjcm',
    },
    connectionFailureMessage: {
        defaultMessage: 'Connection to Azure Monitor failed. Please check your configuration and try again.',
        id: 'Dmb0ZD',
    },
    sev0: { defaultMessage: 'Sev0', id: 'emW/QQ' },
    sev1: { defaultMessage: 'Sev1', id: 'f2e1WS' },
    sev2: { defaultMessage: 'Sev2', id: 'uyO1AJ' },
    sev3: { defaultMessage: 'Sev3', id: 'v9OQKq' },
    sev4: { defaultMessage: 'Sev4', id: 'EWrExf' },
});

export const IcMResources = defineMessages({
    disconnectConfirmationTitle: { defaultMessage: 'Disconnect IcM?', id: 'vAWzLN' },
    disconnectConfirmationMessage: {
        defaultMessage:
            'This will permanently delete the connection to IcM. The agent will no longer be able to manage incidents. Are you sure you want to delete this connection?',
        id: 'VwHnVH',
    },
    changePlatformConfirmationTitle: { defaultMessage: 'Disconnect IcM?', id: 'vAWzLN' },
    changePlatformConfirmationMessage: {
        defaultMessage:
            'To change the incident platform, you need to disconnect from IcM. The agent will no longer manage incidents. Are you sure you want to disconnect?',
        id: 'X8SzJ6',
    },
    notConnectedMessage: { defaultMessage: 'IcM is not connected.', id: '+7dYeU' },
    connectedMessage: { defaultMessage: 'IcM is connected.', id: 'tyJNW5' },
    connectedMessageWithoutHandlers: {
        defaultMessage: 'IcM connected. Your next step is to set up incident response plans.',
        id: 'A0rbH+',
    },
    connectingMessage: { defaultMessage: 'Connecting to IcM ...', id: 's9ZhRf' },
    connectionDescription: {
        defaultMessage: 'Connect to ICM so that the Agent can automatically listen and respond to your Incidents.',
        id: 'o7TUwc',
    },
    allowListDescription: {
        defaultMessage: 'Allowlist below Managed Identity on your ICM Service Team.',
        id: 'oTNrPz',
    },
    managedIdentity: {
        defaultMessage: 'Managed Identity',
        id: 'UZMdQH',
    },
    agentSpaceManagedIdentity: {
        defaultMessage:
            'Managed Identity (Agent Space managed identity automatically chosen as Agent Space and compatible connector was found) ',
        id: 'JbmMWM',
    },
    quickstartHandlerDescription: {
        defaultMessage: 'Add a default incident response plan for the agent to use for Sev3 LiveSite incidents.',
        id: '+nt4l9',
    },
    connectionFailureMessage: {
        defaultMessage:
            'Connection to IcM failed. Please confirm that the managed identity of your agent is allowlisted on your IcM service team and try again.',
        id: 'JXtKxS',
    },
    sev2: { defaultMessage: 'Sev2', id: 'uyO1AJ' },
    sev2_5: { defaultMessage: 'Sev2.5', id: 'XEanEV' },
    sev3: { defaultMessage: 'Sev3', id: 'v9OQKq' },
    sev4: { defaultMessage: 'Sev4', id: 'EWrExf' },
});

export const SettingsTabResources = defineMessages({
    incidentPlatform: { defaultMessage: 'Incident platform', id: 'EZBG/A' },
    accessControl: { defaultMessage: 'Access control (IAM)', id: '7w4v59' },
    basics: { defaultMessage: 'Basics', id: 'itC9lG' },
    grafanaDashboard: { defaultMessage: 'Grafana dashboard', id: '2zi2Yj' },
    managedResources: { defaultMessage: 'Managed resource groups', id: 'yilQrD' },
    connectors: { defaultMessage: 'Connectors', id: '2mMJRv' },
    identity: { defaultMessage: 'Identity', id: 'tShbyC' },
    knowledgeBase: { defaultMessage: 'Knowledge base', id: 'tLYOnZ' },
    dataKnowledgeSpace: { defaultMessage: 'Data knowledge space', id: '5U04OG' },
    usage: { defaultMessage: 'Agent consumption', id: 'p7xkho' },
    sessionInsights: { defaultMessage: 'Session insights', id: 'CQ0CLu' },
    support: { defaultMessage: 'Support + troubleshooting', id: 'NN4zut' },
    fileSource: { defaultMessage: 'File Source', id: 'CJ6tzL' },
    dataSource: { defaultMessage: 'Data Source', id: 'uudb6D' },
    subAgents: { defaultMessage: 'Subagents', id: 'lQmkhq' },
    mcpServers: { defaultMessage: 'MCP servers', id: 'K9q4Xw' },
    permissions: { defaultMessage: 'Permissions', id: 'SFuk1v' },
    upgradeChannel: { defaultMessage: 'Early access to features', id: 'uBPOyN' },
    upgradeChannelDescription: { defaultMessage: 'Choose the channel for receiving agent updates', id: 'hfqoIO' },
    upgradeChannelStable: { defaultMessage: 'Current status: Stable - Receives updates once a month', id: 'rPdyc5' },
    upgradeChannelPreview: {
        defaultMessage: 'Enable the preview channel to test out new features early, however they may be less stable.',
        id: 'pU0SQw',
    },
    upgradeChannelCurrentStatus: { defaultMessage: 'Current status', id: 'pFm27r' },
    upgradeChannelUpdatingTitle: { defaultMessage: 'Updating upgrade channel', id: 'ppARxn' },
    upgradeChannelUpdatingDescription: { defaultMessage: 'Updating upgrade channel to {channel}', id: 'ZCA/Ga' },
    upgradeChannelUpdateSuccess: { defaultMessage: 'Upgrade channel updated to {channel} successfully', id: 'VpsD7s' },
    upgradeChannelUpdateFailed: { defaultMessage: 'Failed to update upgrade channel', id: 'aRUGFu' },
});

export const GrafanaDashboardResources = defineMessages({
    newValueDisplay: { defaultMessage: ' (new)', id: 'aaAabe' },
    region: { defaultMessage: 'Region', id: 'lnaWo/' },
    selectRegion: { defaultMessage: 'Select region', id: 'tshYzs' },
    createNew: { defaultMessage: 'Create new', id: '5WK7jL' },
    grafanaResourceName: { defaultMessage: 'Azure Managed Grafana resource name', id: 'JAtEuU' },
    azureMonitorWorkspaceResourceName: { defaultMessage: 'Azure Monitor Workspace resource name', id: '2hB9SP' },
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
    userAssignedManagedIdentity: { defaultMessage: 'User-assigned Managed Identity', id: '6S2zmX' },
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
    uniqueAmwResourceNameError: {
        defaultMessage: 'Azure Monitor Workspace resource must be unique within the resource group.',
        id: 'riEsC7',
    },
    invalidGrafanaResourceNameError: {
        defaultMessage:
            'The name must begin with a letter, end with a letter or number, and contain only letters, numbers, and hyphens. It must be 2 to 23 characters long.',
        id: 'WuZcUT',
    },
    invalidAmwResourceNameError: {
        defaultMessage: 'Only alphanumeric characters and dashes are allowed, and the value must be 3-44 characters long.',
        id: 'RH8rNM',
    },
    enterResourceName: { defaultMessage: 'Enter resource name', id: '3DzXFS' },
});

export const UsageResources = defineMessages({
    updateAllocationTitle: {
        defaultMessage: 'Update AAU allocation',
        id: 'rU0caB',
    },
    updateAllocationInProgressDescription: {
        defaultMessage: 'Updating AAU allocation from {oldValue} to {newValue}.',
        id: 'QChoto',
    },
    updateAllocationSuccessDescription: {
        defaultMessage: 'AAU allocation updated from {oldValue} to {newValue} successfully.',
        id: '/TZk1G',
    },
    updateAllocationFailedDescription: {
        defaultMessage: 'Failed to update AAU allocation. Error: {errorMessage}',
        id: 'XMUseu',
    },
    description: {
        defaultMessage:
            'Azure SRE Agent billing is measured in Azure agent units (AAU). Monthly AAU billing reflects both the fixed always-on flow and variable usage from active flow. You can increase or decrease the active flow AAU allocation at any time.',
        id: 'TO55dt',
    },
    descriptionLinkText: {
        defaultMessage: 'Learn more about how to calculate cost',
        id: '8dTxSy',
    },
    changeAAUAllocationText: {
        defaultMessage: 'Change AAU allocation',
        id: 'fr6Gnj',
    },
    monthlyActiveFlowAAUsLabel: {
        defaultMessage: 'Monthly active flow AAUs',
        id: 'SoBWeV',
    },
    monthlyAAULimitLabel: {
        defaultMessage: 'Monthly AAU limit:',
        id: 'AHQdRU',
    },
    billingDescription: {
        defaultMessage: 'Always-on flow + {count, number} active flow AAUs',
        id: 'a1UV+9',
    },
    activeFlowResetMessage: {
        defaultMessage: 'Active flow AAUs reset in {days, plural, =0 {today} one {# day} other {# days}}',
        id: 'uLmZLS',
    },
    totalActiveFlowConsumptionTitle: {
        defaultMessage: 'Total active flow consumption',
        id: 'Pa1S0J',
    },
    consumptionAAUUsageLabel: {
        defaultMessage: 'Consumption AAU usage',
        id: 'dWSIqT',
    },
    dailyActiveFlowConsumptionTitle: {
        defaultMessage: 'Daily active flow consumption',
        id: '8AMq3a',
    },
    aauConsumptionLegendText: {
        defaultMessage: 'AAU Consumption',
        id: 'QOSRgI',
    },
    dialogDescription: {
        defaultMessage:
            'You can increase or decrease active flow AAUs as needed. If the agent reaches the active flow limit, it continues running only in the always-on flow but is unavailable for chat and actions.',
        id: '5Y6lHe',
    },
    usageLimitInputFieldInfo: {
        defaultMessage: 'Maximum 200,000 AAUs',
        id: 'LSqcaU',
    },
    usageLimitErrorMessage: {
        defaultMessage: 'The maximum AAU limit is 200,000',
        id: '/5UY6N',
    },
    usageLimitWarningMessage: {
        defaultMessage:
            'The current consumption exceeds the new allocation, which will take effect next month. Until then, the agent will run only in the always-on flow. Chat and actions will be unavailable.',
        id: 'AdnFH8',
    },
    dataLoadErrorTitle: {
        defaultMessage: 'Failed to load data',
        id: 'iJ+eDu',
    },
    dataLoadErrorDescription: {
        defaultMessage: 'There was an error loading the usage data. Please try again later.',
        id: 'u04DEJ',
    },
});

export const FeedbackResources = defineMessages({
    provideAgentFeedback: { defaultMessage: 'Provide agent feedback', id: 'P7MNl8' },
    provideResponseFeedback: { defaultMessage: 'Provide response feedback', id: 'omHElC' },
    submitFeedbackTitle: { defaultMessage: 'Submit feedback to Microsoft', id: '+FoBRs' },
    generalFeedbackPlaceholder: {
        defaultMessage: 'Give as much detail as you can, but do not include any personal information.',
        id: 'csu0rb',
    },
    threadFeedbackPlaceholder: {
        defaultMessage: `What didn't you like about the agent's response? Your feedback helps us make the agent even better.`,
        id: '61e5yQ',
    },
    feedbackContactMe: { defaultMessage: "It's OK to contact me about my feedback.", id: 'E396gv' },
    feedbackPrivacyStatement: {
        defaultMessage: 'Data usage, customer rights, and privacy statement if needed. We got it covered.',
        id: 'OV0MpG',
    },
});

export const GithubIssueResources = defineMessages({
    createGithubIssueTitle: { defaultMessage: 'Create GitHub issue with SRE agent team', id: 'A+4geD' },
    createGithubIssueLinkText: {
        defaultMessage: 'Open GitHub issue page with pre-filled information above',
        id: '3SsYXO',
    },
    titleError: {
        defaultMessage: 'Title is required.',
        id: 'PZvrnY',
    },
    issueDescriptionError: {
        defaultMessage: 'Issue description is required.',
        id: '3k7JQw',
    },
    titleField: {
        defaultMessage: 'Title',
        id: '9a9+ww',
    },
    issueDescriptionField: {
        defaultMessage: 'Issue Description',
        id: 'HJ7ZeR',
    },
    threadIdField: {
        defaultMessage: 'Thread ID',
        id: 'ggVnjB',
    },
    stepsToReproduceField: {
        defaultMessage: 'Steps to Reproduce',
        id: 'G6XRkU',
    },
    expectedBehaviorField: {
        defaultMessage: 'Expected Behavior',
        id: '7Rnjmx',
    },
    actualBehaviorField: {
        defaultMessage: 'Actual Behavior',
        id: 'LuHM0/',
    },
    titlePrefix: {
        defaultMessage: 'Issue',
        id: 'ryAzxy',
    },
    issueDescriptionPlaceholder: {
        defaultMessage: 'Briefly describe the problem or request.',
        id: 'UImEMW',
    },
    threadIdPlaceholder: {
        defaultMessage: 'Paste the thread ID from the SRE Agent portal. (e.g., 50f7521d-dfee-487e-9188-5abdc8adde91)',
        id: 'J0rs+f',
    },
    stepsToReproducePlaceholder: {
        defaultMessage: '1. Describe the action you took\n2. Mention the resource or service involved',
        id: 'QYiU4f',
    },
    expectedBehaviorPlaceholder: {
        defaultMessage: 'What should happen?',
        id: 'zRv1ks',
    },
    actualBehaviorPlaceholder: {
        defaultMessage: 'What actually happened?',
        id: 'CQisjK',
    },
    githubImageAltText: {
        defaultMessage: 'GitHub logo',
        id: 'oIm1Fb',
    },
});

export const GraphResources = defineMessages({
    resourceSelectorDescription: {
        defaultMessage:
            "This map shows how your application's resources are connected across multiple resource groups, regions, and subscriptions. The agent analyzes these resources and organizes them into a core application group based on the primary resource.",
        id: '0G14fL',
    },
    visualView: { defaultMessage: 'Visual view', id: 'Ua0Vpv' },
    gridView: { defaultMessage: 'Grid view', id: 'N+sJfO' },
    tableHeaderName: { defaultMessage: 'Resource name', id: 'eqYdSS' },
    tableHeaderResourceType: { defaultMessage: 'Resource type', id: 'WHleoJ' },
    tableHeaderRepositoryConnection: { defaultMessage: 'Repository connection', id: 'FLm/x4' },
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
    uses: { defaultMessage: 'Uses', id: 'Vb+qQ4' },
    usesAction: { defaultMessage: 'Uses (Action)', id: 'VUVCb1' },
    usesTrigger: { defaultMessage: 'Uses (Trigger)', id: '1389z+' },
    usesTriggerAction: { defaultMessage: 'Uses (Trigger, Action)', id: 'QR4/mV' },
});

export const ResourceInfoResources = defineMessages({
    name: { defaultMessage: 'Name', id: 'HAlOn1' },
    type: { defaultMessage: 'Type', id: '+U6ozc' },
    close: { defaultMessage: 'Close', id: 'rbrahO' },
    dashboard: { defaultMessage: 'Dashboard', id: 'hzSNj4' },
    dashboardLinkText: { defaultMessage: 'Go to Azure Managed Grafana', id: 'SAINuE' },
    grafanaLogo: { defaultMessage: 'Grafana logo', id: 'mzRg+7' },
    repositoryConnection: { defaultMessage: 'Repository connection', id: 'FLm/x4' },
    authorizeRepositoryAccess: { defaultMessage: 'Authorize repository access', id: 'wru3Di' },
    connectedApis: { defaultMessage: 'Connected APIs', id: 'VKbBsK' },
    apimBackendEndpoint: { defaultMessage: 'Backend Endpoint', id: 'qqolQu' },
    armResourceId: { defaultMessage: 'Azure Resource Id', id: 'zfMgIf' },
    connectRepository: { defaultMessage: 'Connect repository', id: '1fNFGt' },
    linkRepositoryToResource: { defaultMessage: 'Link repository to resource', id: 'BV3Mir' },
    repositoryUrl: { defaultMessage: 'Repository URL', id: 'AA/tRJ' },
    repositoryLongUrlPlaceholder: {
        defaultMessage:
            'https://github.com/owner/repo-name or https://dev.azure.com/organization/project/_git/repo or https://organization.visualstudio.com/project/_git/repository-name',
        id: 'EqwyQ/',
    },
    repositoryUrlErrorMessage: {
        defaultMessage:
            'Repository URL must be like the following for GitHub: https://github.com/owner/repo-name or for Azure DevOps: https://dev.azure.com/organization/project/_git/repository-name or https://organization.visualstudio.com/project/_git/repository-name',
        id: 'dYwe9B',
    },
    connecting: { defaultMessage: 'Connecting...', id: '5y2qWO' },
    annotation: { defaultMessage: 'Annotation', id: 'dQtJBl' },
    editAnnotation: { defaultMessage: 'Edit annotation', id: 'Qfec1M' },
    addAnnotation: { defaultMessage: 'Add annotation', id: 'aORQjS' },
    addAnnotationDescription: {
        defaultMessage: 'Add a description about this resource to help the agent with groupings and insights.',
        id: '9VUlhV',
    },
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
    noPermissionAuthorizeRepositoryAccess: { defaultMessage: 'You do not have permission to authorize repository access.', id: '5CX32Z' },
    noPermissionAddAnnotation: { defaultMessage: 'You do not have permission to add or edit annotations.', id: 'IbIk/K' },
});

export const AppHealth = defineMessages({
    unhealthy: { defaultMessage: 'Unhealthy', id: 'YdXbbC' },
    healthy: { defaultMessage: 'Healthy', id: 'TIDNOO' },
    degraded: { defaultMessage: 'Degraded', id: 'VQDmmK' },
    reportUnhealthyNode: { defaultMessage: 'Report unhealthy node', id: 'YE+vjH' },
    sendingReport: { defaultMessage: 'Sending a report...', id: '5GUtRJ' },
});

export const WelcomeResources = defineMessages({
    initialWelcomeMessagePt1: {
        defaultMessage:
            "I'm your Azure SRE Agent and here to help you monitor your resources, investigate incidents, automate responses, and ensure your systems follow best practices for reliability, performance, and security.",
        id: 'aXGUvh',
    },
    initialWelcomeMessagePt2: {
        defaultMessage: "I'm currently gathering information about your app and analyzing your resources, which might take a few minutes. ",
        id: '8QX1EO',
    },
    finishedAnalyzingResources: {
        defaultMessage:
            "I analyzed your application's resources across multiple resource groups, regions, and subscriptions. I then generated a resource map of the connections I identified based on the primary resource. Based on what I learned, I also put together some suggested prompts that focus on your resources.",
        id: '0KB3lG',
    },
    resourceAnalysis: { defaultMessage: 'Resource analysis', id: 'PWmGlA' },
    suggestedPromptsForYourResources: { defaultMessage: 'Suggested prompts for your resources', id: '3RvKcp' },
    learnMoreAboutPrompts: { defaultMessage: 'Learn more about prompts', id: 'OLJJDc' },
    primaryResourceType: { defaultMessage: 'Primary resource type', id: 'nQpONp' },
});

export const DailyReportResources = defineMessages({
    // Report title and header
    resourceReport: { defaultMessage: 'Resource Report', id: '6feu1e' },

    // Overview cards
    repositoryInsights: { defaultMessage: 'Repository insights', id: 'oY4j0R' },
    incidentsSummary: { defaultMessage: 'Incidents summary', id: 'zpkKsD' },
    coreAppGroupHealthPerformance: { defaultMessage: 'Core application group health + performance', id: 'SPdP4q' },
    codeOptimizations: { defaultMessage: 'Code optimizations', id: 'fHE7i7' },
    codeOptimizationInsights: { defaultMessage: 'Code optimization insights', id: 'DF0TNm' },

    // Severity levels
    critical: { defaultMessage: 'Critical', id: '2pzTGC' },
    high: { defaultMessage: 'High', id: 'AxMhQr' },
    moderate: { defaultMessage: 'Moderate', id: 'OlIql8' },
    low: { defaultMessage: 'Low', id: '477I0g' },

    // Incident statuses
    active: { defaultMessage: 'Active', id: '3a5wL8' },
    mitigated: { defaultMessage: 'Mitigated', id: 'dnXgff' },
    resolved: { defaultMessage: 'Resolved', id: 'W6nSYE' },

    // Health statuses
    unhealthy: { defaultMessage: 'Unhealthy', id: 'YdXbbC' },
    degraded: { defaultMessage: 'Degraded', id: 'VQDmmK' },
    healthy: { defaultMessage: 'Healthy', id: 'TIDNOO' },

    // Section titles
    actionSummary: { defaultMessage: 'Action summary', id: 'iyTlSa' },
    unhealthyCoreAppGroups: { defaultMessage: 'Unhealthy core application groups', id: 'hfACn+' },
    degradedCoreAppGroups: { defaultMessage: 'Degraded core application groups', id: 'CZ6s9k' },
    healthyCoreAppGroups: { defaultMessage: 'Healthy core application groups', id: 'j5QthH' },

    // Resource metrics
    availability: { defaultMessage: 'Availability', id: 'hOxIeP' },
    cpuUsage: { defaultMessage: 'CPU usage', id: '+DBMRK' },
    memory: { defaultMessage: 'Memory', id: 'dVx3yz' },

    // Code Optimizations metrics
    codeOptimizationsCpu: { defaultMessage: 'CPU usage insights', id: 'FGP0Kd' },
    codeOptimizationsMemory: { defaultMessage: 'Memory usage insights', id: 'vB/Cv7' },
    codeOptimizationsBlocking: { defaultMessage: 'Blocking usage insights', id: 'vsQnBO' },
    codeOptimizationsTotal: { defaultMessage: 'Total insights', id: '0pysPM' },
    codeOptimizationsRecommendations: { defaultMessage: 'recommendations', id: '6Lox/s' },
    codeOptimizationsType: { defaultMessage: 'Type', id: '+U6ozc' },
    codeOptimizationsImpactValue: { defaultMessage: 'Peak Usage', id: 'V4yeoh' },
    codeOptimizationsIssue: { defaultMessage: 'Performance Issue', id: 'oQJEFn' },
    codeOptimizationsNoRecommendationsMessage: { defaultMessage: 'No code optimization recommendations detected.', id: 'HFuyFh' },
    codeOptimizationsLearnMore: { defaultMessage: 'Learn more about code optimizations', id: 'txLniB' },
    codeOptimizationsGetMoreDetails: { defaultMessage: 'Get more details about these insights', id: 'uva2Xo' },

    // App group resource info
    coreAppGroupResourceName: { defaultMessage: 'Core application group resource name', id: 'ibf4+R' },
    coreAppGroupType: { defaultMessage: 'Core application group type', id: 'yoK+he' },

    // Action items
    priority: { defaultMessage: 'Priority', id: '8lCjAM' },
    urgency: { defaultMessage: 'Urgency', id: 'KXyWfi' },

    // Empty states
    repositoryAlertsFound: { defaultMessage: 'repository alerts found', id: 'GroBmn' },
    incidentsReported: { defaultMessage: 'incidents reported', id: 'xDYr9m' },
    resourcesAvailable: { defaultMessage: 'resources available in this period', id: 'rBPlLc' },
    actions: { defaultMessage: 'actions', id: 'CvWvf+' },

    // Chart and data
    noHistoricalDataAvailable: { defaultMessage: 'No historical data available', id: 'Mof2Rv' },
    insufficientDataPoints: { defaultMessage: 'Insufficient data points for chart', id: 'xePyxp' },
    resourceMetrics: { defaultMessage: 'Resource Metrics', id: 'JxjlUE' },

    // Incident details
    impact: { defaultMessage: 'Impact', id: 'W2JBdp' },
    incidentId: { defaultMessage: 'Incident ID', id: 'MB9ceM' },
    created: { defaultMessage: 'Created', id: 'ORGv1Q' },
    duration: { defaultMessage: 'Duration', id: 'IuFETn' },
    goToIncidentThread: { defaultMessage: 'Go to incident thread', id: '7fDYaV' },

    // Security
    description: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    repository: { defaultMessage: 'Repository', id: 'UxeJFE' },
    state: { defaultMessage: 'State', id: 'ku+mDU' },

    // Time formats
    notAvailable: { defaultMessage: 'N/A', id: 'PW+sL4' },
});

export const AgentModeResources = defineMessages({
    agentMode: { defaultMessage: 'Agent mode', id: 'VtBCjG' },
    updateAgentModeFailureDescription: {
        defaultMessage: 'Failed to update the agent mode.',
        id: 'wVpXzO',
    },
    fetchAgentModeFailureMessage: {
        defaultMessage: 'Failed to fetch agent mode',
        id: 'CJEYfo',
    },
    agentModeTooltip: {
        defaultMessage: 'Change agent mode for this thread',
        id: '29xePt',
    },
    agentModeNoPermission: {
        defaultMessage: 'You do not have permission to change the thread mode from {mode}',
        id: 'UKMPBH',
    },
});

export const GenericErrorResources = defineMessages({
    failedToCreateScheduledTask: {
        defaultMessage: 'Failed to create scheduled task. Please try again.',
        id: 'cEBkPF',
    },
    unexpectedError: {
        defaultMessage: 'An unexpected error occurred.',
        id: 'Xkdnen',
    },
    failedToLinkRepository: {
        defaultMessage: 'Failed to link repository',
        id: 'nxvTtg',
    },
    failedToParseDailyReport: {
        defaultMessage: 'Failed to parse daily report:',
        id: 'tCrMHt',
    },
    invalidThreadDataFromStream: {
        defaultMessage: 'Invalid thread data received from streaming message',
        id: '77t9cY',
    },
    failedToCheckAgentMemoryStatus: {
        defaultMessage: 'Failed to check agent memory status, falling back to disabled:',
        id: 'yeJPGA',
    },
    justNow: {
        defaultMessage: 'Just now',
        id: 'bxv59V',
    },
    close: { defaultMessage: 'Close', id: 'rbrahO' },
    closePanel: { defaultMessage: 'Close panel', id: 'RAjqKb' },
    resizeDrawer: { defaultMessage: 'Resize drawer', id: 'Gl8fnJ' },
    selectTask: { defaultMessage: 'Select task', id: '0fhwgp' },
    takeScreenshot: { defaultMessage: 'Take Screenshot', id: 'KvZ6B9' },
    moreOptions: { defaultMessage: 'More options', id: 'IzCVhG' },
    dismissNotification: { defaultMessage: 'Dismiss notification', id: 'pe7UAe' },
    unknownError: { defaultMessage: 'Unknown error', id: 'qDwvZ4' },
});

export const ServiceNowResources = defineMessages({
    serviceNowEndpoint: { defaultMessage: 'ServiceNow endpoint', id: 'OxtrmO' },
    serviceNowUsername: { defaultMessage: 'Username', id: 'JCIgkj' },
    serviceNowPassword: { defaultMessage: 'Password', id: '5sg7KC' },
    serviceNowInstanceName: { defaultMessage: 'Instance name', id: 'fblXmJ' },
    changeKey: { defaultMessage: 'Change credentials', id: '0q6E9l' },
    description: {
        defaultMessage:
            'Connect to ServiceNow with your instance endpoint and credentials. This allows the agent to manage incidents and tickets in your ServiceNow environment.',
        id: 'Nfo3YG',
    },
    disconnectConfirmationTitle: { defaultMessage: 'Disconnect ServiceNow?', id: 'Jm+zxF' },
    disconnectConfirmationMessage: {
        defaultMessage:
            'This will permanently delete the connection to ServiceNow. The agent will no longer be able to manage tickets. Are you sure you want to disconnect?',
        id: 'n0JssX',
    },
    changePlatformConfirmationTitle: { defaultMessage: 'Disconnect ServiceNow?', id: 'Jm+zxF' },
    changePlatformConfirmationMessage: {
        defaultMessage:
            'To change the incident platform, you need to disconnect from ServiceNow. The agent will no longer manage tickets. Are you sure you want to disconnect?',
        id: 'h+YOYh',
    },
    notConnectedMessage: { defaultMessage: 'ServiceNow is not connected.', id: 'f0Luxo' },
    connectedMessage: { defaultMessage: 'ServiceNow is connected.', id: 'Ep74wA' },
    connectedMessageWithoutHandlers: {
        defaultMessage: 'ServiceNow connected. Your next step is to set up incident response plans.',
        id: 'ehbsXg',
    },
    connectingMessage: { defaultMessage: 'Connecting to ServiceNow ...', id: 'HvIalp' },
    quickstartHandlerDescription: {
        defaultMessage: 'Add a default incident response plan for the agent to use for High priority incidents.',
        id: 'SXJ1c9',
    },
    connectionFailureMessage: {
        defaultMessage: 'Connection to ServiceNow failed. Please check your configuration and try again.',
        id: 'Cwgo05',
    },
    priorityCritical: { defaultMessage: 'Critical', id: '2pzTGC' },
    priorityHigh: { defaultMessage: 'High', id: 'AxMhQr' },
    priorityModerate: { defaultMessage: 'Moderate', id: 'OlIql8' },
    priorityLow: { defaultMessage: 'Low', id: '477I0g' },
    priorityPlanning: { defaultMessage: 'Planning', id: '99OdS3' },
});

export const KnowledgeBaseResources = defineMessages({
    fileUploadTitle: {
        defaultMessage: 'Add a knowledge source',
        id: 'a8czK7',
    },
    fileUploadDescription: {
        defaultMessage:
            'Add a file or connect an external data source, such as a service or repository, to help the agent generate more informed responses.',
        id: 'Z+uDP9',
    },
    fileUploadLinkDescription: {
        defaultMessage: 'Learn more about knowledge sources',
        id: 's00xPm',
    },
    filesRejected: {
        defaultMessage: '{count} document(s) rejected ({fileNames}). Only .md and .txt files are allowed.',
        id: 'SXEeds',
    },
    uploadFailed: {
        defaultMessage: 'Upload failed: {error}',
        id: 'R1i9Gl',
    },
    deleteFailed: {
        defaultMessage: 'Delete failed: {error}',
        id: 'Mvqep7',
    },
    bulkDeleteFailed: {
        defaultMessage: 'Bulk delete failed: {error}',
        id: '59z+Aj',
    },
    someFilesFailedToDelete: {
        defaultMessage: 'Some documents failed to delete: {failedFiles}',
        id: 'kbDtEK',
    },
    // File upload UI text
    addMore: {
        defaultMessage: 'Add More',
        id: '3jPMQI',
    },
    uploadFiles: {
        defaultMessage: 'Add a file',
        id: 'f0Sf6g',
    },
    // Uploaded files list
    fileName: {
        defaultMessage: 'File Name',
        id: 'xHpwlo',
    },
    delete: {
        defaultMessage: 'Delete',
        id: 'K3r6DQ',
    },
    deleting: {
        defaultMessage: 'Deleting...',
        id: 'noZdV2',
    },
    deleteSelected: {
        defaultMessage: 'Delete Selected ({count})',
        id: 'KoJ1C+',
    },
    filesSelected: {
        defaultMessage: '{count, plural, one {{count} document selected} other {{count} documents selected}}',
        id: 'NsLW3l',
    },
    noFilesUploaded: {
        defaultMessage: 'No documents uploaded yet',
        id: '1HLt0U',
    },
    searchForFiles: {
        defaultMessage: 'Search for files...',
        id: 'JlJ8dZ',
    },
    acceptedFileTypes: {
        defaultMessage: 'Accepted file types: .md, .txt',
        id: 'sQkhYk',
    },
    dragAndDropFiles: {
        defaultMessage: 'Drag and drop files here or',
        id: 'j3bwJQ',
    },
    browseFiles: {
        defaultMessage: 'Browse Files',
        id: 'M88YzU',
    },
    selectedFiles: {
        defaultMessage: 'Selected Files',
        id: '4upwsn',
    },
    removeAll: {
        defaultMessage: 'Remove All',
        id: 'aOVqnW',
    },
    remove: {
        defaultMessage: 'Remove',
        id: 'G/yZLu',
    },
    addMoreFiles: {
        defaultMessage: 'Add More Files',
        id: 'gHCL4X',
    },
    uploading: {
        defaultMessage: 'Uploading...',
        id: 'JEsxDw',
    },
    uploadingFiles: {
        defaultMessage: 'Uploading Files',
        id: 'XaSHE5',
    },
    uploadingFilesMessage: {
        defaultMessage: 'Please wait while your files are being uploaded...',
        id: 'crWYMh',
    },
    // Additional strings
    addFile: { defaultMessage: 'Add file', id: 'sXiGbo' },
    filesUploadedSuccessfully: {
        defaultMessage: 'Files uploaded successfully',
        id: 'ag1Rum',
    },
    deletingFiles: {
        defaultMessage: 'Deleting Files',
        id: 'rXYf9o',
    },
    deletingFilesMessage: {
        defaultMessage: 'Please wait while your files are being deleted...',
        id: 'BEck3n',
    },
    filesDeletedSuccessfully: {
        defaultMessage: 'Files deleted successfully',
        id: 'sVDrdr',
    },
    refresh: {
        defaultMessage: 'Refresh',
        id: 'rELDbB',
    },
    selectedFilesTable: {
        defaultMessage: 'Selected files table',
        id: '9XASFm',
    },
    removeFile: {
        defaultMessage: 'Remove file',
        id: 'hgAzMV',
    },
    folder: {
        defaultMessage: 'Folder',
        id: 'ukQpDs',
    },
    filesStoredIn: {
        defaultMessage: 'Files will be stored in the agent.',
        id: 'NIrBo5',
    },
    deleteFile: {
        defaultMessage: 'Delete file?',
        id: '7bDMaW',
    },
    deleteFiles: {
        defaultMessage: 'Delete {count} files?',
        id: 'RK9Ag4',
    },
    deleteFileMessage: {
        defaultMessage:
            'This will permanently delete the file from the knowledge sources the agent uses. Are you sure you want to delete this file?',
        id: 'rBhkvO',
    },
    deleteFilesMessage: {
        defaultMessage:
            'This will permanently delete {count} files from the knowledge sources the agent uses. Are you sure you want to delete these files?',
        id: '9+dRta',
    },
    addFileAction: {
        defaultMessage: 'Add a file',
        id: 'f0Sf6g',
    },
    noSearchResults: {
        defaultMessage: 'No files match your search',
        id: 'hzElIu',
    },
    noSearchResultsDescription: {
        defaultMessage: 'Try different search terms or clear your search to see all files.',
        id: 'NSWNPa',
    },
    dragFilesHere: {
        defaultMessage: 'Drag files here or',
        id: 'gDVRX+',
    },
    browseForFiles: {
        defaultMessage: 'browse for files',
        id: 'FvtGBp',
    },
    supportedFileFormats: {
        defaultMessage: 'Supported file formats: .md and .txt',
        id: 'YsFdGq',
    },
    maximumFileSize: {
        defaultMessage: 'Maximum file size: 100MB',
        id: '0sA1BF',
    },
    knowledgeBase: {
        defaultMessage: 'Knowledge Base',
        id: 'EbNaDn',
    },
});

export const DailyReportsTabResources = defineMessages({
    selectADate: { defaultMessage: 'Select a date', id: '7qOQpv' },
    loadingReportsAriaLabel: { defaultMessage: 'Loading daily reports', id: 'Hs02Tq' },
});

export const ScheduledTasksResources = defineMessages({
    scheduledTasks: { defaultMessage: 'Scheduled tasks', id: 'sy7vzf' },
    tasks: { defaultMessage: 'Tasks', id: 'yhU1et' },
    scheduledTasksDescription: {
        defaultMessage: 'Create and manage scheduled tasks to automatically run agent actions at regular intervals.',
        id: '2HM8tn',
    },
    learnMoreAboutScheduledTasks: { defaultMessage: 'Learn more about scheduled tasks', id: 'iPhHGC' },
    goToScheduledTasks: {
        defaultMessage: 'Go to Scheduled Tasks',
        id: 'zJFez9',
    },
    activeTasks: { defaultMessage: 'Active tasks', id: 'wGcWbo' },
    totalTasks: { defaultMessage: 'Total tasks', id: 'Px6I1N' },
    totalRuns: { defaultMessage: 'Total runs', id: 'g4Dsk6' },

    // Data grid
    name: { defaultMessage: 'Name', id: 'HAlOn1' },
    taskStatus: { defaultMessage: 'Task status', id: 'CBHDb1' },
    schedule: { defaultMessage: 'Schedule', id: 'hGQqkW' },
    createdBy: { defaultMessage: 'Created by', id: 'p4mBmL' },
    lastRun: { defaultMessage: 'Last run', id: 'mHep2R' },
    nextRun: { defaultMessage: 'Next run', id: '3yurtF' },
    completedRuns: { defaultMessage: 'Completed runs', id: 'I9W7ey' },

    // Toolbar
    createTask: { defaultMessage: 'Create task', id: '7X1tNR' },
    updateList: { defaultMessage: 'Update list', id: '8Vd2Kv' },
    turnOn: { defaultMessage: 'Turn on', id: 'npvxpr' },
    turnOff: { defaultMessage: 'Turn off', id: 'XZ+Fx6' },
    runTaskNow: { defaultMessage: 'Run task now', id: 'Y7RxDE' },
    lastUpdated: { defaultMessage: 'Last updated', id: '0ICwq5' },
    on: { defaultMessage: 'On', id: 'Zh+5A6' },
    off: { defaultMessage: 'Off', id: 'OvzONl' },
    ended: { defaultMessage: 'Ended', id: 'TP/cMX' },
    searchByScheduledTask: { defaultMessage: 'Search by scheduled task', id: 'J8uEym' },

    // Form
    createScheduledTask: { defaultMessage: 'Create scheduled task', id: 'dJqE3e' },
    editScheduledTask: { defaultMessage: 'Edit scheduled task', id: '6ZDt71' },
    taskName: { defaultMessage: 'Task name', id: 'wbwhbH' },
    taskNamePlaceholder: { defaultMessage: 'Enter a scheduled task name', id: '+pmvA0' },
    responseSubAgent: { defaultMessage: 'Response subagent', id: 'GMhzu1' },
    responseSubAgentPlaceholder: { defaultMessage: 'Select an agent', id: 'ipsqxO' },
    taskDetails: { defaultMessage: 'Task details', id: 'HADwwN' },
    taskDetailsPlaceholder: { defaultMessage: 'Enter task details', id: 'cBcrgh' },
    taskDetailsTip: {
        defaultMessage:
            'Keep the description brief and explain the task’s actions. Include necessary resource identifiers, specify time-sensitive information, and define success/failure signals or required output formats.',
        id: '4Qhg3L',
    },
    frequency: { defaultMessage: 'Frequency', id: 'vAW30j' },
    daily: { defaultMessage: 'Daily', id: 'zxvhnE' },
    weekly: { defaultMessage: 'Weekly', id: '/clOBU' },
    customCron: { defaultMessage: 'Custom cron', id: 'G/YsSs' },
    dayOfWeek: { defaultMessage: 'Day of week', id: 'gD6r6x' },
    monday: { defaultMessage: 'Monday', id: 'azMsfM' },
    tuesday: { defaultMessage: 'Tuesday', id: 'YAgYL6' },
    wednesday: { defaultMessage: 'Wednesday', id: 'lxblgl' },
    thursday: { defaultMessage: 'Thursday', id: 'qAhUUO' },
    friday: { defaultMessage: 'Friday', id: 'QrihTZ' },
    saturday: { defaultMessage: 'Saturday', id: 'WMNHPh' },
    sunday: { defaultMessage: 'Sunday', id: 'mJR06P' },
    monthly: { defaultMessage: 'Monthly', id: 'wYsv4Z' },
    dayOfMonth: { defaultMessage: 'Day of month', id: 'oMsZ4g' },
    timeOfDay: { defaultMessage: 'Time of day', id: 'h0W8DM' },
    am: { defaultMessage: 'AM', id: 'N0d5pM' },
    pm: { defaultMessage: 'PM', id: 'qxlJil' },
    cronExpressionUTC: { defaultMessage: 'Cron expression (UTC)', id: '9jut5b' },
    cronExpressionPlaceholder: { defaultMessage: 'e.g. 0 0 * * *', id: 'g4II+z' },
    invalidCronExpression: {
        defaultMessage: 'Invalid cron expression. Please use format: minute hour day month day-of-week',
        id: 'TZWmvi',
    },
    everyHours: { defaultMessage: '{hours, plural, =1 {Every hour} other {Every # hours}}', id: 'IIK54g' },
    everyMinutes: { defaultMessage: '{minutes, plural, =1 {Every minute} other {Every # minutes}}', id: 'ZolojY' },
    dailyAt: { defaultMessage: 'Daily at {time}', id: '726zma' },
    weeklyOn: { defaultMessage: 'Weekly on {day} at {time}', id: '5SH2BD' },
    monthlyOn: { defaultMessage: 'Monthly on day {dayOfMonth} at {time}', id: '2ep4et' },
    advancedSettings: { defaultMessage: 'Advanced settings', id: 'zhoVUT' },
    timeZone: { defaultMessage: 'Time zone', id: 'IcUakl' },
    startOn: { defaultMessage: 'Start on', id: 'ZOO+kP' },
    repeatUntil: { defaultMessage: 'Repeat until', id: 'ts7YvN' },
    repeatUntilPlaceholder: { defaultMessage: 'End Date (Optional)', id: 'M4emJP' },
    repeatUntilValidationMessage: { defaultMessage: 'End date must be after start date.', id: 'hjtyTg' },
    messageGroupingForUpdates: { defaultMessage: 'Message grouping for updates', id: 'XV8dOu' },
    useSameThread: { defaultMessage: 'Use same thread', id: 'sI/fs2' },
    newThreadForEachRun: { defaultMessage: 'New thread for each run', id: 'WfXPv6' },
    setARunLimit: { defaultMessage: 'Set a run limit', id: 'tTVtNj' },
    setARunLimitPlaceholder: { defaultMessage: "Leave the field blank if there's no limit", id: '26Vq1R' },
    setARunLimitTooltip: { defaultMessage: 'The task will stop running after reaching this number', id: '9X3VEw' },

    // Operations and notifications
    createScheduledTaskNotificationTitle: { defaultMessage: 'Create scheduled task', id: 'dJqE3e' },
    createScheduledTaskNotificationInProgress: {
        defaultMessage: 'Creating scheduled task{name, select, undefined {} other { {name}}}',
        id: 'N75VIa',
    },
    createScheduledTaskNotificationSuccess: {
        defaultMessage: 'Scheduled task{name, select, undefined {} other { {name}}} is now active.',
        id: 'TKNqWx',
    },
    createScheduledTaskNotificationError: {
        defaultMessage: 'Failed to create scheduled task.{errorMessage, select, undefined {} other { {errorMessage}}}',
        id: 'R56nxF',
    },
    updateScheduledTaskNotificationTitle: { defaultMessage: 'Update scheduled task', id: 'w5FDGY' },
    updateScheduledTaskNotificationInProgress: {
        defaultMessage: 'Updating scheduled task{name, select, undefined {} other { {name}}}',
        id: '3/OG7/',
    },
    updateScheduledTaskNotificationSuccess: {
        defaultMessage: 'Scheduled task{name, select, undefined {} other { {name}}} has been updated.',
        id: 'SEdSoA',
    },
    updateScheduledTaskNotificationError: {
        defaultMessage: 'Failed to update scheduled task.{errorMessage, select, undefined {} other { {errorMessage}}}',
        id: '1/3fw+',
    },

    pauseScheduledTaskNotificationTitleSingle: {
        defaultMessage: 'Turn off scheduled task',
        id: 'YMjUWP',
    },
    pauseScheduledTaskNotificationTitleMultiple: {
        defaultMessage: 'Turn off scheduled tasks',
        id: 'yisTNp',
    },
    pauseScheduledTaskNotificationInProgressSingle: {
        defaultMessage: 'Turning off scheduled task{name, select, undefined {} other { {name}}}.',
        id: 'bMqcq2',
    },
    pauseScheduledTaskNotificationInProgressMultiple: {
        defaultMessage: 'Turning off scheduled tasks.',
        id: 'Uiw6Xo',
    },
    pauseScheduledTaskNotificationSuccessSingle: {
        defaultMessage: 'Scheduled task{name, select, undefined {} other { {name}}} is now off.',
        id: 'WUZAzC',
    },
    pauseScheduledTaskNotificationSuccessMultiple: {
        defaultMessage: 'Scheduled tasks are now off.',
        id: 'J3nUiY',
    },
    pauseScheduledTaskNotificationError: {
        defaultMessage: 'Failed to turn off scheduled task.{errorMessage, select, undefined {} other { {errorMessage}}}',
        id: 'bGIW7m',
    },

    resumeScheduledTaskNotificationTitleSingle: {
        defaultMessage: 'Turn on scheduled task',
        id: '73c47a',
    },
    resumeScheduledTaskNotificationTitleMultiple: {
        defaultMessage: 'Turn on scheduled tasks',
        id: 'IpS9Tn',
    },
    resumeScheduledTaskNotificationInProgressSingle: {
        defaultMessage: 'Turning on scheduled task{name, select, undefined {} other { {name}}}.',
        id: 'ODswb7',
    },
    resumeScheduledTaskNotificationInProgressMultiple: {
        defaultMessage: 'Turning on scheduled tasks.',
        id: 'Vi21gb',
    },
    resumeScheduledTaskNotificationSuccessSingle: {
        defaultMessage: 'Scheduled task{name, select, undefined {} other { {name}}} is now on.',
        id: 'zefL4U',
    },
    resumeScheduledTaskNotificationSuccessMultiple: {
        defaultMessage: 'Scheduled tasks are now on.',
        id: 'LjwVHP',
    },
    resumeScheduledTaskNotificationError: {
        defaultMessage: 'Failed to turn on scheduled task.{errorMessage, select, undefined {} other { {errorMessage}}}',
        id: 'KlWAix',
    },

    runScheduledTaskNotificationTitleSingle: {
        defaultMessage: 'Run scheduled task',
        id: 'dzy0ND',
    },
    runScheduledTaskNotificationTitleMultiple: {
        defaultMessage: 'Run scheduled tasks',
        id: 'SjeMma',
    },
    runScheduledTaskNotificationInProgressSingle: {
        defaultMessage: 'Running scheduled task{name, select, undefined {} other { {name}}}.',
        id: '5WNvJO',
    },
    runScheduledTaskNotificationInProgressMultiple: {
        defaultMessage: 'Running scheduled tasks.',
        id: 'dbXXhB',
    },
    runScheduledTaskNotificationSuccessSingle: {
        defaultMessage: 'Scheduled task{name, select, undefined {} other { {name}}} has been run.',
        id: 'AuUgE1',
    },
    runScheduledTaskNotificationSuccessMultiple: {
        defaultMessage: 'Scheduled tasks have been run.',
        id: 'pByJCZ',
    },
    runScheduledTaskNotificationError: {
        defaultMessage: 'Failed to run scheduled task.{errorMessage, select, undefined {} other { {errorMessage}}}',
        id: '1vVgdq',
    },

    deleteScheduledTaskConfirmationDescriptionSingle: {
        defaultMessage: 'Are you sure you want to delete this scheduled task? This action cannot be undone.',
        id: 'oDb8Xv',
    },
    deleteScheduledTaskConfirmationDescriptionMultiple: {
        defaultMessage: 'Are you sure you want to delete these scheduled tasks? This action cannot be undone.',
        id: 'yGVEST',
    },
    deleteScheduledTaskNotificationTitleSingle: {
        defaultMessage: 'Delete scheduled task',
        id: 'IYLPrk',
    },
    deleteScheduledTaskNotificationTitleMultiple: {
        defaultMessage: 'Delete scheduled tasks',
        id: 'M+AZNX',
    },
    deleteScheduledTaskNotificationInProgressSingle: {
        defaultMessage: 'Deleting scheduled task{name, select, undefined {} other { {name}}}.',
        id: 'iWNutw',
    },
    deleteScheduledTaskNotificationInProgressMultiple: {
        defaultMessage: 'Deleting scheduled tasks.',
        id: 'JSVEZ3',
    },
    deleteScheduledTaskNotificationSuccessSingle: {
        defaultMessage: 'Scheduled task{name, select, undefined {} other { {name}}} has been deleted.',
        id: 'S8Rsb6',
    },
    deleteScheduledTaskNotificationSuccessMultiple: {
        defaultMessage: 'Scheduled tasks have been deleted.',
        id: '8lQNdb',
    },
    deleteScheduledTaskNotificationError: {
        defaultMessage: 'Failed to delete scheduled task.{errorMessage, select, undefined {} other { {errorMessage}}}',
        id: '8H0PeE',
    },

    // Badges
    messageGroupingBadgeText: {
        defaultMessage: 'Message grouping:',
        id: '8MoJEj',
    },
    scheduleBadgeText: {
        defaultMessage: 'Schedule:',
        id: 'NmQwIl',
    },
    createdTimestampBadgeText: {
        defaultMessage: 'Created at:',
        id: 'i3P6ZD',
    },
    startTimestampBadgeText: {
        defaultMessage: 'Start:',
        id: 'xP3hmB',
    },

    // Executions
    startTime: { defaultMessage: 'Start time', id: '/zFP1/' },
    duration: { defaultMessage: 'Duration', id: 'IuFETn' },
    runStatus: { defaultMessage: 'Run status', id: 'WNTEQn' },
    threadName: { defaultMessage: 'Thread name', id: 'tiNK7U' },
    taskSummary: { defaultMessage: 'Task summary', id: 'spehs+' },
    downloadFile: { defaultMessage: 'Download file', id: 'k9qHCC' },
    executionSuccess: { defaultMessage: 'Success', id: 'xrKHS6' },
    executionFailed: { defaultMessage: 'Failed', id: 'vXCeIi' },
    noExecutions: { defaultMessage: 'No executions found', id: 'HDeHN9' },
    noExecutionsDescription: { defaultMessage: 'This scheduled task has not been run yet.', id: 'WCPqyS' },
    executionHistoryTableAriaLabel: { defaultMessage: 'Scheduled task execution history', id: 'TEziH8' },
    statusFilterAll: { defaultMessage: 'All', id: 'zQvVDJ' },
    backToScheduledTasks: { defaultMessage: 'Back to scheduled tasks', id: 'eXGPt3' },
    editTask: { defaultMessage: 'Edit task', id: 'dsTLW1' },
    noThreadFound: { defaultMessage: 'No thread found', id: 'y1cukD' },
});

export const SubAgentsResources = defineMessages({
    noSubAgents: { defaultMessage: 'No subagents configured', id: 'vCyJXp' },
    runHistory: { defaultMessage: 'Run History', id: 'LnglaU' },
    duplicateNameError: { defaultMessage: 'A subagent with this name already exists', id: 'NZ4EtH' },
    createSubAgent: { defaultMessage: 'Create Subagent', id: 'wRi+GV' },
    namePlaceholder: { defaultMessage: 'Enter subagent name', id: 'lpYK4u' },
    creatingSubAgent: { defaultMessage: 'Creating Subagent', id: 'wFcmSl' },
    creatingSubAgentDescription: { defaultMessage: 'Creating subagent "{name}"', id: '5AvgDc' },
    subAgentCreated: { defaultMessage: 'Subagent "{name}" has been created successfully', id: 'Hh4t4o' },
    createSubAgentFailed: { defaultMessage: 'Failed to create subagent', id: 'OQMatj' },
    createSubAgentWithMessageFailed: { defaultMessage: 'Failed to create subagent with error: {error}', id: '/HeRX5' },
});

export const McpServerResources = defineMessages({
    mcp: { defaultMessage: 'MCP', id: 'RbWH8Q' },
    title: { defaultMessage: 'Connect an MCP server', id: 'fssuHC' },
    description: {
        defaultMessage:
            'Connect a Model Context Protocol (MCP) server to give the agent additional tools and actions for automating incident handling.',
        id: 'SZMgCd',
    },
    learnMore: { defaultMessage: 'Learn more about MCP servers', id: '98A08r' },
    connectServer: { defaultMessage: 'Connect MCP server', id: 'Euy+fB' },
    refresh: { defaultMessage: 'Refresh', id: 'rELDbB' },
    disconnect: { defaultMessage: 'Disconnect', id: 'qj1uhz' },
    searchPlaceholder: { defaultMessage: 'Search', id: 'xmcVZ0' },
    serviceTypeFilter: { defaultMessage: 'Service type: All', id: 'rTBfGn' },
    allServiceTypes: { defaultMessage: 'All', id: 'zQvVDJ' },
    mcpServersTableAriaLabel: { defaultMessage: 'MCP servers table', id: 'LbkJp1' },
    name: { defaultMessage: 'MCP server name', id: 'lUAfdH' },
    serviceType: { defaultMessage: 'Service type', id: 'EN5iMk' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    emptyStateTitle: { defaultMessage: 'Add more tools for incident response', id: 'CvGdox' },
    emptyStateDescription: {
        defaultMessage: 'Connect an Azure service MCP server to give the agent more tools to resolve an incident.',
        id: 'w8UjdT',
    },
    emptyStateAlt: { defaultMessage: 'MCP server icon', id: '+p60ph' },
});

export const MemorySearchCardResources = defineMessages({
    memorySearchResults: { defaultMessage: 'Memory Search Results', id: 'DuejfP' },
    pastIncidentsOnSameResource: { defaultMessage: 'Past Incidents on Same Resource', id: '/Vy71a' },
    similarSymptomIncidents: { defaultMessage: 'Similar Symptom Incidents', id: 'rI2aPZ' },
    userMemories: { defaultMessage: 'User Memories', id: '+iCTRd' },
    relevantDocuments: { defaultMessage: 'Relevant Documents', id: 'oBv1Sy' },
    memorySearchResultsIntro: { defaultMessage: 'Here are the memory search results:', id: 'CeDXMM' },
    viewMemorySearchResults: { defaultMessage: 'View Memory Search Results', id: 'I1tfGG' },
    symptomsLabel: { defaultMessage: 'Symptoms:', id: 'h4XDJP' },
    rootCauseLabel: { defaultMessage: 'Root Cause:', id: 'sILeXv' },
});

export const KnowledgeGraphCardResources = defineMessages({
    knowledgeGraphSearchResults: { defaultMessage: 'Knowledge Graph Search Results', id: 'fi7+px' },
    knowledgeGraphSearchResultsIntro: { defaultMessage: 'Here are the knowledge graph search results:', id: '0Xs9vS' },
    viewKnowledgeGraphSearchResults: { defaultMessage: 'View Knowledge Graph Search Results', id: 'OgEDPw' },
    entitiesLabel: { defaultMessage: 'Entities', id: 'CFLecQ' },
    relationsLabel: { defaultMessage: 'Relations', id: 'mn5pjI' },
    queryLabel: { defaultMessage: 'Query:', id: 'qC5uhF' },
    noResults: { defaultMessage: 'No matching entities or relations found.', id: 'PGhDTf' },
});

export const ExtendedAgentsGraphResources = defineMessages({
    // Tab and Navigation
    extendedAgentsTab: { defaultMessage: 'Subagent builder', id: 'EN0CPQ' },
    canvasView: { defaultMessage: 'Canvas view', id: 'uKwm6S' },
    tableView: { defaultMessage: 'Table view', id: 'ufzv1A' },
    listViewSelectAll: { defaultMessage: 'Select all', id: '94Fg25' },
    listViewNameColumn: { defaultMessage: 'Name', id: 'HAlOn1' },
    listViewTypeColumn: { defaultMessage: 'Type', id: '+U6ozc' },
    listViewDescriptionColumn: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    listViewActionsColumn: { defaultMessage: 'Actions', id: 'wL7VAE' },
    listViewDescriptionFallback: { defaultMessage: 'No description available', id: 'rAbWbb' },
    listViewDescription: {
        defaultMessage: 'Use subagents to automatically run tasks, query data, and respond to incidents.',
        id: 'wPF0jY',
    },
    learnMoreAboutSubagent: { defaultMessage: 'Learn more about subagents', id: '96TDsd' },

    // Creation Dialog
    createNewEntity: { defaultMessage: 'Create New Entity', id: '5POweM' },
    createYourFirstEntity: { defaultMessage: 'Create Your First Entity', id: 'N+XwM+' },
    whatToCreate: { defaultMessage: 'What would you like to create?', id: '5q8BxP' },
    quickCreateAgentTitle: { defaultMessage: 'Create a handoff subagent', id: 'ZAkVKt' },
    quickCreateAgentDescription: {
        defaultMessage: 'New handoff subagents start as Autonomous. You can connect tools and other details later.',
        id: 'fBeNkx',
    },
    quickCreateAgentAutonomousHint: {
        defaultMessage: 'We’ll mark this agent as Autonomous automatically so it can run on its own.',
        id: 'L/i6dy',
    },
    creationSuccessAddTrigger: { defaultMessage: 'Add a trigger', id: '2+TYMx' },
    creationSuccessAddTool: { defaultMessage: 'Add a tool', id: 'xsbZ+Q' },
    entityNameValidationMessage: {
        defaultMessage: 'Name can only contain letters, numbers, or hyphens and must be {maxLength} characters or fewer.',
        id: 'bdhTWR',
    },

    // Entity Types
    agent: { defaultMessage: 'Subagent', id: 'Q++yMM' },
    tool: { defaultMessage: 'Tool', id: 'h6183G' },
    connector: { defaultMessage: 'Connector', id: 'r8XsCU' },

    // Type Descriptions
    agentDescription: { defaultMessage: 'An AI subagent with instructions and tools', id: 'LxHm7z' },
    toolDescription: { defaultMessage: 'A function or capability for agents to use', id: 'Ai/0lk' },
    connectorDescription: { defaultMessage: 'A data source connection for tools (e.g., Kusto cluster for running queries)', id: 'Dhy8jb' },
    trigger: { defaultMessage: 'Trigger', id: 'B3Q5mz' },
    triggerDescriptionLoading: { defaultMessage: 'Checking trigger setup…', id: 'x/7RC2' },
    triggerDescriptionIncident: {
        defaultMessage:
            '{count, plural, =0 {Set up incident response plans to trigger agents automatically.} one {1 incident response plan ready to trigger your agents.} other {{count} incident response plans ready to trigger your agents.}}',
        id: 'RsY+gv',
    },
    triggerDescriptionScheduled: {
        defaultMessage:
            '{count, plural, =0 {Create scheduled tasks to run agents automatically.} one {1 scheduled task ready to run agents automatically.} other {{count} scheduled tasks ready to run agents automatically.}}',
        id: '0nrwMu',
    },
    triggerDescriptionFallback: {
        defaultMessage: 'Create incident response plans or scheduled tasks to trigger agents automatically.',
        id: 'udi5I/',
    },
    triggerStatusLoading: { defaultMessage: 'Loading trigger status…', id: 'CX0VFc' },
    triggerIncidentStat: {
        defaultMessage:
            '{count, plural, =0 {No incident response plans yet} one {1 incident response plan ready} other {{count} incident response plans ready}}',
        id: '7oBAcZ',
    },
    triggerScheduledStat: {
        defaultMessage: '{count, plural, =0 {No scheduled tasks yet} one {1 scheduled task ready} other {{count} scheduled tasks ready}}',
        id: 'JXHRt5',
    },
    triggerIncidentButton: { defaultMessage: 'Open incident triggers', id: 'w6nz4p' },
    triggerScheduledButton: { defaultMessage: 'Open scheduled tasks', id: 'CaZAZZ' },
    triggerBadgeIncident: { defaultMessage: 'Incident trigger', id: 'THFIRB' },
    triggerBadgeScheduled: { defaultMessage: 'Scheduled trigger', id: 'vlv+Ch' },
    connectorNavigateCta: { defaultMessage: 'Manage connectors', id: 'cqx+Rd' },

    // Trigger Creation Defaults
    triggerAgentFallbackName: { defaultMessage: 'New agent', id: 'KR79he' },
    triggerAgentRecentLabel: { defaultMessage: 'Recently used', id: 'm+cdhC' },
    triggerAgentAllLabel: { defaultMessage: 'All agents', id: 'nCowEs' },
    triggerAgentWarning: {
        defaultMessage: 'No tools linked yet. Add tools so this agent can take action.',
        id: 'o1g3JC',
    },
    triggerIncidentDefaultName: { defaultMessage: '{agentName} incident response plan', id: 'h4BqRo' },
    triggerIncidentDefaultInstructions: {
        defaultMessage: 'When an incident opens, have {agentName} investigate and summarize the latest state.',
        id: 'sdNNv5',
    },
    triggerScheduledDefaultName: { defaultMessage: '{agentName} scheduled task', id: 'ZqVUb5' },
    triggerScheduledDefaultDescription: {
        defaultMessage: 'Automatically run {agentName} on a schedule to keep key systems healthy.',
        id: 'GpZr9J',
    },
    triggerScheduledDefaultPrompt: {
        defaultMessage: 'Provide a short report about the latest signals and note any follow-up actions.',
        id: 'aqGaRm',
    },

    // Trigger Creation Dialog
    triggerCreateAction: { defaultMessage: 'Create trigger', id: 'FyrC09' },
    triggerDetailsHeading: { defaultMessage: 'Trigger details', id: '4C44ie' },
    triggerDetailsSubheading: {
        defaultMessage: 'Choose how this trigger should behave and which agent it starts.',
        id: 'yhqGka',
    },
    triggerModeIncidentTitle: { defaultMessage: 'Incident response', id: 'BgdyiU' },
    triggerModeIncidentDescription: {
        defaultMessage: 'Create a plan that starts this agent when a new incident is detected.',
        id: 'OLSbA3',
    },
    triggerModeScheduledTitle: { defaultMessage: 'Scheduled task', id: 'dG8VrM' },
    triggerModeScheduledDescription: {
        defaultMessage: 'Run this agent on a repeating schedule.',
        id: 'eYEnNu',
    },
    triggerModeScheduledDisabled: {
        defaultMessage: 'Scheduled tasks require an upgrade. Contact your administrator to enable them.',
        id: 'K1h5xo',
    },
    triggerAgentLabel: { defaultMessage: 'Starting agent', id: 'kWBrry' },
    triggerAgentPlaceholder: { defaultMessage: 'Select a starting agent', id: 'k5T5VE' },
    triggerStrategyLabel: { defaultMessage: 'How should this trigger be set up?', id: 'mbtoHb' },
    triggerStrategyQuick: { defaultMessage: 'Quick create', id: 'M3159R' },
    triggerStrategyQuickDescription: { defaultMessage: 'Create a new trigger with custom settings', id: 'iV7LUx' },
    triggerStrategyExisting: { defaultMessage: 'Use existing', id: 'I5k4g/' },
    triggerStrategyExistingDescription: { defaultMessage: 'Use an existing trigger configuration', id: 'ks97gc' },
    triggerModeLabel: { defaultMessage: 'Trigger type', id: '6Vngei' },
    triggerNameLabel: { defaultMessage: 'Trigger name', id: 'zDcGyS' },
    triggerNamePlaceholder: { defaultMessage: 'e.g., P1 incident response', id: 'iydbuT' },
    triggerIncidentPriorityLabel: { defaultMessage: 'Incident priority', id: 'y5oInX' },
    triggerIncidentTypeLabel: { defaultMessage: 'Incident type', id: 'Udeffr' },
    triggerInstructionsLabel: { defaultMessage: 'Instructions', id: 'sV2v5L' },
    triggerInstructionsIncidentPlaceholder: {
        defaultMessage: 'Describe how {agentName} should handle new incidents.',
        id: '7tqXmc',
    },
    triggerDescriptionLabel: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    triggerDescriptionPlaceholder: {
        defaultMessage: 'Explain what this scheduled trigger does for {agentName}.',
        id: 'OeU7Xk',
    },
    triggerScheduleInputModeLabel: {
        defaultMessage: 'How would you like to define the schedule?',
        id: 'CPsPh7',
    },
    triggerScheduleInputModePreset: { defaultMessage: 'Pick from presets', id: 'uYb82D' },
    triggerScheduleInputModeNatural: { defaultMessage: 'Describe it in natural language', id: '+/9vVV' },
    triggerSchedulePresetLabel: { defaultMessage: 'Schedule', id: 'hGQqkW' },
    triggerScheduleCustomLabel: { defaultMessage: 'Custom schedule', id: 'khOzXi' },
    triggerScheduleCustomExpressionLabel: { defaultMessage: 'Cron expression', id: 'YmslQP' },
    triggerScheduleCustomPlaceholder: { defaultMessage: 'e.g., 0 9 * * 1-5', id: 'QomX1y' },
    triggerScheduleCustomDescription: {
        defaultMessage: 'Use standard cron syntax (UTC) to control when the agent runs.',
        id: 'OXek0m',
    },
    triggerScheduleStartTimeLabel: { defaultMessage: 'Start time (optional)', id: 'jv8SuZ' },
    triggerScheduleStartHelp: {
        defaultMessage: 'Optional: choose when this schedule should begin running.',
        id: 'TmQqXl',
    },
    triggerInstructionsScheduledPlaceholder: {
        defaultMessage: 'Describe what {agentName} should do each time it runs.',
        id: 'wOHH3J',
    },
    triggerInstructionsScheduledRequired: {
        defaultMessage: 'Provide instructions for what this scheduled task should do.',
        id: '+kmL8g',
    },
    triggerScheduleSummary: { defaultMessage: 'Runs {description}.', id: '498yM3' },
    triggerScheduleNextRunsLabel: { defaultMessage: 'Next run times', id: 'wb/AUH' },
    triggerScheduleNaturalLabel: { defaultMessage: 'Describe the schedule', id: 'LkUvu/' },
    triggerScheduleNaturalPlaceholder: { defaultMessage: 'e.g., Every weekday at 9am', id: '0ltHAS' },
    triggerScheduleNaturalHelp: {
        defaultMessage: 'Use natural language—we’ll translate it into a cron schedule.',
        id: 'MjxZUD',
    },
    triggerScheduleNaturalResolved: { defaultMessage: 'Detected cron: {cron}', id: 'AdRf7Y' },
    triggerScheduleNaturalGenerate: { defaultMessage: 'Generate schedule', id: 'StVmtb' },
    triggerScheduleNaturalGenerating: { defaultMessage: 'Generating…', id: 'tB02Wz' },
    triggerScheduleNaturalGenerateFailed: {
        defaultMessage: "We couldn't understand that description. Try a different phrasing.",
        id: '/WgnYE',
    },
    triggerScheduleNaturalClear: { defaultMessage: 'Clear description', id: 'w0xxAb' },
    triggerScheduleNaturalErrorRequired: {
        defaultMessage: 'Describe the schedule before generating a cron expression.',
        id: '12GY0h',
    },
    triggerScheduleNaturalAssumptions: { defaultMessage: 'Assumptions', id: 'X3hl5x' },
    triggerScheduleNaturalWarningsHeading: { defaultMessage: 'Warnings', id: 'VSWkne' },
    triggerScheduleNaturalExamplesHeading: { defaultMessage: 'Examples', id: '3GLH+d' },
    triggerScheduleAwaitingParse: { defaultMessage: 'Parsing…', id: 'Z9TajN' },
    triggerScheduleAdvancedLabel: { defaultMessage: 'Advanced options', id: '0cEOKu' },
    triggerScheduleCronInvalid: { defaultMessage: 'Enter a valid cron expression.', id: '0TOEqZ' },
    triggerScheduleTimezoneLabel: { defaultMessage: 'Timezone', id: '7nUCu9' },
    triggerExistingIncidentLabel: { defaultMessage: 'Incident response plan', id: 'mky0K0' },
    triggerExistingScheduledLabel: { defaultMessage: 'Scheduled task', id: 'dG8VrM' },
    triggerExistingPlaceholder: { defaultMessage: 'Select an existing trigger', id: '4/nSt8' },
    triggerExistingNone: {
        defaultMessage: 'No matching items yet. Complete the quick create form to add one.',
        id: 'j59lXr',
    },
    triggerExistingLastRun: { defaultMessage: 'Last run: {value}', id: 'FFFf74' },
    triggerExistingNoRun: { defaultMessage: 'No runs yet', id: 'x3qvaC' },
    triggerExistingNextRun: { defaultMessage: 'Next run: {value}', id: '1EnaNQ' },
    triggerStrategyHelp: {
        defaultMessage: 'Start fresh or reuse an existing trigger configuration.',
        id: 'gvlNuI',
    },
    triggerScheduledEnableCta: { defaultMessage: 'Open scheduled tasks', id: 'CaZAZZ' },

    // Wizard Steps
    stepType: { defaultMessage: 'Type', id: '+U6ozc' },
    stepDetails: { defaultMessage: 'Details', id: 'Lv0zJu' },
    stepReview: { defaultMessage: 'Review', id: 'R+J5ox' },

    // Buttons
    back: { defaultMessage: 'Back', id: 'cyR7Kh' },
    cancel: { defaultMessage: 'Cancel', id: '47FYwb' },
    next: { defaultMessage: 'Next', id: '9+Ddtu' },
    create: { defaultMessage: 'Create', id: 'VzzYJk' },
    creating: { defaultMessage: 'Creating...', id: 'mRL9Vh' },
    save: { defaultMessage: 'Save', id: 'jvo0vs' },
    saving: { defaultMessage: 'Saving...', id: 'TiR/Hq' },
    edit: { defaultMessage: 'Edit', id: 'wEQDC6' },
    editSkill: { defaultMessage: 'Edit Skill', id: 'UWZEBA' },

    // Agent Form Fields
    agentName: { defaultMessage: 'Agent name', id: 'ctcA0c' },
    agentNamePlaceholder: { defaultMessage: 'e.g., IncidentAnalyzer', id: '9MwJ6v' },
    agentNameHelp: { defaultMessage: 'A unique identifier for your agent', id: '15nhHh' },
    agentType: { defaultMessage: 'Agent Type', id: 'NDy5dO' },
    agentTypeHelp: { defaultMessage: 'Defines how the agent operates', id: 'PPrKNq' },
    autonomous: { defaultMessage: 'Autonomous', id: 'Sr5R7d' },
    orchestrator: { defaultMessage: 'Orchestrator', id: 'RK2Ddg' },
    activity: { defaultMessage: 'Activity', id: 'ZmlNQ3' },
    instructions: { defaultMessage: 'Instructions', id: 'sV2v5L' },
    instructionsPlaceholder: { defaultMessage: 'Describe what this agent does and how it should behave...', id: 'o0pkiK' },
    instructionsHelp: { defaultMessage: "System prompt that defines the agent's behavior", id: 'FkvYGq' },

    handoffDescriptionLabel: { defaultMessage: 'Handoff Description', id: 'QY3CEK' },
    handoffDescriptionPlaceholder: {
        defaultMessage: 'Describe when and why other agents should hand off to this agent...',
        id: '98TwgV',
    },
    handoffDescriptionHelp: {
        defaultMessage:
            'A clear, concise description that helps other agents understand when to delegate work to this specialist agent. This appears in agent handoff decisions and collaboration workflows.',
        id: '6umiRw',
    },
    handoffDescriptionSuggestionHeading: {
        defaultMessage: '✨ AI Suggested Handoff Description',
        id: 'tJuu1A',
    },

    tools: { defaultMessage: 'Tools', id: 'nUT0Lv' },
    toolsOptional: { defaultMessage: 'Extended Tools (Optional)', id: '+TFnQ/' },
    toolsPlaceholder: { defaultMessage: 'Select tools this agent can use', id: 'F6qYbg' },
    toolsHelp: { defaultMessage: 'Select existing tools or create new ones later', id: 'tvw8DP' },

    // System Tools
    systemToolsOptional: { defaultMessage: 'System Tools (Optional)', id: 'sdussP' },
    systemToolsPlaceholder: { defaultMessage: 'Select system tools this agent can use', id: 'VQn8u+' },
    systemToolsHelp: { defaultMessage: 'System tools are built-in tools provided by the platform', id: 'euKVCa' },

    // MCP Tools
    mcpToolsOptional: { defaultMessage: 'MCP Tools (Optional)', id: 'CHt7dX' },
    mcpToolsPlaceholder: { defaultMessage: 'Select MCP tools from connected servers', id: 'Da55kZ' },
    mcpToolsPlaceholderEmpty: { defaultMessage: 'No MCP tools available', id: 'GCcYpN' },
    mcpToolsNoConnections: { defaultMessage: 'No MCP connections available', id: 'LAHAJH' },
    mcpToolsLoading: { defaultMessage: 'Loading MCP tools...', id: 'oTOgOq' },
    mcpToolsSelectedCount: {
        defaultMessage: '{count, plural, one {# tool selected} other {# tools selected}}',
        id: '1o9eJ6',
    },
    mcpToolsHelpText: {
        defaultMessage:
            'Tools from Model Context Protocol (MCP) servers. These tools are grouped by connection and loaded from connected servers such.',
        id: 'ssDQgc',
    },
    mcpToolsLoadErrorFallback: { defaultMessage: 'Failed to load MCP tools.', id: 'NPm9R5' },

    // Tool Form Fields
    createKustoTool: { defaultMessage: 'Create a Kusto tool', id: 'gclDgn' },
    editKustoTool: { defaultMessage: 'Edit Kusto tool', id: 'bDbbX7' },
    createPythonTool: { defaultMessage: 'Create a Python tool', id: '+3gcXm' },
    editPythonTool: { defaultMessage: 'Edit Python tool', id: 'FKyoe8' },
    toolNoConnectorsMessage: { defaultMessage: 'To create a Kusto tool, first add an Azure Data Explorer connector.', id: 'fiLH5L' },
    goToConnectors: { defaultMessage: 'Go to Connectors', id: 'QK0vxq' },
    toolName: { defaultMessage: 'Tool name', id: 'oDH12O' },
    toolNamePlaceholder: { defaultMessage: 'Enter a descriptive name', id: 'UNQfKX' },
    toolNameHelp: { defaultMessage: 'A unique identifier for your tool', id: 'nySNrf' },
    toolType: { defaultMessage: 'Tool type', id: 'mZ3wu9' },
    toolTypeHelp: { defaultMessage: 'The type of tool functionality', id: 'BP3wUO' },
    kustoTool: { defaultMessage: 'Kusto tool', id: 'v6cujo' },
    linkTool: { defaultMessage: 'Link Tool', id: 'ppzeDo' },
    description: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    descriptionPlaceholder: { defaultMessage: 'Enter a description', id: 'QAVYIG' },
    descriptionHelp: { defaultMessage: 'Clear description for agents to understand when to use this tool', id: 'ubULuu' },
    connectorOptional: { defaultMessage: 'Connector (Optional)', id: 'bdFY56' },
    connectorPlaceholder: { defaultMessage: 'Select a connector', id: 'Gnbr9F' },
    databasePlaceholder: { defaultMessage: 'Enter the database name', id: 'gox4oK' },
    queryPlaceholder: { defaultMessage: 'Enter the query for the tool to run', id: '8SCDv/' },
    queryHint: {
        defaultMessage:
            'Use ##ParamName## in your query to define parameter placeholders.\nExample: where SubscriptionId == "##SubscriptionId##"\nThese will be replaced with actual values at runtime.',
        id: '6BTCPo',
    },

    toolsSearchPlaceholder: {
        defaultMessage: 'Search tools by name, category, or description',
        id: 'P0yf0W',
    },
    toolsSearchNoResults: { defaultMessage: 'No tools match your search.', id: 'bxSp6r' },
    toolsCategoryLabel: { defaultMessage: 'Category: {category}', id: 'cfbpDd' },

    extendedToolsUnavailable: {
        defaultMessage: 'No extended tools available.',
        id: 'j1FyDL',
    },

    systemToolsSearchPlaceholder: {
        defaultMessage: 'Search system tools by name, category, or description',
        id: 'lni1id',
    },

    systemToolsSearchNoResults: {
        defaultMessage: 'No system tools match your search.',
        id: 'PvMAYc',
    },

    systemToolsUnavailable: {
        defaultMessage: 'No system tools available.',
        id: 'jqpolM',
    },
    connectorHelp: { defaultMessage: 'Choose the Kusto connector this tool will query', id: 'PS7hBo' },
    parameterBindingLabel: { defaultMessage: 'Binding', id: 'BWt4Jj' },
    parameterBindingHelp: {
        defaultMessage: 'Controls how this value is passed to the tool method',
        id: 'aKNsTc',
    },
    parameterBindingDictionary: { defaultMessage: 'Dictionary (group into args)', id: 'xNC5hG' },
    parameterBindingDirect: { defaultMessage: 'Direct argument', id: 'LsGLc0' },
    parameterBindingIgnored: { defaultMessage: 'Ignore (use static value)', id: 'oDVFQr' },
    parameterMapToDictionaryLabel: { defaultMessage: 'Dictionary key (map_to)', id: 'MLVVhB' },
    parameterMapToDirectLabel: { defaultMessage: 'Map to argument', id: 'Km+WSh' },
    parameterMapToDictionaryHelp: {
        defaultMessage: 'Name of the dictionary this parameter contributes to',
        id: 'kX8T8H',
    },
    parameterMapToDirectHelp: {
        defaultMessage: 'Tool method argument that receives this value',
        id: 'IcB2fr',
    },
    parameterDictionaryValueTypeLabel: { defaultMessage: 'Dictionary value type', id: 'n2h/5e' },
    parameterDictionaryValueTypeHelp: {
        defaultMessage: 'Type for dictionary values',
        id: 'VzeCzD',
    },
    parameterDataTypeHelp: {
        defaultMessage: 'Data type and requirement for validation',
        id: 'Bluuk+',
    },
    createTool: { defaultMessage: 'Create tool', id: 'l0VCdV' },
    createToolTitle: { defaultMessage: 'Create Kusto Tool', id: 'OpUvSu' },
    createToolInProgress: { defaultMessage: 'Creating Kusto Tool', id: 'LaIt/v' },
    failedToCreateTool: { defaultMessage: 'Failed to create tool', id: 'Dvly6A' },
    toolCreatedSuccessfully: { defaultMessage: 'Tool created successfully', id: 'f04POI' },
    updateToolTitle: { defaultMessage: 'Update Kusto Tool', id: 'n25S4d' },
    updateToolInProgress: { defaultMessage: 'Updating Kusto Tool', id: 'DP0m/y' },
    failedToUpdateTool: { defaultMessage: 'Failed to update tool', id: 'PpdTYy' },
    toolUpdatedSuccessfully: { defaultMessage: 'Tool updated successfully', id: 'fvSiRs' },
    createPythonToolTitle: { defaultMessage: 'Create Python Tool', id: '0eQHdW' },
    createPythonToolInProgress: { defaultMessage: 'Creating Python Tool', id: 'btp86z' },
    updatePythonToolTitle: { defaultMessage: 'Update Python Tool', id: '0dIKAx' },
    updatePythonToolInProgress: { defaultMessage: 'Updating Python Tool', id: 'dRQsmw' },
    addParameter: { defaultMessage: 'Add parameter', id: 'qB4s5L' },
    parameterName: { defaultMessage: 'Parameter name', id: 'xLrAFR' },
    parameterNamePlaceholder: { defaultMessage: 'Enter the parameter name', id: '0hpRE4' },
    type: { defaultMessage: 'Type', id: '+U6ozc' },
    value: { defaultMessage: 'Value', id: 'GufXy5' },
    runATestMessage: {
        defaultMessage: 'Run a test first to validate the query and ensure the agent can access Azure Data Explorer.',
        id: 'swe9tO',
    },
    testQuery: { defaultMessage: 'Test query', id: 'CTRqZs' },
    runTest: { defaultMessage: 'Run test', id: 'mZ0R9v' },
    failedToRunTest: { defaultMessage: 'Failed to run test', id: 'DcEKTG' },
    testValues: { defaultMessage: 'Test values', id: 'HZQtAg' },
    inputValue: { defaultMessage: 'Input value', id: 'PF15hb' },
    inputValuePlaceholder: { defaultMessage: 'Enter a value', id: 'pndXMi' },
    inputStringValuePlaceholder: { defaultMessage: 'Enter a string', id: 'pgIRcn' },
    inputNumberValuePlaceholder: { defaultMessage: 'Enter a number', id: 'EHy/sj' },
    inputBooleanValuePlaceholder: { defaultMessage: 'Enter a boolean', id: 'XS8hWM' },
    inputDatetimeValuePlaceholder: { defaultMessage: 'Enter a datetime', id: 'a8fWCd' },
    inputType: { defaultMessage: 'Input type', id: 'TrnqJG' },
    inputTypePlaceholder: { defaultMessage: 'Select the input type', id: 'OoxcFC' },
    string: { defaultMessage: 'String', id: 'I3MA83' },
    number: { defaultMessage: 'Number', id: 'kFkPWB' },
    boolean: { defaultMessage: 'Boolean', id: 'DIZgr3' },
    datetime: { defaultMessage: 'Datetime', id: 'd2czNd' },

    // Connector Form Fields
    connectorName: { defaultMessage: 'Connector Name', id: 'qiY3L8' },
    connectorNamePlaceholder: { defaultMessage: 'e.g., ProductionKusto', id: '/tauMV' },
    connectorNameHelp: { defaultMessage: 'A unique identifier for your connector', id: '7Vep/o' },
    connectorType: { defaultMessage: 'Connector Type', id: 'XHc00z' },
    connectorTypeHelp: { defaultMessage: 'The type of data source', id: '4z+NaY' },
    kusto: { defaultMessage: 'Kusto', id: '/rNkXe' },
    descriptionOptional: { defaultMessage: 'Description (Optional)', id: 's6iZgz' },
    descriptionConnectorPlaceholder: { defaultMessage: 'Describe this connector...', id: '8RcpTO' },
    descriptionConnectorHelp: { defaultMessage: 'Optional description of what this connector accesses', id: 'gP/k0J' },

    // Review Step
    reviewYourAgent: { defaultMessage: 'Review Your agent', id: 'S0GneP' },
    reviewYourTool: { defaultMessage: 'Review Your tool', id: 'zuQF37' },
    reviewYourConnector: { defaultMessage: 'Review Your connector', id: 'TwMqIK' },
    reviewYourTrigger: { defaultMessage: 'Review Your trigger', id: 'Jz5GjX' },
    reviewYamlPreviewUnavailable: { defaultMessage: 'Nothing to preview yet.', id: 'oKJ0fm' },
    triggerReviewLead: { defaultMessage: 'Review your trigger configuration before creating', id: 'P5X8Kw' },
    triggerReviewBackToEdit: { defaultMessage: 'Back to Edit', id: 'tGHG7q' },

    // Empty State
    buildYourAgentEcosystem: { defaultMessage: 'Build Your Agent Ecosystem', id: 'HNECLT' },
    scaleYourAgentsCapabilitiesWithSubagents: { defaultMessage: 'Scale your agent’s capabilities with subagents', id: 'MCLPMF' },
    emptyStateDescription: {
        defaultMessage: 'Create subagents to automatically run tasks, query data, and respond to incidents.',
        id: 'm/B8a5',
    },
    emptyStateDescriptionLearnMore: { defaultMessage: 'Learn more about creating subagents.', id: 'EVXYDU' },
    createSubagent: { defaultMessage: 'Create subagent', id: 'O9C6j/' },
    createSkill: { defaultMessage: 'Create skill', id: '8jD/lL' },
    cannotCreateSubagentWithSkills: { defaultMessage: 'Cannot create subagents when skills are enabled', id: 'W7GhBB' },
    cannotCreateSkillWithSubagents: { defaultMessage: 'Cannot create skills when subagents exist', id: 'OmDQWM' },
    skillFormTab: { defaultMessage: 'Form', id: 'baRFiF' },
    skillYamlTab: { defaultMessage: 'YAML', id: 'FvhvDO' },
    skillFilesTab: { defaultMessage: 'Additional Files', id: 'vEPakK' },
    skillName: { defaultMessage: 'Name', id: 'HAlOn1' },
    skillDescription: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    skillDescriptionPlaceholder: { defaultMessage: 'Brief description of what this skill does', id: 'TMjYpD' },
    skillTools: { defaultMessage: 'Tools', id: 'nUT0Lv' },
    skillToolsPlaceholder: { defaultMessage: 'Comma-separated tool names, e.g., SearchKusto, GetLogs', id: '7JgdFw' },
    skillContent: { defaultMessage: 'Skill Content (SKILL.md)', id: 'gmo2Su' },
    skillMetadata: { defaultMessage: 'Metadata', id: '8Q504V' },
    skillContentPlaceholder: { defaultMessage: 'Enter the skill markdown content...', id: 'rc7ZT2' },
    skillFilesDescription: { defaultMessage: 'Add additional files to include with this skill.', id: '3xD7BQ' },
    dragFilesHere: { defaultMessage: 'Drag files here or', id: 'gDVRX+' },
    browseForFiles: { defaultMessage: 'browse for files', id: 'FvtGBp' },
    additionalFiles: { defaultMessage: 'Additional files', id: 'QozEE6' },
    fileName: { defaultMessage: 'File name', id: 'ppAn7O' },
    skillFiles: { defaultMessage: 'Files', id: 'm4vqJl' },
    noFileSelected: { defaultMessage: 'Select a file to edit', id: 'Eo9M+K' },
    editingFile: { defaultMessage: 'Editing: {fileName}', id: 'xjZrbx' },
    defaultFile: { defaultMessage: 'default', id: 'FiXNt1' },
    newFolder: { defaultMessage: 'New folder', id: 'VCHJad' },
    newFile: { defaultMessage: 'New file', id: '0L2J5T' },
    folderName: { defaultMessage: 'Folder name', id: 'lR/7o8' },
    uploadFolder: { defaultMessage: 'Upload folder', id: '5X7NpJ' },
    skill: { defaultMessage: 'Skill', id: 'GFhSwY' },
    subagent: { defaultMessage: 'Subagent', id: 'Q++yMM' },
    aiAgents: { defaultMessage: 'AI Agents', id: '4XOvey' },
    aiAgentsFeature: { defaultMessage: 'Define autonomous agents with custom instructions and capabilities', id: '7smbCB' },
    toolsFeature: { defaultMessage: 'Create reusable functions and actions for your agents', id: 'AqVLXi' },
    connectorsFeature: { defaultMessage: 'Connect to data sources like Kusto for rich agent capabilities', id: 'vCsh17' },

    // FAB Tooltip
    createNewEntityTooltip: { defaultMessage: 'Create new entity', id: 'FTWkpv' },

    // Errors and Messages
    errorLoadingGraph: { defaultMessage: 'Error loading graph: {error}', id: '1FMpZj' },
    required: { defaultMessage: 'Required', id: 'Seanpx' },
    optional: { defaultMessage: 'Optional', id: 'InWqys' },
    agentSelectorLabel: { defaultMessage: 'Agent', id: 'QGVI63' },
    agentSelectorPlaceholder: { defaultMessage: 'Select an agent', id: 'ipsqxO' },
    noAgentsFound: { defaultMessage: 'No agents available yet', id: '3H77Ic' },
    searchLabel: { defaultMessage: 'Search', id: 'xmcVZ0' },
    refreshGraphButton: { defaultMessage: 'Refresh', id: 'rELDbB' },
    toolsCountBadge: { defaultMessage: 'Tools · {count}', id: '5walLE' },
    systemToolsCountBadge: { defaultMessage: 'System tools · {count}', id: 'IvKdxU' },
    mcpToolsCountBadge: { defaultMessage: 'MCP tools · {count}', id: '9jaWOF' },
    handoffCountBadge: { defaultMessage: 'Handoffs · {count}', id: 'MnlPLt' },
    agentAsToolCountBadge: { defaultMessage: 'Agents-as-tools · {count}', id: 'tujypD' },
    memoryEnabledBadge: { defaultMessage: 'Knowledge Base Enabled', id: 'ketkP5' },
    skillsEnabledBadge: { defaultMessage: 'Skills Enabled', id: '/mPnOR' },
    skillsLabel: { defaultMessage: 'Skills', id: 'EJSVsO' },
    noSkillsFound: { defaultMessage: 'No skills found', id: 'z4NnlT' },
    searchSkillsPlaceholder: { defaultMessage: 'Search by skill', id: 'uQ/yKk' },
    yesLabel: { defaultMessage: 'Yes', id: 'a5msuh' },
    noLabel: { defaultMessage: 'No', id: 'oUWADl' },
    memoryDocumentsCount: { defaultMessage: '{count} {count, plural, one {document} other {documents}}', id: 'GBLVEV' },
    memoryAddDocuments: { defaultMessage: 'Add Documents', id: 'ja85pV' },
    memoryViewKnowledgeBase: { defaultMessage: 'View Knowledge Base', id: 'z43sTn' },
    memoryKnowledgeBasePrompt: {
        defaultMessage: 'Your knowledge base has {count} {count, plural, one {document} other {documents}}. Add more?',
        id: 'jSgRib',
    },
    memoryNoDocuments: { defaultMessage: 'No documents in knowledge base. Add some?', id: 'UBbDNy' },
    connectsTo: { defaultMessage: 'Connects to', id: 'gO/IxZ' },
    filesTab: { defaultMessage: 'Files', id: 'm4vqJl' },
    selectAgentPrompt: {
        defaultMessage: 'Pick an agent to visualize its tools, connectors, and handoffs.',
        id: 'WcnRC1',
    },
    noResultsForFilters: {
        defaultMessage: 'No matches for the current filters.',
        id: '86kOeb',
    },
    gridViewPlaceholder: {
        defaultMessage: 'Grid view is coming soon.',
        id: 'Xb85sq',
    },

    // Grid View
    subAgentBuilderTitle: { defaultMessage: 'Subagent Builder', id: 'ymiZvr' },
    subAgentBuilderDescription: {
        defaultMessage:
            'This list shows your extended agents, tools, and connectors organized by category. Create and manage subagents to extend your capabilities, add custom tools for specific tasks, and configure connectors to access external resources.',
        id: 'Qcakd7',
    },
    nameColumn: { defaultMessage: 'Name', id: 'HAlOn1' },
    typeColumn: { defaultMessage: 'Type', id: '+U6ozc' },
    descriptionColumn: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    searchPlaceholder: { defaultMessage: 'Search...', id: '0BUTMv' },
    searchBySubagent: { defaultMessage: 'Search by subagent', id: 'sCRBH3' },
    searchByIncidentTrigger: { defaultMessage: 'Search by incident trigger', id: 'Icfr5t' },
    searchByTool: { defaultMessage: 'Search by tool', id: 'Ij0ity' },
    subagentNameColumn: { defaultMessage: 'Subagent name', id: '2reUcp' },
    triggersColumn: { defaultMessage: 'Triggers', id: 'GnWmca' },
    handoffColumn: { defaultMessage: 'Handoff subagents', id: 'oTxjU2' },
    editYaml: { defaultMessage: 'Edit YAML', id: '0w+w97' },
    deleteConfirmTitle: { defaultMessage: 'Delete Items', id: 'sqGM6w' },
    deleteConfirmMessage: {
        defaultMessage:
            'Are you sure you want to delete {count, plural, one {this item} other {these # items}}? This action cannot be undone.',
        id: 'Wekzzh',
    },
    updateList: { defaultMessage: 'Update list', id: '8Vd2Kv' },
    outputTypeLabel: { defaultMessage: 'Output type', id: 'DQfSk8' },
    temperatureLabel: { defaultMessage: 'Temperature', id: 'cG0Q8M' },
    llmModelLabel: { defaultMessage: 'Model', id: 'rhSI1/' },
    maxReflectionLabel: { defaultMessage: 'Max reflections', id: 'aprHig' },
    criticPromptLabel: { defaultMessage: 'Critic prompt', id: 'dk4jPW' },
    criticOnHandOffLabel: { defaultMessage: 'Critic on handoff', id: 'FzjwEg' },
    instructionsTitle: { defaultMessage: 'Instructions', id: 'sV2v5L' },
    noInstructions: { defaultMessage: 'No instructions provided yet.', id: 'DhBWKr' },
    handoffDescriptionTitle: { defaultMessage: 'Handoff description', id: 'yLRKP5' },
    toolsSectionTitle: { defaultMessage: 'Tools', id: 'nUT0Lv' },
    noTools: { defaultMessage: 'No tools configured.', id: 'mgZVf8' },
    systemToolsSectionTitle: { defaultMessage: 'System tools', id: '2kRPCZ' },
    noSystemTools: { defaultMessage: 'No system tools configured.', id: 'c/bty0' },
    mcpToolsSectionTitle: { defaultMessage: 'MCP tools', id: 'GSCabm' },
    noMcpTools: { defaultMessage: 'No MCP tools configured.', id: 'KptJSd' },
    systemToolPluginLabel: { defaultMessage: 'Plugin', id: 'mVkTZZ' },
    connectorsSectionTitle: { defaultMessage: 'Connectors', id: '2mMJRv' },
    noConnectors: { defaultMessage: 'No connectors linked.', id: 'peOEU7' },
    handoffsSectionTitle: { defaultMessage: 'Handoffs', id: '/RdF9w' },
    noHandoffs: { defaultMessage: 'No handoffs configured.', id: 'tjE5Cb' },
    agentsAsToolsSectionTitle: { defaultMessage: 'Agents as tools', id: 'uMNRNl' },
    noAgentsAsTools: { defaultMessage: 'No agents-as-tools configured.', id: 'TRceX/' },
    commonToolsSectionTitle: { defaultMessage: 'Common tools', id: 'PU1wDo' },
    noCommonTools: { defaultMessage: 'No common tools configured.', id: '44Y8wQ' },
    commonPromptsSectionTitle: { defaultMessage: 'Common prompts', id: 'VvrxSY' },
    noCommonPrompts: { defaultMessage: 'No common prompts added.', id: 'SXcBLr' },
    agentConfigurationTitle: { defaultMessage: 'Agent configuration', id: '7Uk/61' },
    allowParallelLabel: { defaultMessage: 'Allow parallel tool calls', id: 'Kxd0Vr' },
    metadataSectionTitle: { defaultMessage: 'Metadata', id: '8Q504V' },
    toolDetailsTitle: { defaultMessage: 'Tool details · {name}', id: '83YwiU' },
    toolTypeLabel: { defaultMessage: 'Tool type', id: 'mZ3wu9' },
    toolDescriptionLabel: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    noDescription: { defaultMessage: 'No description provided.', id: 'YCnSYw' },
    connectorLabel: { defaultMessage: 'Connector', id: 'r8XsCU' },
    parametersSectionTitle: { defaultMessage: 'Query parameters', id: 'xet2Cv' },
    noParameters: { defaultMessage: 'No parameters defined.', id: '6TO45V' },
    toolNoConnectorsAvailable: { defaultMessage: 'No connectors available', id: 'ju7bLp' },
    toolAddAllButton: { defaultMessage: 'Add All', id: 'ehhpyJ' },
    toolNoParamsNeeded: { defaultMessage: 'No params needed', id: 'U914O/' },
    toolParamTypeText: { defaultMessage: 'Text', id: 'aA8bDw' },
    toolParamTypeYesNo: { defaultMessage: 'Yes/No', id: 'KgcF6B' },
    toolParamTypeDate: { defaultMessage: 'Date', id: 'P7PLVj' },
    toolQueryDetectedParamsLabel: { defaultMessage: '{count} params detected', id: 'GzKpRn' },
    connectorDetailsTitle: { defaultMessage: 'Connector details · {name}', id: 'nWJtZd' },
    connectorTypeLabel: { defaultMessage: 'Connector type', id: 'kQSvkP' },
    statusLabel: { defaultMessage: 'Status', id: 'tzMNF3' },
    connectorStatusEnabled: { defaultMessage: 'Enabled', id: 'V52jNn' },
    connectorStatusDisabled: { defaultMessage: 'Disabled', id: 'tthToS' },
    connectorDescriptionLabel: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    connectorAuthLabel: { defaultMessage: 'Authentication', id: 'YeKWbP' },
    kustoConfigurationTitle: { defaultMessage: 'Kusto Configuration', id: 'MaaxCM' },
    kustoModeLabel: { defaultMessage: 'Mode', id: 'mrOnjM' },
    kustoDatabaseLabel: { defaultMessage: 'Database', id: 'jjTzIr' },
    kustoQueryLabel: { defaultMessage: 'Query', id: 'qj71j1' },
    kustoFunctionLabel: { defaultMessage: 'Function', id: '4fPELu' },
    kustoClusterUriLabel: { defaultMessage: 'Cluster URI', id: 'bCzUPd' },
    severityLabel: { defaultMessage: 'Severity', id: 'vCAhII' },
    triggerServiceLabel: { defaultMessage: 'Service', id: 'n7yYXG' },
    noMetadata: { defaultMessage: 'No metadata provided.', id: 'Zt/L20' },
    noNodeSelected: { defaultMessage: 'Select a node to see its details.', id: 'oM0lfe' },
    agentSummaryTitle: { defaultMessage: 'Filtered agent', id: 'NlBiTm' },
    filteredAgentLabel: { defaultMessage: 'Currently showing {name}', id: 'F5b1Mw' },
    noAgentSelected: { defaultMessage: 'Select an agent to view its configuration.', id: 'IrCHQG' },
    overviewTabLabel: { defaultMessage: 'Overview', id: '9uOFF3' },
    yamlTabLabel: { defaultMessage: 'YAML', id: 'FvhvDO' },
    yamlEditorDescription: {
        defaultMessage: 'Review and edit the full {entityLabel} YAML.',
        id: 'AzTehW',
    },
    yamlValidationInvalid: { defaultMessage: 'Unable to parse YAML: {message}', id: 'xy47sz' },
    yamlValidationKindMissing: {
        defaultMessage: "YAML must include kind: '{expectedKind}'.",
        id: 'H1OQZj',
    },
    yamlValidationSpecMissing: { defaultMessage: 'YAML must include a spec object.', id: '4BVpFN' },
    yamlValidationNameMissing: {
        defaultMessage: 'YAML spec must include a name for the {entityLabel}.',
        id: 'mSoczp',
    },
    yamlValidationCollectionMissing: {
        defaultMessage: "YAML must include '{collectionName}' with at least one entry.",
        id: 'jxYyO2',
    },
    yamlValidationCollectionNameMissing: {
        defaultMessage: 'Each {entityLabel} must include a name.',
        id: 'ZBKZDe',
    },
    yamlValidationCollectionTypeMissing: {
        defaultMessage: 'Each {entityLabel} must include a type.',
        id: 'pSeneB',
    },
    toolsCollectionName: { defaultMessage: 'tools', id: 'rrN9ke' },
    connectorsCollectionName: { defaultMessage: 'connectors', id: 'B5kyn8' },
    yamlSaveSuccess: { defaultMessage: 'Configuration saved successfully.', id: 'nxmZHP' },
    yamlSaveError: { defaultMessage: 'Save failed: {message}', id: '9Dp2VU' },
    yamlSaveButton: { defaultMessage: 'Save YAML', id: 'T7fzjx' },
    yamlResetButton: { defaultMessage: 'Reset changes', id: 'PysW/n' },
    yamlEmptyState: {
        defaultMessage: 'Select a {entityLabel} to edit its configuration YAML.',
        id: 'rKdPkY',
    },
    yamlSavingLabel: { defaultMessage: 'Saving…', id: 'WUV28A' },
    yamlOpenButton: { defaultMessage: 'Edit YAML', id: '0w+w97' },
    yamlDialogTitle: { defaultMessage: 'Edit YAML configuration', id: 'coy1iv' },
    yamlInlineEditorTitle: { defaultMessage: 'Edit YAML', id: '0w+w97' },
    yamlInlineSaveChanges: { defaultMessage: 'Save Changes', id: '3VI9mt' },
    yamlErrorTitle: { defaultMessage: 'YAML error', id: 'X7mXLW' },
    yamlUnsavedChanges: { defaultMessage: 'You have unsaved changes.', id: 'LTDPn/' },
    yamlCloseButton: { defaultMessage: 'Close', id: 'rbrahO' },
    deleteExtendedAgentWarning: {
        defaultMessage:
            'Are you sure you want to delete this Extended Agent? The YAML configuration will be permanently removed and cannot be recovered and cannot be recovered. This action cannot be undone.',
        id: 'hWVnHh',
    },
    deleteExtendedToolWarning: {
        defaultMessage:
            'Are you sure you want to delete this Extended Tool? The YAML configuration will be permanently removed and cannot be recovered and cannot be recovered. This action cannot be undone.',
        id: 'bK0VQe',
    },
    deleteSkillTitle: {
        defaultMessage: 'Delete skill',
        id: 'k+phZu',
    },
    deleteSkillWarning: {
        defaultMessage:
            'Are you sure you want to delete this skill? The skill configuration will be permanently removed and cannot be recovered. This action cannot be undone.',
        id: 'iqsZgT',
    },
    deleteSkillNotificationError: {
        defaultMessage: 'Failed to delete skill {name}',
        id: 'JTS+Zf',
    },
    deletingSkills: {
        defaultMessage: '{count, plural, one {Deleting skill...} other {Deleting {count} skills...}}',
        id: 'Yymgt5',
    },
    deleteSkillSuccess: {
        defaultMessage: '{count, plural, one {Skill "{name}" deleted successfully} other {{count} skills deleted successfully}}',
        id: 'SllKh/',
    },
    deleteSkillPartialError: {
        defaultMessage: 'Deleted {successCount} skills, but {failureCount} failed to delete',
        id: 'SM/hWJ',
    },
    skillNameRequired: {
        defaultMessage: 'Skill name is required',
        id: 'HeFlq1',
    },
    failedToSaveSkill: {
        defaultMessage: 'Failed to save skill',
        id: 'LbYKes',
    },
    expandToShowMore: {
        defaultMessage: 'Expand to show more',
        id: 'ix9hVm',
    },
    expandToShowAllTools: {
        defaultMessage: 'Expand to show all {count} tools',
        id: 'R7A5z4',
    },
    collapseToShowLess: {
        defaultMessage: 'Collapse to show less',
        id: 'DS/6lY',
    },
    toolCount: {
        defaultMessage: '{count, plural, one {1 tool} other {{count} tools}}',
        id: 'QZnXvZ',
    },
    extendedAgentDeletionReminder: {
        defaultMessage:
            'New agents can take a few seconds to appear in the graph. If you do not see it right away, refresh the list after a brief moment.',
        id: '4QLM52',
    },
    incidentTriggerNameTitle: { id: '/qCaiZ', defaultMessage: 'Incident trigger name' },
    incidentImpactedService: { id: 'fdCjVS', defaultMessage: 'Impacted service' },
    incidentTitleContains: { id: 'brxlTt', defaultMessage: 'Title contains' },
    scheduledTriggerNameTitle: { id: 'C6Jt4c', defaultMessage: 'Scheduled task name' },
    scheduleTitle: { id: 'hGQqkW', defaultMessage: 'Schedule' },
    kustoToolName: { id: 'cg54iX', defaultMessage: 'Kusto tool name' },
    connectorStatus: { id: 'cLHCHE', defaultMessage: 'Connector status' },
    onLabel: { id: 'Zh+5A6', defaultMessage: 'On' },
    offLabel: { id: 'OvzONl', defaultMessage: 'Off' },
    completedLabel: { id: '95stPq', defaultMessage: 'Completed' },
    connectedStatus: { id: 'IvjoDS', defaultMessage: 'Connected' },
    disconnectedStatus: { id: 'FZeQlc', defaultMessage: 'Disconnected' },
    incidentTriggers: { id: 'vS6Lmt', defaultMessage: 'Incident triggers' },
    kustoTools: { id: 'Y47Dwm', defaultMessage: 'Kusto tools' },
    agentDatagrid: { id: 'kxokJj', defaultMessage: 'Agent datagrid' },
    noEntityFound: { defaultMessage: 'No {entity} found', id: 'EVA81S' },
    service: { defaultMessage: 'Service', id: 'n7yYXG' },
    parameter: { defaultMessage: 'Parameter', id: 'VU4BBu' },
    noParametersConfigured: { defaultMessage: 'No parameters configured', id: 'Vipo+I' },
    agentAutonomy: { defaultMessage: 'Agent Autonomy', id: 'iY7P1k' },
    incidents: { defaultMessage: 'Incidents', id: 'mtr3R4' },
    incidentDescription: { defaultMessage: 'To view incidents triggered, go to Incidents', id: 'PXJ+oX' },
    goToIncidents: { defaultMessage: 'Go to Incidents', id: '/7GPtw' },
    runs: { defaultMessage: 'Runs', id: 'W1Qs5O' },
    runDescription: { defaultMessage: 'To view runs, go to Agent Runs', id: 'ifdupA' },
    goToScheduledTasks: { defaultMessage: 'Go to Scheduled Task', id: 'O/gthC' },
    handoffInstructions: { defaultMessage: 'Handoff Instructions', id: 'hF/2EL' },
    category: { defaultMessage: 'Category', id: 'ccXLVi' },
    builtInTool: { defaultMessage: 'Built-in Tool', id: 'EQa6dK' },
    customTool: { defaultMessage: 'Custom Tool', id: 'ONbwY2' },
    mcpTool: { defaultMessage: 'MCP Tool', id: 'LstsP/' },
    builtInTools: { defaultMessage: 'Built-in Tools', id: 'Eu772h' },
    url: { defaultMessage: 'URL', id: 'bWjdfa' },

    // Relationship Builder
    relationshipNameRequired: { defaultMessage: 'Enter a name before creating this item.', id: 'YT6JU9' },
    relationshipCreationNotice: {
        defaultMessage: 'Your new {entityType} will be ready in a few seconds. Feel free to keep editing while we connect it.',
        id: 'tU3A8E',
    },
    toolLowercase: { defaultMessage: 'tool', id: 'YrlQBL' },
    agentLowercase: { defaultMessage: 'agent', id: 'snui0/' },
    connectorLowercase: { defaultMessage: 'connector', id: '44QmgP' },
    relationshipAgentFieldsRequired: {
        defaultMessage: 'Enter a name and instructions before creating this agent.',
        id: '35Pxrz',
    },
    relationshipBuilderTitle: { defaultMessage: 'Connect related agents and tools', id: 'CNd+KS' },
    relationshipBuilderOptionalTitle: { defaultMessage: 'Relationships (optional)', id: 't6/9PU' },
    relationshipBuilderDescription: {
        defaultMessage: 'Link this agent to its handoffs and tools so the relationships are ready when it goes live.',
        id: 'sTodCB',
    },
    thisAgentPlaceholder: { defaultMessage: 'This agent', id: '47Qksb' },
    relationshipToolLabel: { defaultMessage: 'Tools this agent can call', id: '33HEdR' },
    relationshipAddHandoffLabel: { defaultMessage: 'Add handoff to existing agent', id: 'qvviEg' },
    relationshipSelectAgent: { defaultMessage: 'Select an agent to hand off to', id: '8rNTuV' },
    relationshipAddButton: { defaultMessage: 'Add', id: '2/2yg+' },
    relationshipAddToolLabel: { defaultMessage: 'Add existing tool', id: 'RI59Ed' },
    relationshipToolSearchPlaceholder: { defaultMessage: 'Search tools…', id: 'xvTDgy' },
    relationshipToolSearchEmpty: {
        defaultMessage: 'No tools available. Try creating one or clearing your filters.',
        id: 'm3Npit',
    },
    relationshipToolCategoryLabel: { defaultMessage: 'Category: {category}', id: 'cfbpDd' },
    relationshipToolCategoryFallback: { defaultMessage: 'General', id: '1iEPTM' },
    relationshipSelectTool: { defaultMessage: 'Select a tool to connect', id: 'DMEE1o' },
    relationshipCreateToolTitle: { defaultMessage: 'Create a new tool', id: 'Dw2xIY' },
    relationshipCollapse: { defaultMessage: 'Collapse', id: 'W/V6+Y' },
    relationshipExpand: { defaultMessage: 'Expand', id: '0oLj/t' },
    relationshipCreateAndConnect: { defaultMessage: 'Create and connect', id: '3Pq+hf' },
    relationshipCreateAgentTitle: { defaultMessage: 'Create a new handoff agent', id: 'wQdhYo' },
    relationshipCurrentHandoffs: { defaultMessage: 'Handoff Agents (Optional)', id: 'FVOAIQ' },
    relationshipNoHandoffAgents: { defaultMessage: 'No handoff agents available', id: '9q75fe' },
    relationshipNoHandoffs: { defaultMessage: 'No handoffs added yet.', id: 'HiBsED' },
    relationshipRemoveHandoff: {
        defaultMessage: 'Remove handoff {name}',
        id: 'FE2ukR',
    },
    relationshipCurrentTools: { defaultMessage: 'Connected tools', id: 'XM+FCY' },
    relationshipNoTools: { defaultMessage: 'No tools connected yet.', id: 'mJXXeS' },
    relationshipDismiss: { defaultMessage: 'Dismiss', id: 'TDaF6J' },
    relationshipQuickActionTooltip: { defaultMessage: 'Connect handoffs and tools', id: 'IJX2gV' },
    relationshipQuickDialogTitle: { defaultMessage: 'Connect relationships for {name}', id: 'VTkRGg' },
    relationshipQuickDialogTitleFallback: { defaultMessage: 'Connect relationships', id: 'uFaAgq' },
    relationshipQuickDialogDescription: {
        defaultMessage: 'Link this agent to existing handoffs and tools or create new ones without leaving the graph.',
        id: '//Zb9l',
    },
    relationshipQuickDelayNotice: {
        defaultMessage: 'It may take a few seconds for new agents or tools to appear in the graph after they are created.',
        id: 'sUfX4j',
    },
    relationshipQuickNoAgentSelected: {
        defaultMessage: 'Select an agent to manage its relationships.',
        id: 'Tkn9dK',
    },
    relationshipQuickExistingTitle: { defaultMessage: 'Connect existing items', id: 'J7FnS1' },
    relationshipQuickCreateTitle: { defaultMessage: 'Create and connect new items', id: 'jM/rXB' },
    relationshipQuickCreateToolHeader: { defaultMessage: 'Create a new tool', id: 'Dw2xIY' },
    relationshipQuickCreateAgentHeader: { defaultMessage: 'Create a new handoff agent', id: 'wQdhYo' },
    relationshipContextBannerHeading: { defaultMessage: 'Context: {agentName}', id: 'X3fjbS' },
    relationshipContextAgentSubtext: {
        defaultMessage: 'Creates a handoff for {agentName}.',
        id: 'izQgEq',
    },
    relationshipContextToolSubtext: {
        defaultMessage: 'Attaches to {agentName}.',
        id: 'rG1sas',
    },
    relationshipSummaryTitle: { defaultMessage: 'Relationship preview', id: 'F8UOXS' },
    relationshipSummaryAgent: {
        defaultMessage: '{sourceAgentName} —handoff→ {targetAgentName}',
        id: 'ISsGbC',
    },
    relationshipSummaryTool: {
        defaultMessage: '{sourceAgentName} —uses→ {toolName}',
        id: 'kq7D3O',
    },
    relationshipPendingAgentName: { defaultMessage: 'New agent', id: 'KR79he' },
    relationshipPendingToolName: { defaultMessage: 'New tool', id: 'fT73QI' },
    relationshipQuickAlreadyHandoff: {
        defaultMessage: '{handoffName} is already a handoff for {agentName}.',
        id: 'UjNXaV',
    },
    relationshipQuickAddHandoffSuccess: {
        defaultMessage: 'Added handoff to {handoffName} for {agentName}.',
        id: 'sVJ+oz',
    },
    relationshipQuickAlreadyTool: {
        defaultMessage: '{toolName} is already connected to {agentName}.',
        id: 'tb6uBb',
    },
    relationshipQuickToolMissing: {
        defaultMessage: "We couldn't find tool {toolName}. Refresh and try again.",
        id: 'OO5Y94',
    },
    relationshipQuickAddToolSuccess: {
        defaultMessage: 'Connected tool {toolName} to {agentName}.',
        id: 'IEIivr',
    },
    relationshipQuickAddToolsSuccess: {
        defaultMessage: 'Added tools to {agentName}.',
        id: 'YKwGp6',
    },
    relationshipQuickCreateToolSuccess: {
        defaultMessage: 'Created {toolName} and connected it to {agentName}.',
        id: 'N0wOFf',
    },
    relationshipQuickCreateAgentSuccess: {
        defaultMessage: 'Created {handoffName} and added it as a handoff for {agentName}.',
        id: 'T/G3bv',
    },
    relationshipQuickActionAddHandoffInfo: {
        defaultMessage: 'Select an existing agent below to add as a handoff.',
        id: 'Oshix6',
    },
    relationshipQuickActionAddToolInfo: {
        defaultMessage: 'Select an existing tool below to connect.',
        id: 'Zo9lK6',
    },
    relationshipQuickCreateAgentReminder: {
        defaultMessage: "When you finish creating this agent, we'll automatically add it as a handoff for {agentName}.",
        id: 'pbpjik',
    },
    reviewRelationshipsHeading: { defaultMessage: 'Relationships', id: 'N7lPfx' },
    reviewRelationshipAgent: {
        defaultMessage: 'Will add as handoff to {agentName}.',
        id: '5MSxYS',
    },
    reviewRelationshipTool: {
        defaultMessage: 'Will attach to {agentName} as a tool.',
        id: 'XEB3Fn',
    },
    createAndLink: { defaultMessage: 'Create & Link', id: 'fbr20l' },
    relationshipLinkFailedAgent: {
        defaultMessage: 'Agent created, but linking to {agentName} failed. Retry link.',
        id: 'ijOojL',
    },
    relationshipLinkFailedTool: {
        defaultMessage: 'Tool created, but attaching to {agentName} failed. Retry link.',
        id: '08bD52',
    },
    retryLink: { defaultMessage: 'Retry link', id: '7TVRcx' },
    quickCreateAgentSuccess: {
        defaultMessage: 'Created {agentName} and linked it as a handoff for {sourceAgentName}. Choose what you’d like to do next.',
        id: 'mopY52',
    },
    quickCreateAgentSuccessNoSource: {
        defaultMessage: 'Created {agentName}. Pick a next step below to keep building.',
        id: 'sHc8w5',
    },
    quickCreateAgentSuccessLink: { defaultMessage: 'Go to Incident management', id: '7Cy5xk' },
    testAgentButton: { defaultMessage: 'Test agent', id: 'sYCfrb' },
    testLiveAgent: { defaultMessage: 'Test live agent', id: '8J3eYt' },
    testLiveAgentTooltip: {
        defaultMessage:
            'This test is running against the version of your agent deployed at the time the test was started. Restart the test to run against the latest deployed version.',
        id: 'fZP/Ln',
    },
    restartTestButton: { defaultMessage: 'Restart test', id: 'cM9da4' },
    resumeTestThread: {
        defaultMessage: 'Test restarted with your latest agent changes. Send a message to continue testing.',
        id: 'jqM7Pr',
    },
    startTestThread: { defaultMessage: 'Send a message to start testing your agent.', id: '6lHI9u' },
    testThreadNoAgent: { defaultMessage: 'Create your agent first to enable testing.', id: '1/xsyG' },

    // Prompt Improvement
    improveInstructionsButton: { defaultMessage: 'Improve with AI', id: 'f0LQra' },
    improveInstructionsTooltip: { defaultMessage: 'Use AI to enhance and validate your agent instructions', id: 'JMdyE9' },
    reviewInstructionsButton: { defaultMessage: 'Review', id: 'R+J5ox' },
    reviewInstructionsTooltip: { defaultMessage: 'Review AI suggestions and warnings', id: '1hiiU/' },
    improvingInstructions: { defaultMessage: 'Improving instructions...', id: 'v/3x6O' },
    improveInstructionsError: { defaultMessage: 'Failed to improve instructions. Please try again.', id: 'MJUuF5' },
    improvementApplied: { defaultMessage: 'AI improvements applied to instructions', id: 'PFUTWt' },
    improvementDiscarded: { defaultMessage: 'AI suggestions discarded', id: 'L+ewYY' },
    improvementSuggestions: { defaultMessage: 'AI Suggestions', id: 't3n6rd' },
    improvementWarnings: { defaultMessage: 'Warnings', id: 'VSWkne' },
    improvementApply: { defaultMessage: 'Apply Suggestions', id: 'hGMSK3' },
    improvementDiscard: { defaultMessage: 'Discard', id: 'nmpevl' },
    noImprovementSuggestions: { defaultMessage: 'No suggestions available', id: '3R3pgd' },
    noImprovementWarnings: { defaultMessage: 'No warnings found', id: 'EBpelF' },
    improvementFollowUps: { defaultMessage: 'Follow-up questions', id: '94QRkK' },
    suggestionsButton: { defaultMessage: 'Suggestions', id: 'Hv0XJn' },
    suggestionsTooltip: { defaultMessage: 'Preview AI recommendations without changing your instructions', id: 'hijOHm' },
    loadingSuggestions: { defaultMessage: 'Fetching suggestions...', id: 'Zu+xDc' },
    improvedInstructionsLabel: { defaultMessage: 'Suggested instructions', id: 'vVpzJQ' },
    improveInstructionsChatUnavailable: {
        defaultMessage: 'AI chat service is currently unavailable. Please try again later.',
        id: 'cLC2fM',
    },
    improveInstructionsInvalidRequest: {
        defaultMessage: 'Invalid request. Please check your instructions and try again.',
        id: 'blYlGH',
    },
    improveInstructionsServerError: {
        defaultMessage: 'Server error occurred. Please try again in a few moments.',
        id: 'Rk/SSB',
    },
    improveInstructionsForbidden: {
        defaultMessage: 'You do not have permission to use this feature.',
        id: 'O3Ak71',
    },

    // Meta Agent Override
    metaAgentOverrideLabel: { defaultMessage: 'Override default meta agent', id: '18C/Jh' },
    metaAgentOverrideHelp: {
        defaultMessage: 'Replace the default meta agent with a specialized version tailored for this extended agent',
        id: 'RR+13c',
    },
    metaAgentOverrideDescription: {
        defaultMessage: 'This overrides the default meta agent to remove general Azure knowledge and provide a more focused experience.',
        id: '9GSKOR',
    },
    metaAgentOverrideYesLabel: { defaultMessage: 'Yes, override with custom meta agent', id: 'MOttDA' },
    metaAgentOverrideNoLabel: { defaultMessage: 'No, keep default meta agent', id: 'sNofwe' },
    metaAgentOverrideReasonTooltip: {
        defaultMessage:
            "Overriding the meta agent is recommended because this extended agent doesn't need the default agent's world knowledge of Azure resources, providing a more focused experience.",
        id: 'VQlvNn',
    },
    metaAgentOverrideInfo: {
        defaultMessage: 'This will create both your agent ("{agentName}") and a separate "meta_agent" for orchestration.',
        id: 'k4dB65',
    },
    metaAgentOverridePlaceholderName: { defaultMessage: 'YourAgent', id: 'jEmOoh' },

    // Agent Memory
    agentMemoryLabel: { defaultMessage: 'Give access to knowledge base', id: '+OMfjA' },
    agentMemoryHelp: {
        defaultMessage:
            'Giving access to your knowledge base gives the agent more context about your services to make better informed decisions',
        id: '2CunXT',
    },
    agentMemoryTooltip: {
        defaultMessage: 'When enabled, the SearchMemory tool and knowledge base prompts will be automatically added',
        id: 'WjqsyE',
    },
    agentMemoryEnabled: {
        defaultMessage:
            'Knowledge base is enabled for this agent. The SearchMemory tool and knowledge base prompts will be automatically included.',
        id: 'FsIud6',
    },

    metaAgentAlreadyExistsMessage: {
        defaultMessage: 'A meta agent override already exists in your system.',
        id: 'VnySGb',
    },
    noNodesFound: {
        defaultMessage: 'No nodes found',
        id: 'sNuzQE',
    },
    quickCreateAddIncidentTrigger: { defaultMessage: 'Add incident trigger', id: 'eq57j2' },
    quickCreateAddScheduledTask: { defaultMessage: 'Add scheduled task', id: 'fCiXKq' },
    quickCreateAddExistingTools: { defaultMessage: 'Add existing tools', id: 'SDsdCi' },
    quickCreateCreateNewKustoTool: { defaultMessage: 'Create new Kusto tool', id: 'mhKk7c' },
    quickCreateCreateNewPythonTool: { defaultMessage: 'Create new Python tool', id: 'vOzK9a' },
    quickCreateAddExistingSubagent: { defaultMessage: 'Add existing subagent', id: 'KJHRU/' },
    quickCreateCreateNewSubagent: { defaultMessage: 'Create new subagent', id: '+h4pn9' },
    createIncidentTrigger: { defaultMessage: 'Create incident trigger', id: 'HQc/Lf' },
    editIncidentTrigger: { defaultMessage: 'Edit incident trigger', id: 'S6we8v' },
    incidentTriggerStep: { defaultMessage: 'Incident trigger', id: 'THFIRB' },
    incidentTriggerName: { defaultMessage: 'Trigger name', id: 'zDcGyS' },
    triggerDetails: { defaultMessage: 'Trigger details', id: '4C44ie' },
    incidentTriggerNamePlaceholder: { defaultMessage: 'Enter a descriptive name', id: 'UNQfKX' },
    incidentsPreviewStep: { defaultMessage: 'Incidents preview', id: 'DD2Wjk' },
    responseSubagent: { defaultMessage: 'Response subagent', id: 'GMhzu1' },
    responseSubagentPlaceholder: { defaultMessage: 'Select a response subagent', id: 'D97db3' },
    createIncidentTriggerNoPlatformMessage: { defaultMessage: 'You need an incident platform to add an incident trigger.', id: 'eRwVws' },
    createIncidentTriggerNoPlatformButton: { defaultMessage: 'Connect an incident platform', id: 'ItmLyp' },
    addHandoffToExistingAgent: { defaultMessage: 'Add handoff to existing agent', id: 'qvviEg' },
    addHandoffFromExistingAgent: { defaultMessage: 'Add handoff from existing agent', id: '4RwjwU' },
    noAgentsAvailableForHandoff: { defaultMessage: 'No agents available for handoff', id: '79kwlH' },
    noTargetAgentSpecified: { defaultMessage: 'No target agent specified', id: 'PXJi2R' },
    noSourceAgentSpecified: { defaultMessage: 'No source agent specified', id: 'FS0tWa' },
    subagentName: { defaultMessage: 'Subagent name', id: '2reUcp' },
    noAgentsFoundForHandoff: { defaultMessage: 'No agents found', id: '451B6Z' },
    addSubagent: { defaultMessage: 'Add subagent', id: 'PbaaPs' },
    mcpTools: { defaultMessage: 'MCP tools', id: 'GSCabm' },
    allTools: { defaultMessage: 'All tools', id: '0Qocmw' },
    selectTool: { defaultMessage: 'Select tool', id: 'BakoFg' },
    selectToolWithName: { defaultMessage: 'Select tool {toolName}', id: 'FWou/7' },
    selectAllTools: { defaultMessage: 'Select all tools', id: 'O25MZU' },
    selectAllToolsInGroup: { defaultMessage: 'Select all tools in {groupName}', id: 'bR/LRB' },
    deselectAllTools: { defaultMessage: 'Deselect all tools', id: 'vZRYf4' },
    deselectAllToolsInGroup: { defaultMessage: 'Deselect all tools in {groupName}', id: 'xGK1X2' },
    clearAll: { defaultMessage: 'Clear all', id: 'QW+Q5N' },
    addTools: { defaultMessage: 'Add tools', id: '5fVEet' },
    createSubagentTitle: { defaultMessage: 'Create a subagent', id: 'I4PbZq' },
    editSubagentTitle: { defaultMessage: 'Edit subagent', id: 'khI1mJ' },
    formTab: { defaultMessage: 'Form', id: 'baRFiF' },
    yamlTab: { defaultMessage: 'YAML', id: 'FvhvDO' },
    subagentNamePlaceholder: { defaultMessage: 'Enter a descriptive name', id: 'UNQfKX' },
    refineWithAi: { defaultMessage: 'Refine with AI', id: 'uwY8Nf' },
    viewAiSuggestions: { defaultMessage: 'View AI suggestions', id: '03qRza' },
    agentInstructionsPlaceholder: { defaultMessage: 'Enter instructions', id: 'AbpmRv' },
    agentHandoffInstructions: { defaultMessage: 'Handoff instructions', id: '8u2W0L' },
    agentHandoffInstructionsPlaceholder: { defaultMessage: 'Enter handoff instructions', id: '/+WtRc' },
    advancedSettings: { defaultMessage: 'Advanced settings', id: 'zhoVUT' },
    handoffSubagents: { defaultMessage: 'Handoff subagents', id: 'oTxjU2' },
    handoffSubagentsPlaceholder: { defaultMessage: 'Select subagents for handoff', id: 'VBAOA8' },
    chooseTools: { defaultMessage: 'Choose tools', id: 'vzH0q5' },
    closePanel: { defaultMessage: 'Close panel', id: 'RAjqKb' },
    suggestedImprovements: { defaultMessage: 'Suggested improvements', id: 'TdvJVw' },
    suggestions: { defaultMessage: 'Suggestions', id: 'Hv0XJn' },
    warnings: { defaultMessage: 'Warnings', id: 'VSWkne' },
    improvedInstructions: { defaultMessage: 'Improved instructions', id: 'XyUQa7' },
    improvedHandoffInstructions: { defaultMessage: 'Improved handoff instructions', id: 'hoHnYn' },
    createMetaAgentNotificationTitle: { defaultMessage: 'Create meta agent', id: 'qrs+1B' },
    createMetaAgentNotificationInProgress: { defaultMessage: 'Creating meta agent', id: '+6/51l' },
    createMetaAgentNotificationSuccess: { defaultMessage: 'Created meta agent', id: '+nnQeH' },
    createMetaAgentNotificationFailure: { defaultMessage: 'Failed to create meta agent. Error: {errorMessage}', id: 'pw1ueF' },
    createSubagentNotificationTitle: { defaultMessage: 'Create subagent {agentName}', id: '4lv5gO' },
    createSubagentNotificationInProgress: { defaultMessage: 'Creating subagent {agentName}', id: 'SB7SRx' },
    createSubagentNotificationSuccess: { defaultMessage: 'Created subagent {agentName}', id: 'CIAL+k' },
    createSubagentNotificationFailure: { defaultMessage: 'Failed to create subagent {agentName}. Error: {errorMessage}', id: 'Bn/txo' },
    updateSubagentNotificationTitle: { defaultMessage: 'Update subagent {agentName}', id: '6RNwei' },
    updateSubagentNotificationInProgress: { defaultMessage: 'Updating subagent {agentName}', id: 'YBHvOV' },
    updateSubagentNotificationSuccess: { defaultMessage: 'Updated subagent {agentName}', id: 'QP91Js' },
    updateSubagentNotificationFailure: { defaultMessage: 'Failed to update subagent {agentName}. Error: {errorMessage}', id: 'GpcNV2' },
    addHandoffNotificationTitle: { defaultMessage: 'Add handoff from {sourceAgent} to subagent {targetAgent}', id: 'mVB9Bd' },
    addHandoffNotificationInProgress: { defaultMessage: 'Adding handoff from {sourceAgent} to subagent {targetAgent}', id: 'Vs/db/' },
    addHandoffNotificationSuccess: { defaultMessage: 'Added handoff from {sourceAgent} to subagent {targetAgent}', id: '+Y8m6A' },
    addHandoffNotificationFailure: {
        defaultMessage: 'Failed to add handoff from {sourceAgent} to subagent {targetAgent}. Error: {errorMessage}',
        id: 'RMD0Mj',
    },
    openInVisualView: { defaultMessage: 'Open in visual view', id: 'L/ct40' },
    subAgentCreateMenuLabel: { defaultMessage: 'Subagent', id: 'Q++yMM' },
    subAgentCreateMenuDescription: { defaultMessage: 'Performs specialized tasks on behalf of the SRE Agent.', id: 'stbbGt' },
    metaAgentCreateMenuLabel: { defaultMessage: 'SRE Agent', id: '+WRusC' },
    metaAgentCreateMenuDescription: { defaultMessage: 'Allow subagent capabilities to override the SRE Agent.', id: 'ocQGCd' },
    incidentTriggerCreateMenuLabel: { defaultMessage: 'Incident trigger', id: 'THFIRB' },
    incidentTriggerCreateMenuDescription: {
        defaultMessage: 'Automatically starts a subagent in response to an incident or alert.',
        id: 'hdfKe+',
    },
    createSubagentStep: { defaultMessage: 'Create subagent', id: 'O9C6j/' },
    createIncidentTriggerWithLearnings: { defaultMessage: 'Create incident trigger with learnings', id: 'VAsLEP' },
    incidentTriggerWithLearningsCreateMenuLabel: { defaultMessage: 'Incident trigger with learnings', id: 'Ml5W34' },
    incidentTriggerWithLearningsCreateMenuDescription: {
        defaultMessage: 'Incident trigger that includes past incident learnings and analysis.',
        id: '8Tlee1',
    },
    scheduledTaskTriggerCreateMenuLabel: { defaultMessage: 'Scheduled task trigger', id: 'ioRcwS' },
    scheduledTaskTriggerCreateMenuDescription: { defaultMessage: 'Starts a task at a defined time or interval.', id: 'JA/VO+' },
    kustoToolCreateMenuLabel: { defaultMessage: 'Kusto tool', id: 'v6cujo' },
    kustoToolCreateMenuDescription: {
        defaultMessage: 'Query tool the subagent uses to collect data from Azure Data Explorer.',
        id: 'iYJ/Ha',
    },
    skillCreateMenuLabel: { defaultMessage: 'Skill', id: 'GFhSwY' },
    skillCreateMenuDescription: {
        defaultMessage: 'Reusable knowledge and instructions that enhance agent capabilities.',
        id: 'nR0FVC',
    },
    pythonToolCreateMenuLabel: { defaultMessage: 'Python tool', id: 'rFU0oo' },
    pythonToolCreateMenuDescription: {
        defaultMessage: 'Custom Python function to extend agent capabilities with AI-assisted code generation.',
        id: 'oyf+bG',
    },
    // Python Tool Terminal strings
    pythonToolTestPlayground: { defaultMessage: 'Test Playground', id: 'qzMeS2' },
    pythonToolReady: { defaultMessage: 'Ready', id: 'IZFEUg' },
    pythonToolRunning: { defaultMessage: 'Running...', id: 'oyZN19' },
    pythonToolRun: { defaultMessage: 'Run', id: 'KiXNvz' },
    pythonToolParameters: { defaultMessage: 'Parameters:', id: 'qTWwGw' },
    pythonToolRequired: { defaultMessage: '*required', id: 'pXPlXr' },
    pythonToolOptional: { defaultMessage: 'optional', id: 'V4KNjk' },
    pythonToolNoParametersRequired: { defaultMessage: 'No parameters required', id: 'iJcrHj' },
    pythonToolTestPassed: { defaultMessage: 'Test Passed', id: 'xpMDZO' },
    pythonToolWantToImprove: { defaultMessage: 'Want to improve it?', id: 'kGmr6n' },
    pythonToolFillInParameters: { defaultMessage: 'Fill in parameters and click Run to test', id: 'kVOq9V' },
    pythonToolCtrlEnter: { defaultMessage: 'Ctrl+Enter', id: 'bO7sKf' },
    pythonToolTimeoutTitle: { defaultMessage: 'Timeout in seconds (5-900)', id: 'lBZfVb' },
    // Python Tool PromptView strings
    pythonToolPreviousTestFailed: { defaultMessage: 'Previous test failed:', id: 'EPi9lK' },
    pythonToolKeepCurrentCode: { defaultMessage: 'Keep Current Code', id: 'kopJ66' },
    // Python Tool Info Panel strings
    pythonToolCodeSectionTitle: { defaultMessage: 'Python Code', id: 'VfjG62' },
});

export const PlaygroundResources = defineMessages({
    dialogTitle: {
        defaultMessage: 'Agent playground',
        id: 'WsJuyI',
    },
    dialogSubtitle: {
        defaultMessage: 'Design, test, and refine agents without touching production.',
        id: 'yeL9WV',
    },
    headerTitle: {
        defaultMessage: 'Playground',
        id: 'XqRXu6',
    },
    viewTesterTooltip: {
        defaultMessage: 'Test your agent in a simple chat interface',
        id: '2ePVf6',
    },
    viewTesterAriaLabel: {
        defaultMessage: 'Tester - Test your agent',
        id: 'wjfh7Z',
    },
    viewAuthorTestTooltip: {
        defaultMessage: 'Author agent configuration and test',
        id: 'bs6oPY',
    },
    viewAuthorTestAriaLabel: {
        defaultMessage: 'Author and Test - Configure and test',
        id: 'h+sisx',
    },
    viewEvaluateTooltip: {
        defaultMessage: 'Full workflow with quality evaluation',
        id: 'EOt0+9',
    },
    viewEvaluateAriaLabel: {
        defaultMessage: 'Author, Test & Evaluate - Full workflow',
        id: 'wBgTQg',
    },
    collapsePanelAriaLabel: {
        defaultMessage: 'Collapse panel',
        id: 'BuziI2',
    },
    expandChatPreviewTitle: {
        defaultMessage: 'Expand chat preview',
        id: 'OrQ4EY',
    },
    expandConfigurationPanelTitle: {
        defaultMessage: 'Expand configuration panel',
        id: 'v3xWf6',
    },
    restartChatTitle: {
        defaultMessage: 'Restart chat conversation',
        id: 'SR33a/',
    },
    moreActionsAriaLabel: {
        defaultMessage: 'More actions',
        id: 'S8/4ZI',
    },
    closeButton: {
        defaultMessage: 'Close',
        id: 'rbrahO',
    },
    comingSoon: {
        defaultMessage: 'Playground actions are coming soon.',
        id: 'A4ZrJb',
    },
    noSelectionMessage: {
        defaultMessage: 'Select an agent or tool to start a playground session.',
        id: 'XDSe35',
    },
    agentSummary: {
        defaultMessage: 'You are previewing {name}.',
        id: 'y87pob',
    },
    toolSummary: {
        defaultMessage: 'You are previewing {name}.',
        id: 'y87pob',
    },
    summaryLastPublishedLabel: {
        defaultMessage: 'Last published {time}',
        id: '0eZALE',
    },
    summaryLastPublishedUnknown: {
        defaultMessage: 'Not yet published',
        id: 'iJCsVz',
    },
    summaryLastAppliedLabel: {
        defaultMessage: 'Draft synced {time}',
        id: 'Oc1AVw',
    },
    summaryLastAppliedNever: {
        defaultMessage: 'Draft not yet synced',
        id: 'SmFb5S',
    },
    evaluationBannerMessage: {
        defaultMessage: 'Get AI-powered quality insights and improvement suggestions',
        id: 'HMZIeG',
    },
    evaluationBannerCta: {
        defaultMessage: 'Try Evaluation',
        id: 'Jsy4yb',
    },
    autoApplyEnabledTooltip: {
        defaultMessage: 'Configuration changes are auto-applied (disable auto-apply to use manual apply)',
        id: 'Z5ntkd',
    },
    noPendingChangesTooltip: {
        defaultMessage: 'No pending configuration changes to apply',
        id: 'AYrXUB',
    },
    autoApplyEnabledLabel: {
        defaultMessage: 'Auto-apply ON',
        id: 'L7EoQf',
    },
    autoApplyDisabledLabel: {
        defaultMessage: 'Auto-apply OFF',
        id: 'WhuvJH',
    },
    summaryInsightsStatusLabel: {
        defaultMessage: 'Insights: {status}',
        id: 'fINN7t',
    },
    summarySwitchToYaml: {
        defaultMessage: 'Switch to YAML',
        id: 'LGZkPZ',
    },
    summarySwitchToForm: {
        defaultMessage: 'Switch to form',
        id: '4oK1Me',
    },
    applyChangesButton: {
        defaultMessage: 'Apply changes',
        id: '3n8o15',
    },
    applyChangesTooltip: {
        defaultMessage: 'Apply your edits and refresh insights',
        id: '6w0QQX',
    },
    applyChangesErrorTooltip: {
        defaultMessage: 'Resolve YAML errors before applying changes',
        id: 'dkt4ac',
    },
    openPlaygroundButton: {
        defaultMessage: 'Open in playground',
        id: 'c3TO1+',
    },
    setupTitle: {
        defaultMessage: 'How do you want to get started?',
        id: 'fE5E6W',
    },
    setupDescription: {
        defaultMessage: 'You can edit this entity directly or spin up a copy once cloning is available.',
        id: 'FeBVnC',
    },
    editExistingTooltip: {
        defaultMessage: 'Begin editing the current configuration in the playground.',
        id: 'wk6Nft',
    },
    editExistingButton: {
        defaultMessage: 'Edit existing',
        id: 'vZWQHk',
    },
    copyExistingButton: {
        defaultMessage: 'Make a copy',
        id: 'l5a3zV',
    },
    copyComingSoonTooltip: {
        defaultMessage: 'Creates a draft agent and tool workspace for testing without touching production. Coming soon...',
        id: 'CEdEmD',
    },
    configurationTargetLabel: {
        defaultMessage: 'Configuration target',
        id: '9SQWi9',
    },
    configurationAgentTabLabel: {
        defaultMessage: 'Agent',
        id: 'QGVI63',
    },
    configurationToolTabLabel: {
        defaultMessage: 'Tool',
        id: 'h6183G',
    },
    formTabLabel: {
        defaultMessage: 'Form',
        id: 'baRFiF',
    },
    yamlTabLabel: {
        defaultMessage: 'YAML',
        id: 'FvhvDO',
    },
    yamlEditorComingSoon: {
        defaultMessage: 'YAML editing is coming soon for this entity.',
        id: '9xBQKf',
    },
    formComingSoon: {
        defaultMessage: 'Form editing is coming soon for this entity.',
        id: 'ZPP6n7',
    },
    agentPreviewTabLabel: {
        defaultMessage: 'Chat preview',
        id: 'VIYXlx',
    },
    toolPreviewTabLabel: {
        defaultMessage: 'Tool testing',
        id: 'c1ECOO',
    },
    agentPreviewUnavailable: {
        defaultMessage: 'Chat preview is only available for agents.',
        id: 'l2Ni9H',
    },
    toolPreviewUnavailable: {
        defaultMessage: 'Tool testing is unavailable for this entity.',
        id: 'GCQn0j',
    },
    toolPreviewEmpty: {
        defaultMessage: 'No tools are linked yet. Add tools on the left to enable testing.',
        id: 'yYJHyl',
    },
    toolFormUnavailable: {
        defaultMessage: 'Tool configuration is unavailable for this selection.',
        id: 'Z+wlsh',
    },
    toolFormNoTools: {
        defaultMessage: 'Link a tool or create a new one to configure it here.',
        id: 'CGn7Yd',
    },
    toolFormSelectorLabel: {
        defaultMessage: 'Select a tool',
        id: 'GdgrWi',
    },
    toolFormSelectPrompt: {
        defaultMessage: 'Select a linked tool to edit',
        id: 'qquh75',
    },
    toolFormLoading: {
        defaultMessage: 'Loading tool details...',
        id: 'F8xMoD',
    },
    toolFormSystemToolReadOnly: {
        defaultMessage: '{name} is read-only in the playground.',
        id: '+TnQ2+',
    },
    toolFormCreateNewKusto: {
        defaultMessage: 'Create new Kusto tool',
        id: 'mhKk7c',
    },
    toolFormAgentToolsGroup: {
        defaultMessage: 'Agent tools',
        id: 'w87Yom',
    },
    toolFormAvailableToolsGroup: {
        defaultMessage: 'More tools',
        id: 'wI+r41',
    },
    toolFormSystemToolsGroup: {
        defaultMessage: 'System tools',
        id: '2kRPCZ',
    },
    toolFormNewToolPrompt: {
        defaultMessage: 'Start filling in the fields above to define this tool.',
        id: 'QzM+2e',
    },
    toolPreviewSelectorLabel: {
        defaultMessage: 'Select a tool to test',
        id: '+hOwpi',
    },
    toolPreviewSelectPlaceholder: {
        defaultMessage: 'Choose a tool',
        id: '+KyrhG',
    },
    toolPreviewComingSoon: {
        defaultMessage: 'Interactive testing for {name} is coming soon.',
        id: 'T+FE9b',
    },
    toolPreviewSystemToolNotice: {
        defaultMessage: '{name} is a system tool and cannot be run from the playground yet.',
        id: 'sIwc3/',
    },
    toolPreviewUnsupportedType: {
        defaultMessage: 'Testing for {name} is coming soon. Only Kusto tools are supported right now.',
        id: 'Ze5Ejt',
    },
    previewRequiresSetup: {
        defaultMessage: 'Finish setup to enable previews.',
        id: 'br2BTb',
    },
    playgroundEmptyStateSubtitle: {
        defaultMessage: "Experiment with your agent's configuration and see real-time results",
        id: 'tjklHQ',
    },
    yamlReadOnlyNotice: {
        defaultMessage: 'System tools are read-only. View the YAML for reference.',
        id: 'haOtj7',
    },
    previewUpdatedBadge: {
        defaultMessage: 'Preview updated',
        id: '5Lc8g/',
    },
    insightsRerunLink: {
        defaultMessage: 'Re-run insights',
        id: 'nx7y+W',
    },
    insightsCardTitle: {
        defaultMessage: 'Agent Quality',
        id: 'B7EJpi',
    },
    insightsRefreshButton: {
        defaultMessage: 'Refresh insights',
        id: 'NGhUmq',
    },
    insightsRefreshTooltip: {
        defaultMessage: 'Analyse the current prompt, tools, and test runs to get tailored guidance.',
        id: '2xE9ci',
    },
    insightsFetching: {
        defaultMessage: 'Analysing configuration…',
        id: 'Df+1hk',
    },
    insightsLastRun: {
        defaultMessage: 'Analysed {time}',
        id: 'LIm7q8',
    },
    insightsNotAnalysedYet: {
        defaultMessage: 'Insights not yet analysed',
        id: 'YuvMS0',
    },
    insightsNoData: {
        defaultMessage: 'Run insights to see guidance for this configuration.',
        id: 'u60c6a',
    },
    insightsConfidenceLabel: {
        defaultMessage: '{score}% confidence',
        id: 'egubDj',
    },
    insightsPromptHighlightsHeader: {
        defaultMessage: 'Prompt highlights',
        id: 'pCjcdM',
    },
    insightsToolSuggestionsHeader: {
        defaultMessage: 'Tool recommendations',
        id: 'VpVoQZ',
    },
    insightsChatDiagnosticsHeader: {
        defaultMessage: 'Chat diagnostics',
        id: 'FLN/px',
    },
    insightsActionItemsHeader: {
        defaultMessage: 'Priority fixes',
        id: 'Y2PFzA',
    },
    insightsNotesHeader: {
        defaultMessage: 'Notes',
        id: '7+Domh',
    },
    insightsError: {
        defaultMessage: 'We could not generate insights right now. Try again in a moment.',
        id: 'LXOsKd',
    },
    insightsStaleBadge: {
        defaultMessage: 'Changes pending analysis',
        id: 'Hh8It5',
    },
    insightsToolFailureFallback: {
        defaultMessage: 'Tool test failed but no error details were provided.',
        id: 'arE3XO',
    },
    insightsDetailsShow: {
        defaultMessage: 'Show details',
        id: 's2XIgr',
    },
    insightsDetailsHide: {
        defaultMessage: 'Hide details',
        id: 'X5Q310',
    },
    insightsCollapsedSummary: {
        defaultMessage: 'Run insights to track confidence and recommended fixes.',
        id: 'EEb00g',
    },
    toastChatRestartReminder: {
        defaultMessage: 'Please restart the chat to see your changes.',
        id: 'bjLeN1',
    },
    toastApplyFailedTitle: {
        defaultMessage: 'Failed to apply changes',
        id: 'Zq5BCQ',
    },
    toastApplyFailedBody: {
        defaultMessage: 'Please try again or check the console for details.',
        id: 'W5zi/x',
    },
    toastUndoLabel: {
        defaultMessage: 'Undo (10s)',
        id: 'mMgwbu',
    },
    toastChatRestarted: {
        defaultMessage: 'Chat restarted with new configuration.',
        id: 'ZBpAJ/',
    },
    playgroundResizeHandleLabel: {
        defaultMessage: 'Resize configuration and preview panels',
        id: 'QpCzB2',
    },
    relativeTimeMoments: {
        defaultMessage: 'moments ago',
        id: 'Cv2vtV',
    },
    relativeTimeMinutes: {
        defaultMessage: '{count, plural, one {# minute ago} other {# minutes ago}}',
        id: 'aLBTVj',
    },
    relativeTimeHours: {
        defaultMessage: '{count, plural, one {# hour ago} other {# hours ago}}',
        id: 'LupnQU',
    },
    relativeTimeDays: {
        defaultMessage: '{count, plural, one {# day ago} other {# days ago}}',
        id: '8OCpdQ',
    },
    chatFindingUserTitle: {
        defaultMessage: 'User at {time}',
        id: 'wGLrHW',
    },
    chatFindingAgentTitle: {
        defaultMessage: 'Agent at {time}',
        id: 't+IV9f',
    },
    chatFindingTimeFallback: {
        defaultMessage: 'recent activity',
        id: 'LFb8Ku',
    },
    // Improved UX strings
    startTestingButton: {
        defaultMessage: 'Start testing',
        id: '42RAxK',
    },
    continueEditingButton: {
        defaultMessage: 'Continue editing',
        id: 'rVbKiP',
    },
    setupImprovedTitle: {
        defaultMessage: 'Ready to test your agent?',
        id: 'xf9BKi',
    },
    setupImprovedDescription: {
        defaultMessage: 'Choose your approach below. You can always switch between editing and testing.',
        id: 'Q2CgqG',
    },
    editExistingImprovedButton: {
        defaultMessage: 'Edit & test live agent',
        id: '6CxEZS',
    },
    editExistingImprovedTooltip: {
        defaultMessage: 'Make changes to the current agent configuration and test immediately.',
        id: 'dO3eHj',
    },
    confidenceGoal: {
        defaultMessage: 'Goal: 85%+ confidence',
        id: 'kNEmDU',
    },
    insightsCallToAction: {
        defaultMessage: 'Get AI recommendations to improve your agent',
        id: '/OUWsL',
    },
    insightsUsingLatestYaml: {
        defaultMessage: 'Using latest YAML changes',
        id: 'BbTpTJ',
    },
    insightsLevelLabelLegend: {
        defaultMessage: 'Playbook Legend',
        id: 'VWq2cH',
    },
    insightsLevelLabelPro: {
        defaultMessage: 'Ops Pro',
        id: 'DFQKku',
    },
    insightsLevelLabelRising: {
        defaultMessage: 'Rising Specialist',
        id: 'f9xN/g',
    },
    insightsLevelLabelRookie: {
        defaultMessage: 'Rookie Analyst',
        id: 'xXHSO2',
    },
    insightsLevelMessageLegend: {
        defaultMessage: '{metric} at {score}%—you are outperforming the target confidence. Ship it! 🚀',
        id: 'dkqYUR',
    },
    insightsLevelMessagePro: {
        defaultMessage: '{metric} is at {score}%. Continue refining to move toward the next tier.',
        id: 'wjEBlr',
    },
    insightsLevelMessageRising: {
        defaultMessage: '{metric} lands at {score}%. Sharpen instructions and tools to climb faster.',
        id: 'TQ0n+a',
    },
    insightsLevelMessageRookie: {
        defaultMessage: '{metric} at {score}%. Start experimenting—your upgrade path awaits.',
        id: '5xbHr/',
    },
    insightsScoreXpLabel: {
        defaultMessage: '{score} XP',
        id: '6B0Aua',
    },
    insightsDeltaPositive: {
        defaultMessage: '+{delta} since last run',
        id: 'y0KXuM',
    },
    insightsDeltaNegative: {
        defaultMessage: '-{delta} since last run',
        id: 'polqL7',
    },
    insightsDeltaNeutral: {
        defaultMessage: 'No change since last run',
        id: '6Y8rFa',
    },
    insightsNextTier: {
        defaultMessage: '{delta, plural, one {# confidence point to reach {tier}.} other {# confidence points to reach {tier}.}}',
        id: 'uNTENz',
    },
    insightsMaxTierMessage: {
        defaultMessage: 'Max tier achieved—keep refining to stay ahead.',
        id: 'methez',
    },
    initialConfidenceMessage: {
        defaultMessage: 'Improve your agent prompt to increase confidence',
        id: 'vG08m6',
    },
    confidenceInfoTooltip: {
        defaultMessage:
            'Confidence is calculated based on your agent prompt, available tools, and chat interactions. Click refresh to get AI-powered recommendations.',
        id: '7sbcy0',
    },
    intentMetLabel: {
        defaultMessage: 'Intent Met Score',
        id: 'pA7W9m',
    },
    intentMetTooltip: {
        defaultMessage: 'How well your agent is meeting user intents based on chat interactions. Updates after conversations.',
        id: 'heMr0D',
    },
    quickStart: {
        defaultMessage: 'Quick start',
        id: 'OYKAqF',
    },
    advancedSettings: {
        defaultMessage: 'Advanced settings',
        id: 'zhoVUT',
    },
    playgroundChatEmptyBadge: {
        defaultMessage: 'Playground mode',
        id: 'S5UkvS',
    },
    playgroundChatEmptyTitle: {
        defaultMessage: 'Welcome to the playground',
        id: 'L7gQfJ',
    },
    playgroundChatEmptySubtitle: {
        defaultMessage: 'Tune agent behavior in a safe space before you publish changes.',
        id: 'ene/8x',
    },
    playgroundChatEmptyBenefitsHeading: {
        defaultMessage: 'Playground highlights',
        id: 'CMbKXm',
    },
    playgroundChatEmptyBenefitPrompt: {
        defaultMessage: 'Iterate on instructions without impacting production threads.',
        id: 'WsILsq',
    },
    playgroundChatEmptyBenefitTools: {
        defaultMessage: 'Validate tool wiring and connector responses in context.',
        id: '21vNrm',
    },
    playgroundChatEmptyBenefitInsights: {
        defaultMessage: 'Capture AI-driven insights on confidence and next tweaks.',
        id: 'eYClyG',
    },
    playgroundChatEmptyDescription: {
        defaultMessage: 'Get started with the curated scenarios below or craft your own prompts.',
        id: '2m6oj5',
    },
    playgroundChatEmptySyncing: {
        defaultMessage: 'Auto-applying changes… latest draft will be used for new tests.',
        id: 'KAYnaH',
    },
    playgroundChatEmptyRecentInsight: {
        defaultMessage: 'Last confidence: {score}%',
        id: '1jBkyH',
    },
    playgroundChatEmptyStreakLabel: {
        defaultMessage: 'Insight streak: {count}',
        id: 'WdmBr4',
    },
    playgroundChatSendPromptLabel: {
        defaultMessage: 'Run scenario',
        id: 'z0uI4M',
    },
    playgroundChatAgentFallback: {
        defaultMessage: 'this agent',
        id: '/9FZdx',
    },
    playgroundChatPromptWarmupTitle: {
        defaultMessage: 'Warm-up scenario',
        id: 'gGfQUL',
    },
    playgroundChatPromptWarmupDescription: {
        defaultMessage: 'Check if the agent introduces itself properly and outlines its capabilities.',
        id: 'ZBlAho',
    },
    playgroundChatPromptWarmupMessage: {
        defaultMessage: '{name}, give me your mission briefing and how you partner with engineers.',
        id: 'Y0BC+F',
    },
    playgroundChatPromptStressTitle: {
        defaultMessage: 'Stress test',
        id: 'YN1/XY',
    },
    playgroundChatPromptStressDescription: {
        defaultMessage: 'Probe for multi-step diagnostics to see if the agent can orchestrate tools effectively.',
        id: 'FeBneB',
    },
    playgroundChatPromptStressMessage: {
        defaultMessage: '{name}, I need diagnostics for intermittent latency spikes across AKS clusters eastus & westeurope.',
        id: '531otx',
    },
    playgroundChatPromptAuditTitle: {
        defaultMessage: 'Audit check',
        id: 'b0+PAq',
    },
    playgroundChatPromptAuditDescription: {
        defaultMessage: 'Validate how the agent summarizes risk and next steps for leadership updates.',
        id: 'tbxHpV',
    },
    playgroundChatPromptAuditMessage: {
        defaultMessage: '{name}, audit today’s incidents and draft a leadership update with priority blockers.',
        id: 'DWD03m',
    },
    qualityDrawerTitle: {
        defaultMessage: 'Quality watcher',
        id: 'VVy70J',
    },
    qualityDrawerCloseButton: {
        defaultMessage: 'Close quality watcher',
        id: '5M+Mu/',
    },
    qualityDrawerLoadingTitle: {
        defaultMessage: 'Evaluating agent…',
        id: 'r2cpCI',
    },
    qualityDrawerLoadingSubtitle: {
        defaultMessage: 'Analyzing configuration for improvement recommendations.',
        id: 'zHZdPM',
    },
    qualityDrawerHighlightsTitle: {
        defaultMessage: 'Highlights',
        id: 'KGmQjH',
    },
    qualityDrawerQuickFixesTitle: {
        defaultMessage: 'Quick fixes',
        id: '0Krg21',
    },
    qualityDrawerAutoLabel: {
        defaultMessage: 'Auto apply',
        id: 'vIBOch',
    },
    qualityDrawerQuickFixLabel: {
        defaultMessage: 'Select fix',
        id: '1eaczG',
    },
    qualityDrawerPreviewShow: {
        defaultMessage: 'Preview diff',
        id: 'MOhprT',
    },
    qualityDrawerPreviewHide: {
        defaultMessage: 'Hide diff',
        id: 'JiENoF',
    },
    qualityDrawerNoFindings: {
        defaultMessage: 'No quick fixes needed.',
        id: 'Qn5bey',
    },
    qualityDrawerEmpty: {
        defaultMessage: 'Run an analysis to see tailored fixes.',
        id: '5ukccA',
    },
    qualityDrawerUpdated: {
        defaultMessage: 'Updated {time}',
        id: '0aJxT2',
    },
    qualityDrawerUpdatedNever: {
        defaultMessage: 'No recent analysis',
        id: 'WolNVP',
    },
    qualitySummaryNotAnalyzed: {
        defaultMessage: 'Agent quality not scored yet. Run a check to reveal opportunities.',
        id: 'dGa0bG',
    },
    qualitySummaryRunning: {
        defaultMessage: 'Scoring agent quality… fresh quick fixes will land here shortly.',
        id: '/bWE45',
    },
    qualitySummaryStale: {
        defaultMessage: 'Agent quality is {score}/100 but may be outdated. Refresh to keep guidance current.',
        id: 'INiE0x',
    },
    qualitySummaryWithFindings: {
        defaultMessage: 'Agent quality is {score}/100 with {count, plural, one {# quick fix ready} other {# quick fixes ready}}.',
        id: '7Or82y',
    },
    qualitySummaryNoFindings: {
        defaultMessage: 'Agent quality is {score}/100. No quick fixes right now.',
        id: 'JbMs9l',
    },
    qualityFindingsButtonLabel: {
        defaultMessage: 'Review {count, plural, =0 {findings} one {# quick fix} other {# quick fixes}}',
        id: 'O+srpo',
    },
    qualityDrawerOpenButton: {
        defaultMessage: 'View findings',
        id: 'HUPaMQ',
    },
    qualityDrawerOpenTooltip: {
        defaultMessage: 'Open watcher drawer',
        id: 'zUNfOA',
    },
    qualityDrawerCloseTooltip: {
        defaultMessage: 'Hide watcher panel',
        id: 'Xmty/4',
    },
    qualityDrawerHideButton: {
        defaultMessage: 'Hide findings',
        id: 'xJLI97',
    },
    qualityRibbonSelection: {
        defaultMessage: '{count, plural, one {# quick fix selected} other {# quick fixes selected}}',
        id: 'r2ZGnB',
    },
    qualityRibbonProjected: {
        defaultMessage: 'Projected +{lift} overall',
        id: 'KaLLyk',
    },
    qualityRibbonProjectedScore: {
        defaultMessage: 'Estimated total {score}% confidence',
        id: '3sQV42',
    },
    qualityRibbonClearButton: {
        defaultMessage: 'Clear',
        id: '/GCoTA',
    },
    qualityRibbonApplyButton: {
        defaultMessage: 'Apply fixes',
        id: 'noxLT+',
    },
    qualityRibbonApplyingLabel: {
        defaultMessage: 'Applying…',
        id: 'isZXYe',
    },
    qualityScoreLabel: {
        defaultMessage: 'Quality {score}/100',
        id: 'BNH9Uy',
    },
    qualityStatusRunning: {
        defaultMessage: 'Scoring…',
        id: 'Ql/5RS',
    },
    qualityStatusFresh: {
        defaultMessage: 'Fresh',
        id: 'OBoUb6',
    },
    qualityStatusFreshWithTime: {
        defaultMessage: 'Fresh • {time}',
        id: 'vz94UY',
    },
    qualityStatusStale: {
        defaultMessage: 'Out of date',
        id: 'osuyeD',
    },
    qualityStatusNotAnalyzed: {
        defaultMessage: 'Not analyzed',
        id: 'W8LUUc',
    },
    qualityPanelTitle: {
        defaultMessage: 'Quality Evaluation',
        id: 'iihhVc',
    },
    qualityOverallLabel: {
        defaultMessage: 'Overall Quality',
        id: 'O0JI8M',
    },
    qualityIntentLabel: {
        defaultMessage: 'Intent Match',
        id: 'cYwv8P',
    },
    qualityIntentTooltip: {
        defaultMessage: 'Intent Match (1-5)',
        id: 's2GWth',
    },
    qualityRunButtonLabel: {
        defaultMessage: 'Score agent quality',
        id: 'dwkMC2',
    },
    qualityRunTooltip: {
        defaultMessage: 'Score this agent and get tailored fixes.',
        id: 'rrQX3M',
    },
    qualityAutoApplyLabel: {
        defaultMessage: 'Auto-apply',
        id: 'Ip7Gd3',
    },
    qualityAutoApplyTooltip: {
        defaultMessage: 'Automatically push YAML edits to the agent during testing.',
        id: 'mAinBm',
    },
    exportAnalysisLabel: {
        defaultMessage: 'Export analysis',
        id: 'ASLbZd',
    },
    playgroundApplyingChangesTitle: {
        defaultMessage: 'Applying your changes',
        id: 'CppiJ4',
    },
    playgroundApplyingChangesMessage: {
        defaultMessage: 'Your changes are being processed and will appear in the chat preview momentarily.',
        id: 'cJGGY4',
    },
    systemToolTesterExecute: {
        defaultMessage: 'Execute Tool',
        id: 'PQgsh+',
    },
    systemToolTesterExecuting: {
        defaultMessage: 'Executing...',
        id: 'g8ctzH',
    },
    systemToolTesterExecutingStatus: {
        defaultMessage: 'Executing {name}...',
        id: 'YXOz1v',
    },
    systemToolTesterEmptyState: {
        defaultMessage: 'Click "Execute Tool" to test this system tool',
        id: 'Jrrm2K',
    },
    systemToolTesterParametersHeading: {
        defaultMessage: 'Test Parameters',
        id: 'KNwLvk',
    },
    systemToolTesterNoParameters: {
        defaultMessage: 'No parameters required for this tool.',
        id: 'hkgUC5',
    },
    systemToolTesterToolNameLabel: {
        defaultMessage: 'Tool Name',
        id: 'INiSE2',
    },
    systemToolTesterDescriptionLabel: {
        defaultMessage: 'Description',
        id: 'Q8Qw5B',
    },
    systemToolTesterPluginLabel: {
        defaultMessage: 'Plugin',
        id: 'mVkTZZ',
    },
    systemToolTesterCategoryLabel: {
        defaultMessage: 'Category',
        id: 'ccXLVi',
    },
    systemToolTesterThreadHint: {
        defaultMessage: 'Auto-generated if left empty',
        id: 'Z4bt4u',
    },
    systemToolTesterThreadPlaceholder: {
        defaultMessage: 'Auto-generated',
        id: 'nFpcjX',
    },
    systemToolTesterParameterPlaceholder: {
        defaultMessage: 'Enter {name}',
        id: 'M+QCSD',
    },
});

export const ThreadTraceResources = defineMessages({
    actionPlan: { defaultMessage: 'Action plan', id: 'Ec1M0J' },
    agentPrompt: { defaultMessage: 'Agent prompt', id: 'KsuM4D' },
    agent: { defaultMessage: 'Agent', id: 'QGVI63' },
    close: { defaultMessage: 'Close', id: 'rbrahO' },
    collapseSpanWithId: { defaultMessage: 'Collapse span with id: {id}', id: 'bquSks' },
    collapse: { defaultMessage: 'Collapse', id: 'W/V6+Y' },
    description: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    dismiss: { defaultMessage: 'Dismiss', id: 'TDaF6J' },
    expandSpanWithId: { defaultMessage: 'Expand span with id: {id}', id: 'owa0nd' },
    expand: { defaultMessage: 'Expand', id: '0oLj/t' },
    incidentId: { defaultMessage: 'Incident ID', id: 'MB9ceM' },
    incidentDetails: { defaultMessage: 'Incident details', id: 'GaA28c' },
    incidentPlatform: { defaultMessage: 'Incident platform', id: 'EZBG/A' },
    incidentResponsePlan: { defaultMessage: 'Incident response plan: ', id: 'gct+2s' },
    incidentStatus: { defaultMessage: 'Incident status: ', id: 'u3AxgJ' },
    incident: { defaultMessage: 'Incident', id: 'zaYxwd' },
    input: { defaultMessage: 'Input', id: 'it6Lig' },
    output: { defaultMessage: 'Output', id: 'fio5op' },
    resolved: { defaultMessage: 'Resolved', id: 'W6nSYE' },
    responseToUser: { defaultMessage: 'Response to user', id: '4OCLSf' },
    stepsToResolution: { defaultMessage: 'Steps to resolution', id: 'hS+M7h' },
    subagent: { defaultMessage: 'Subagent', id: 'Q++yMM' },
    toolAndSubagentActivitySentToUser: { defaultMessage: 'Tool and subagent activity sent to user', id: '/Z1fHN' },
    toolDetails: { defaultMessage: 'Tool details', id: 'yX3QcY' },
    toolUsageId: { defaultMessage: 'Tool usage ID', id: 'x25AjF' },
    toolInputArguments: { defaultMessage: 'Arguments passed to tool', id: 'oyOUhv' },
    toolOutputResult: { defaultMessage: 'Result returned from tool', id: '87DT+o' },
    tool: { defaultMessage: 'Tool', id: 'h6183G' },
    tree: { defaultMessage: 'Tree', id: '4/aFfy' },
    userPrompt: { defaultMessage: 'User prompt', id: 'mbb8vl' },
    response: { defaultMessage: 'Response', id: 'MgdnPi' },
    systemPrompt: { defaultMessage: 'System prompt', id: '/rHnDp' },
    modelThinking: { defaultMessage: 'Model thinking', id: 'x/ocZz' },
    reasoning: { defaultMessage: 'Reasoning', id: 'Aw3qRf' },
    tokenUsage: { defaultMessage: 'Token usage', id: 'oDA49h' },
    tokenUsageDetails: { defaultMessage: 'Input: {input} | Output: {output} | Total: {total}', id: '4biGe0' },
    messageVisibleToUser: { defaultMessage: 'Message visible to user', id: 'effV14' },
    user: { defaultMessage: 'User', id: 'EwRIOm' },
    secondsWithLabel: { defaultMessage: '{seconds} sec', id: '6+mbvY' },
    spanIdCompleted: { defaultMessage: '{id} completed', id: 'MHkWzD' },
    spanIdFailed: { defaultMessage: '{id} failed', id: 'uWmprZ' },
    tokensWithLabel: { defaultMessage: '{tokens}t', id: 'PZc06m' },
    agentHandoff: { defaultMessage: 'Handoff', id: '2+atxY' },
    modelGeneration: { defaultMessage: 'Model Generation', id: 'eVjPEQ' },
    agentResponse: { defaultMessage: 'Agent Response', id: '0i3Qrd' },
    agentThinking: { defaultMessage: 'Reasoning', id: 'Aw3qRf' },
    handoffFromAgent: { defaultMessage: 'From agent', id: 'UNXega' },
    handoffToAgent: { defaultMessage: 'To agent', id: 'yhZZjG' },
    handoffReasoning: { defaultMessage: 'Handoff reasoning', id: 'v84FgY' },
    agentInvoked: { defaultMessage: 'Agent invoked', id: 'ncB14A' },
    subAgentInvoked: { defaultMessage: 'Subagent invoked', id: 'sjlioi' },
    agentName: { defaultMessage: 'Agent name', id: 'ctcA0c' },
    modelDetails: { defaultMessage: 'Model details', id: 'OUJuir' },
    modelName: { defaultMessage: 'Name', id: 'HAlOn1' },
    modelTemperature: { defaultMessage: 'Temperature', id: 'cG0Q8M' },
    noTraceDataFound: { defaultMessage: 'No trace data found for this thread.', id: 'E+jVAN' },
    couldNotParseTrace: { defaultMessage: 'Trace data found, but could not parse into spans.', id: 'lCsNuL' },
    traceParsedWithErrors: { defaultMessage: 'Trace data parsed with errors.', id: 'OPBqEN' },
    traceParsedWithWarnings: { defaultMessage: 'Trace data parsed with warnings.', id: 'ElqLuA' },
    failedToLoadTraceData: { defaultMessage: 'Failed to load trace data: {message}', id: 'fjgqNG' },
    hide: { defaultMessage: 'Hide', id: 'VA/Z1S' },
    show: { defaultMessage: 'Show', id: 'K7AkdL' },
    traceInfoTitle: { defaultMessage: 'About traces', id: 'W3d1We' },
    traceInfoDescription: {
        defaultMessage:
            'Traces visualize the internal activity of your agent session. Data is queried from Application Insights custom events. Select an item on the left to view details.',
        id: 'HZoKF5',
    },
    traceDataAvailable: { defaultMessage: 'What you can see', id: 'cNB4/9' },
    traceDataList: {
        defaultMessage:
            '• Agent start/end events with timing\n• Model generation calls with token usage (prompt, completion, total)\n• Model thinking (internal reasoning from o1/Claude reasoning models)\n• Agent reasoning (from reasoningScratchPad)\n• Agent responses (from notifyUserMessage)\n• Tool executions with inputs and outputs\n• Agent handoffs between meta_agent and specialized agents\n• Azure CLI command status (PendingAuthorization, Failed, Success)\n• User messages that triggered agent actions',
        id: 'RPuopt',
    },
});

export const DeleteConfirmationDialogResources = defineMessages({
    titleSingle: { defaultMessage: '{actionVerb} {itemType}?', id: 'BV7QgK' },
    titleMultiple: { defaultMessage: '{actionVerb} {count} {itemType}s?', id: 'DDSwQB' },
    messageSingle: {
        defaultMessage: 'This will permanently {actionVerb} this {itemType}. Are you sure you want to {actionVerb} this {itemType}?',
        id: 'Q4f1wN',
    },
    messageMultiple: {
        defaultMessage: 'This will permanently {actionVerb} {count} {itemType}s. Are you sure you want to {actionVerb} these {itemType}s?',
        id: '8eOoHt',
    },
    selectedItemsLabel: { defaultMessage: 'Selected {itemType}', id: 'ChQQJ0' },
    cancel: { defaultMessage: 'Cancel', id: '47FYwb' },
});

export const ConnectorsResources = defineMessages({
    addAConnector: { defaultMessage: 'Add a connector', id: 'ri3YXY' },
    connectorsDescription: {
        defaultMessage: 'Add a connector to give the agent additional tools for automating incident handling.',
        id: 'REOGTJ',
    },
    addConnector: { defaultMessage: 'Add connector', id: 'QDa8Q+' },
    connector: { defaultMessage: 'connector', id: '44QmgP' },
    connectorCapital: { defaultMessage: 'Connector', id: 'r8XsCU' },
    connectors: { defaultMessage: 'Connectors', id: '2mMJRv' },
    chooseAConnector: { defaultMessage: 'Choose a connector', id: 'GpHWFC' },
    addMcpServer: { defaultMessage: 'Add MCP server', id: '3dsWh5' },
    bearerToken: { defaultMessage: 'Bearer token', id: 'q3e4cf' },
    customHeaders: { defaultMessage: 'Custom headers', id: 'Li/Qrf' },
    key: { defaultMessage: 'Key', id: 'EcglP9' },
    value: { defaultMessage: 'Value', id: 'GufXy5' },
    customHeadersKeyPlaceholder: { defaultMessage: 'Enter custom header key', id: '6CrAJC' },
    customHeadersValuePlaceholder: { defaultMessage: 'Enter custom header value', id: 'r5UlDs' },
    compiledConnectionString: { defaultMessage: 'Compiled connection string', id: 'OP7f9a' },
    teamsChannelLink: { defaultMessage: 'Teams channel link', id: 'AvHD3R' },
    provideChannelLinkError: { defaultMessage: 'Please provide a channel link', id: 'fyaE5g' },
    channelId: { defaultMessage: 'Channel ID', id: 'DgsR1U' },
    teamsGroupId: { defaultMessage: 'Teams group ID', id: 'ErUgLz' },
    connectorNameValidationMessage: {
        defaultMessage:
            'Name must start with a letter and can only contain letters, numbers, and hyphens. The name must be non-empty and less than {maxLength} characters.',
        id: 'DK708b',
    },
    authenticationMethodPlaceholder: { defaultMessage: 'Select authentication method', id: 'v1LtqB' },
    patOrApiKey: { defaultMessage: 'Personal access token (PAT) or API key', id: 'ooDMAp' },
    patOrApiKeyPlaceholder: { defaultMessage: 'Enter your PAT or API key', id: 'FMDuHb' },
    custom: { defaultMessage: 'Custom', id: 'Sjo1P4' },
    userProvidedConnector: { defaultMessage: 'User provided connector', id: '4Rhz/i' },
    mcpServer: { defaultMessage: 'MCP server', id: 'JvYXxh' },
    setUpConnector: { defaultMessage: 'Set up connector', id: 'yVe+kY' },
    reviewAndAdd: { defaultMessage: 'Review + add', id: '3h7ZKg' },
    service: { defaultMessage: 'Service', id: 'n7yYXG' },
    databaseQueryConnector: { defaultMessage: 'Database query connector', id: 'CbntSG' },
    databaseIndexingConnector: { defaultMessage: 'Database indexing connector', id: 'KqwLPK' },
    documentationConnector: { defaultMessage: 'Documentation connector', id: 'rlwohK' },
    predefinedQueriesDescription: {
        defaultMessage: 'The agent uses predefined queries for structured logs, telemetry, and time series data.',
        id: 'IomdzL',
    },
    queryGenerationDescription: {
        defaultMessage: 'The agent generates queries by learning about your logs, telemetry, and time series data.',
        id: 'MUVpbF',
    },
    documentationDescription: {
        defaultMessage: 'The agent references documentation and files to understand your projects and processes.',
        id: 'qQh4Xu',
    },
    azureDataExplorer: { defaultMessage: 'Azure Data Explorer', id: 'l0UKyP' },
    azureDevops: { defaultMessage: 'Azure DevOps', id: 'D3rb1K' },
    gitHub: { defaultMessage: 'GitHub', id: 'wO9wb5' },
    gitHubMcpServer: { defaultMessage: 'GitHub MCP server', id: 'y/m6Ep' },
    githubDescription: {
        defaultMessage: 'The agent accesses GitHub repositories, features, and actions, including issue tracking and pull requests.',
        id: 'BBT3jd',
    },
    authentication: { defaultMessage: 'Authentication', id: 'YeKWbP' },
    authenticationMethod: { defaultMessage: 'Authentication method', id: 'Vs3jMi' },
    status: { defaultMessage: 'Status', id: 'tzMNF3' },
    source: { defaultMessage: 'Source', id: 'aH4De2' },
    connected: { defaultMessage: 'Connected', id: 'IvjoDS' },
    noSearchResults: {
        defaultMessage: 'No connectors match your search',
        id: 'xPuGYM',
    },
    noSearchResultsDescription: {
        defaultMessage: 'Try different search terms or clear your search to see all connectors.',
        id: '8fw5Cz',
    },
    emptyStateTitle: {
        defaultMessage: 'Extend your agent’s capabilities with connectors',
        id: 'PKvAvF',
    },
    emptyStateDescription: {
        defaultMessage: 'Connectors give the agent more tools to take action.',
        id: 'ekSxhn',
    },
    remove: { defaultMessage: 'Remove', id: 'G/yZLu' },
    connectorsDescriptionLearnMore: { defaultMessage: 'Learn more about connectors', id: 'Kfaepo' },
    duplicateNameError: { defaultMessage: 'A connector with this name already exists', id: 'qf1aUJ' },
    urlKustoFormatError: { defaultMessage: 'The url must be in the format: {format}', id: '3sNyAo' },
    namePlaceholder: { defaultMessage: 'Enter connector name', id: '+2NFJn' },
    urlPlaceholder: { defaultMessage: 'Enter endpoint', id: '1xTayQ' },
    teamsChannelLinkPlaceholder: { defaultMessage: 'Enter Teams channel link', id: 'B/qM5p' },
    repositoryUrl: { defaultMessage: 'Repository URL', id: 'AA/tRJ' },
    serviceRepositoryUrl: { defaultMessage: '{0} repository URL', id: 'DXm10s' },
    url: { defaultMessage: 'URL', id: 'bWjdfa' },
    managedIdentity: { defaultMessage: 'Managed identity', id: 'Ys9AIu' },
    identityPlaceholder: { defaultMessage: 'Select identity', id: '8RdOD0' },
    useManagedIdentityAsFic: { defaultMessage: 'Use managed identity as federated identity credential', id: 'SWvH2K' },
    useManagedIdentityAsFicDescription: {
        defaultMessage:
            'Enable this to use the managed identity to acquire a federated identity credential for cross-tenant authentication.',
        id: 'HZ13wA',
    },
    federatedClientId: { defaultMessage: 'Federated client ID', id: 'PTp9MX' },
    federatedClientIdPlaceholder: { defaultMessage: 'Enter AAD application client ID', id: 'u+H5Wd' },
    federatedTenantId: { defaultMessage: 'Federated tenant ID', id: 'syr24x' },
    federatedTenantIdPlaceholder: { defaultMessage: 'Enter AAD application tenant ID', id: 'q9k0lN' },
    outlookAccount: { defaultMessage: 'Outlook account', id: 'R3eMOe' },
    description: { defaultMessage: 'Description', id: 'Q8Qw5B' },
    sendEmail: { defaultMessage: 'Send email', id: 'sZIoMy' },
    office365Outlook: { defaultMessage: 'Office 365 Outlook', id: 'pPwNx4' },
    sendEmailDescription: { defaultMessage: 'The agent sends email messages.', id: 'Ebd6gR' },
    sendNotification: { defaultMessage: 'Send notification', id: 'nL/Owh' },
    microsoftTeams: { defaultMessage: 'Microsoft Teams', id: 'IIfj4G' },
    signInToService: { defaultMessage: 'Sign in to {service}', id: 'yBnHns' },
    outlook: { defaultMessage: 'Outlook', id: 'moOg2N' },
    serviceAccount: { defaultMessage: '{service} account', id: 'Y5IFiT' },
    connectedAs: { defaultMessage: 'Connected as', id: '0VVX+G' },
    signInWithDifferentAccount: { defaultMessage: 'Sign in with a different account', id: 'oHGFwy' },
    establishingConnection: { defaultMessage: 'Establishing connection ...', id: 'uIXQiw' },
    sendNotificationDescription: {
        defaultMessage: 'The agent posts notifications to the activity feed linking to a chat or team.',
        id: 'rzC0Xo',
    },
    setupTitle: { defaultMessage: 'Set up {service} connector', id: 'PBKSeP' },
    editConnector: { defaultMessage: 'Edit connector', id: '7kPmO3' },
    deleteConnector: { defaultMessage: 'Delete connector', id: '8urALB' },
    deletingConnector: { defaultMessage: 'Deleting connector', id: 'thOyk5' },
    deletingMultipleConnectors: { defaultMessage: 'Deleting {count} connectors...', id: 'XfAFyQ' },
    deletingConnectorDescription: { defaultMessage: 'Deleting connector "{name}"', id: 'QmOU5t' },
    connectorDeleted: { defaultMessage: 'Connector "{name}" has been deleted successfully', id: 'c8Doeq' },
    deleteConnectorFailed: { defaultMessage: 'Failed to delete connector', id: 'ocTBQX' },
    deleteConnectorWithMessageFailed: { defaultMessage: 'Failed to delete connector with error: {error}', id: '5Imse3' },
    creatingConnector: { defaultMessage: 'Creating connector', id: 'MK8Q9z' },
    creatingConnectorDescription: { defaultMessage: 'Creating connector "{name}"', id: 'rxcQLa' },
    connectorCreated: { defaultMessage: 'Connector "{name}" has been created successfully', id: 'm8grMF' },
    createConnectorFailed: { defaultMessage: 'Failed to create connector', id: '/mHau5' },
    createConnectorWithMessageFailed: { defaultMessage: 'Failed to create connector with error: {error}', id: 'S2z35W' },
    updatingConnector: { defaultMessage: 'Updating connector', id: 'QlkL14' },
    updatingConnectorDescription: { defaultMessage: 'Updating connector "{name}"', id: 'p/QlSI' },
    connectorUpdated: { defaultMessage: 'Connector "{name}" has been updated successfully', id: '1aNP56' },
    updateConnectorFailed: { defaultMessage: 'Failed to update connector', id: '2Bb4rY' },
    updateConnectorWithMessageFailed: { defaultMessage: 'Failed to update connector with error: {error}', id: '1OorZL' },
    successfullyDeletedMultiple: { defaultMessage: 'Successfully deleted {count} data connectors', id: 'GCVZ4/' },
    failedToDeleteAll: { defaultMessage: 'Failed to delete all {count} data connectors', id: 'oBxxMY' },
    partialDeleteSuccess: {
        defaultMessage: 'Deleted {successCount} data connectors. Failed to delete {failedCount}: {failedItems}',
        id: 'LQ2AtR',
    },
    connectionType: { defaultMessage: 'Connection type', id: 'ySdJIx' },
    remoteSse: { defaultMessage: 'SSE', id: 'DtmRJJ' },
    localProcess: { defaultMessage: 'Stdio', id: 'mE7Cg1' },
    command: { defaultMessage: 'Command', id: '9WyylR' },
    commandPlaceholder: { defaultMessage: 'Enter command (e.g. npx, python)', id: 'WJ+tyY' },
    arguments: { defaultMessage: 'Arguments', id: 'nc7Brw' },
    argumentPlaceholder: { defaultMessage: 'Enter argument', id: '3DC7Da' },
    environmentVariables: { defaultMessage: 'Environment variables', id: 'jvB4W9' },
    connectViaUrlEndpoint: { defaultMessage: 'Connect via URL endpoint', id: 'GDCPLv' },
    runLocalExecutable: { defaultMessage: 'Run local command or executable', id: 'ESqLtC' },
    commandHelperText: {
        defaultMessage: 'The executable command to start the MCP server. Must be in your system PATH or an absolute path.',
        id: 'cgh0+/',
    },
    toolCount: { defaultMessage: 'Tools: {count}', id: 'tpD3Ax' },
    lastHeartbeat: { defaultMessage: 'Last heartbeat: {time}', id: 'qdxZys' },
    error: { defaultMessage: 'Error', id: 'KN7zKn' },
    requestTimeout: { defaultMessage: 'Request timeout', id: 'inXqB2' },
    failedToFetchStatus: { defaultMessage: 'Failed to fetch status', id: 'VeyLxc' },
});

export const AgentPermissionsResources = defineMessages({
    permissions: { defaultMessage: 'Permissions', id: 'SFuk1v' },
    permissionsDescription: {
        defaultMessage:
            'Manage access to users or apps from other Microsoft Entra tenants. For same-tenant access, use Access control (IAM).',
        id: '+EEXu3',
    },
    add: { defaultMessage: 'Add', id: '2/2yg+' },
    refresh: { defaultMessage: 'Refresh', id: 'rELDbB' },
    delete: { defaultMessage: 'Delete', id: 'K3r6DQ' },
    displayName: { defaultMessage: 'Display name', id: 'dOQCL8' },
    role: { defaultMessage: 'Role', id: '1ZgrhW' },
    roleStandardUser: { defaultMessage: 'Standard User', id: 'g+WSRl' },
    roleReader: { defaultMessage: 'Reader', id: '3nhWFW' },
    roleAuthor: { defaultMessage: 'Author', id: 'tWkQ2J' },
    objectId: { defaultMessage: 'Object ID', id: '7egVxu' },
    tenantId: { defaultMessage: 'Tenant ID', id: 'VdfqU5' },
    displayNamePlaceholder: { defaultMessage: 'Enter display name', id: 'DXqEro' },
    rolePlaceholder: { defaultMessage: 'Select role', id: 'iaCoRs' },
    objectIdPlaceholder: { defaultMessage: 'Enter object ID', id: 'yL92TR' },
    tenantIdPlaceholder: { defaultMessage: 'Enter tenant ID', id: 'xB9aeF' },
    noPermissions: { defaultMessage: 'No permissions', id: '5WkCqY' },
    noPermissionsDescription: { defaultMessage: 'Add permissions to control access to this agent.', id: 'nSmJ6e' },
    emptyStateTitle: { defaultMessage: 'Manage cross-tenant access for this agent', id: '/YR0Nj' },
    emptyStateDescription: {
        defaultMessage:
            'Grant access to users or apps from other Microsoft Entra tenants. For same-tenant access, use Access control (IAM).',
        id: 'eBDp9J',
    },
    addPermission: { defaultMessage: 'Add permission', id: 'w4MKWD' },
    save: { defaultMessage: 'Save', id: 'jvo0vs' },
    cancel: { defaultMessage: 'Cancel', id: '47FYwb' },
    addingPermission: { defaultMessage: 'Adding permission', id: 'siuURH' },
    addingPermissionDescription: { defaultMessage: 'Adding permission for {name}', id: '6TEeYv' },
    permissionAddedSuccess: { defaultMessage: 'Permission added successfully', id: 'U86NEH' },
    permissionAddFailed: { defaultMessage: 'Failed to add permission', id: 'iKfoIP' },
    deletingPermission: { defaultMessage: 'Deleting permission', id: '33BnoA' },
    deletingPermissionDescription: { defaultMessage: 'Deleting {count} permissions', id: 'a4owu3' },
    permissionDeletedSuccess: { defaultMessage: 'Permission deleted successfully', id: 'dM9M1D' },
    permissionDeleteFailed: { defaultMessage: 'Failed to delete permission', id: 'pLFm2y' },
    selectAllRows: { defaultMessage: 'Select all rows', id: '8BaLs0' },
    selectRow: { defaultMessage: 'Select row', id: '4pJVaS' },
});
