import { FormikHelpers } from 'formik';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../../../Common/Contracts/Azure/SreAgent';
import { AuthType, ConnectorFormProps, McpConnectionType } from '../ConnectorWizardFormik';
import { AzureConnectorForm } from '../SetupForm/AzureConnectorForm';
import { McpServerForm } from '../SetupForm/McpServerForm';
import { OutlookTeamsConnectorForm } from '../SetupForm/OutlookTeamsConnectorForm';
import { ConnectorType } from './ConnectorType';
import { getBearerTokenConnectionString, getCustomHeadersConnectionString } from './CustomConnectorHelper';
import { createMcpLocalDataSource } from './McpDataSourceHelper';
import { parseTeamsChannelLink } from './TeamsConnectorHelper';

/**
 * Extracts user assigned identity options from MsiIdentity
 */
export const getUserAssignedIdentityOptions = (agentIdentity?: MsiIdentity): { id: string; name: string }[] => {
    const userAssignedOptions: { id: string; name: string }[] = [];

    const userAssignedIdentityRscIds = agentIdentity?.userAssignedIdentities ? Object.keys(agentIdentity.userAssignedIdentities) : [];
    if (userAssignedIdentityRscIds.length > 0) {
        userAssignedIdentityRscIds.forEach(resourceId => {
            const parts = resourceId.split('/');
            const name = parts[parts.length - 1] || resourceId;
            userAssignedOptions.push({
                id: resourceId,
                name: name,
            });
        });
    }

    return userAssignedOptions;
};

export interface RenderFormOptions {
    connectorType: string;
    userAssignedIdentityOptions: { id: string; name: string }[];
    agentIdentity?: MsiIdentity;
    agentName?: string;
    agentLocation?: string;
    refreshAgent: () => void;
    isEditMode?: boolean;
}

export const renderConnectorForm = (options: RenderFormOptions): React.ReactNode => {
    const {
        connectorType,
        userAssignedIdentityOptions,
        agentIdentity,
        agentName,
        agentLocation,
        refreshAgent,
        isEditMode = false,
    } = options;

    switch (connectorType) {
        case ConnectorType.AzureDataExplorerQuery:
        case ConnectorType.AzureDataExplorerIndexing:
        case ConnectorType.AzureDevOpsDocumentation:
            return (
                <AzureConnectorForm
                    isEditMode={isEditMode}
                    userAssignedIdentities={userAssignedIdentityOptions}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                />
            );
        case ConnectorType.OutlookSendEmail:
        case ConnectorType.TeamsSendNotification:
            return (
                <OutlookTeamsConnectorForm
                    isEditMode={isEditMode}
                    userAssignedIdentities={userAssignedIdentityOptions}
                    agentName={agentName}
                    agentLocation={agentLocation}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                />
            );
        case ConnectorType.McpServer:
        case ConnectorType.GitHub:
            return (
                <McpServerForm
                    isEditMode={isEditMode}
                    userAssignedIdentities={userAssignedIdentityOptions}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                />
            );
        default:
            return null;
    }
};

export interface CreateConnectorSubmitOptions {
    values: ConnectorFormProps;
    formikHelpers: FormikHelpers<ConnectorFormProps>;
    onSubmit: (connector: Connector) => void;
    onClose: () => void;
    resetForm?: () => void;
    resetStep?: () => void;
}

export const handleConnectorSubmit = async (options: CreateConnectorSubmitOptions) => {
    const { values, formikHelpers, onSubmit, onClose, resetStep } = options;

    let connectorType = values.connectorType as ConnectorType;
    // Note: GitHub is not supported as a first-party mcp server by the backend
    // the underlying implementation is essentially just an MCP server
    if (connectorType === ConnectorType.GitHub) {
        connectorType = ConnectorType.McpServer;
    }

    let dataSource: string;
    if (connectorType !== ConnectorType.McpServer) {
        if (connectorType === ConnectorType.TeamsSendNotification) {
            const teamsInfo = parseTeamsChannelLink(values.teamsChannelLink || '');
            dataSource = `${values.url};${teamsInfo?.teamsGroupId};${teamsInfo?.channelId}`;
        } else {
            dataSource = values.url;
        }
    } else {
        if (values.mcpConnectionType === McpConnectionType.Local) {
            const args = values.args?.map(a => a.value).filter(v => v.trim() !== '') || [];
            const env =
                values.env?.reduce(
                    (acc, curr) => {
                        if (curr.key.trim() !== '') {
                            acc[curr.key] = curr.value;
                        }
                        return acc;
                    },
                    {} as Record<string, string>
                ) || {};

            dataSource = createMcpLocalDataSource(values.command || '', args, env);
        } else {
            if (values.authType === AuthType.BearerToken) {
                dataSource = getBearerTokenConnectionString(values.url, values.patOrApiKey || '');
            } else {
                dataSource = getCustomHeadersConnectionString(values.url, values.customHeaders || []);
            }
        }
    }

    const dataConnector: Connector = {
        name: values.name,
        dataConnectorType: values.connectorType?.toString() || '',
        dataSource: dataSource,
        identity: values.identity,
    };

    onClose();
    formikHelpers.resetForm();
    if (resetStep) resetStep();
    onSubmit(dataConnector);
};
