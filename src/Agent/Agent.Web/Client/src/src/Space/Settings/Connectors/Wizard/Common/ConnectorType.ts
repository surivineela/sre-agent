import { resolveResourceIcon } from '../../../../../Common/Helpers/Resources';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';

export enum ConnectorType {
    AzureDataExplorerQuery = 'Kusto',
    AzureDataExplorerIndexing = 'KustoDataIndexer',
    AzureDevOpsDocumentation = 'TsgCrawler',
    OutlookSendEmail = 'Outlook',
    TeamsSendNotification = 'Teams',
    GitHub = 'GitHub',
    McpServer = 'Mcp',
}

export interface ConnectorTypeOption {
    id: string;
    name: string;
    service: string;
    description: string;
    img: string;
}

export const connectorTypeOptions = (intl: any): ConnectorTypeOption[] => [
    {
        id: ConnectorType.AzureDataExplorerQuery,
        name: intl.formatMessage(ConnectorsResources.databaseQueryConnector),
        service: intl.formatMessage(ConnectorsResources.azureDataExplorer),
        description: intl.formatMessage(ConnectorsResources.predefinedQueriesDescription),
        img: resolveResourceIcon('AzureDataExplorer'),
    },
    {
        id: ConnectorType.AzureDataExplorerIndexing,
        name: intl.formatMessage(ConnectorsResources.databaseIndexingConnector),
        service: intl.formatMessage(ConnectorsResources.azureDataExplorer),
        description: intl.formatMessage(ConnectorsResources.queryGenerationDescription),
        img: resolveResourceIcon('AzureDataExplorer'),
    },
    {
        id: ConnectorType.AzureDevOpsDocumentation,
        name: intl.formatMessage(ConnectorsResources.documentationConnector),
        service: intl.formatMessage(ConnectorsResources.azureDevops),
        description: intl.formatMessage(ConnectorsResources.documentationDescription),
        img: resolveResourceIcon('AzureDevOps'),
    },
    {
        id: ConnectorType.GitHub,
        name: intl.formatMessage(ConnectorsResources.gitHubMcpServer),
        service: intl.formatMessage(ConnectorsResources.gitHub),
        description: intl.formatMessage(ConnectorsResources.githubDescription),
        img: resolveResourceIcon('GitHub'),
    },
    {
        id: ConnectorType.OutlookSendEmail,
        name: intl.formatMessage(ConnectorsResources.sendEmail),
        service: intl.formatMessage(ConnectorsResources.office365Outlook),
        description: intl.formatMessage(ConnectorsResources.sendEmailDescription),
        img: resolveResourceIcon('Outlook'),
    },
    {
        id: ConnectorType.TeamsSendNotification,
        name: intl.formatMessage(ConnectorsResources.sendNotification),
        service: intl.formatMessage(ConnectorsResources.microsoftTeams),
        description: intl.formatMessage(ConnectorsResources.sendNotificationDescription),
        img: resolveResourceIcon('Teams'),
    },
];

export const getConnectorName = (connectorType: ConnectorType, intl: any): string => {
    if (connectorType === ConnectorType.McpServer) {
        return intl.formatMessage(ConnectorsResources.mcpServer);
    }

    const option = connectorTypeOptions(intl).find(opt => opt.id === connectorType);
    return option ? option.name : connectorType;
};

export const getConnectorService = (connectorType: ConnectorType, intl: any): string => {
    if (connectorType === ConnectorType.McpServer) {
        return intl.formatMessage(ConnectorsResources.userProvidedConnector);
    }

    const option = connectorTypeOptions(intl).find(opt => opt.id === connectorType);
    return option ? option.service : '';
};

export const getConnectorIcon = (connectorType: ConnectorType, intl: any): string => {
    if (connectorType === ConnectorType.McpServer) {
        return resolveResourceIcon('CustomConnector');
    }

    const option = connectorTypeOptions(intl).find(opt => opt.id === connectorType);
    return option ? option.img : '';
};
