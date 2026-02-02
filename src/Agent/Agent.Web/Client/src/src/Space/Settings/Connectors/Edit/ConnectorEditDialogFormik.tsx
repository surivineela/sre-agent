import { Formik, FormikHelpers } from 'formik';
import { useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { MsiIdentity } from '../../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../../Common/Contracts/Azure/SreAgent';
import { ConnectorType } from '../Wizard/Common/ConnectorType';
import { handleConnectorSubmit } from '../Wizard/Common/DialogHelper';
import { parseMcpLocalDataSource, parseMcpRemoteDataSource } from '../Wizard/Common/McpDataSourceHelper';
import { getValidationSchema } from '../Wizard/Common/ValidationHelper';
import { AuthType, ConnectorFormProps, McpConnectionType } from '../Wizard/ConnectorWizardFormik';
import { ConnectorEditDialog } from './ConnectorEditDialog';

interface ConnectorEditDialogFormikProps {
    isOpen: boolean;
    onOpenChange: (isOpen: boolean) => void;
    connector: Connector;
    onSubmit: (connector: Connector) => void;
    agentName?: string;
    agentLocation?: string;
    agentIdentity?: MsiIdentity;
    refreshAgent: () => void;
}

export const ConnectorEditDialogFormik: React.FC<ConnectorEditDialogFormikProps> = props => {
    const { connector, onOpenChange, onSubmit } = props;

    const intl = useIntl();
    const azPortalProxy = useContext(AzPortalContext);

    const initialFormValues = useMemo((): ConnectorFormProps => {
        const connectorType = connector.dataConnectorType as ConnectorType;
        const initialFormProps: ConnectorFormProps = {
            connectorType: connector.dataConnectorType,
            name: connector.name,
            url: connector.dataSource || '',
            identity: connector.identity,
            authType: undefined,
            patOrApiKey: '',
            email: '',
            teamsChannelLink: '',
            channelId: '',
            teamsGroupId: '',
            customHeaders: [{ key: '', value: '' }],
            useManagedIdentityAsFic: false,
            federatedClientId: '',
            federatedTenantId: '',
            azureDevOpsOrganization: '',
        };

        // Parse FIC properties from extendedProperties
        if (connector.extendedProperties) {
            const props = connector.extendedProperties;
            if (props.useManagedIdentityAsFic) {
                initialFormProps.useManagedIdentityAsFic = Boolean(props.useManagedIdentityAsFic);
                initialFormProps.federatedClientId = (props.federatedClientId as string) || '';
                initialFormProps.federatedTenantId = (props.federatedTenantId as string) || '';
            }
            // Parse Azure DevOps organization
            if (connectorType === ConnectorType.AzureDevOpsOAuth && props.organization) {
                initialFormProps.azureDevOpsOrganization = (props.organization as string) || '';
            }
        }

        if (connectorType === ConnectorType.TeamsSendNotification) {
            const parts = connector.dataSource?.split(';');
            if (parts && parts.length >= 3) {
                initialFormProps.url = parts[0];
                initialFormProps.teamsGroupId = parts[1];
                initialFormProps.channelId = parts[2];
            }
        } else if (connectorType === ConnectorType.McpServer || connectorType === ConnectorType.GitHub) {
            // Check if ExtendedProperties exists (new format)
            if (connector.extendedProperties) {
                const props = connector.extendedProperties;
                const type = (props.type as string)?.toLowerCase();

                if (type === 'stdio') {
                    // Local MCP (stdio) from ExtendedProperties
                    initialFormProps.mcpConnectionType = McpConnectionType.Local;
                    initialFormProps.command = (props.command as string) || '';

                    const args = props.args as string[] | undefined;
                    initialFormProps.args = args && args.length > 0 ? args.map(arg => ({ value: arg })) : [{ value: '' }];

                    const env = props.envs as Record<string, string> | undefined;
                    initialFormProps.env =
                        env && Object.keys(env).length > 0
                            ? Object.entries(env).map(([key, value]) => ({ key, value }))
                            : [{ key: '', value: '' }];
                } else if (type === 'http') {
                    // Remote MCP (HTTP) from ExtendedProperties
                    initialFormProps.mcpConnectionType = McpConnectionType.Remote;
                    initialFormProps.url = (props.endpoint as string) || '';

                    const authType = props.authType as string;
                    initialFormProps.authType = authType as AuthType;

                    if (authType === 'BearerToken') {
                        initialFormProps.patOrApiKey = (props.bearerToken as string) || '';
                    } else if (authType === 'CustomHeaders') {
                        // Extract custom headers (all properties except type, endpoint, authType)
                        const customHeaders: Array<{ key: string; value: string }> = [];
                        Object.entries(props).forEach(([key, value]) => {
                            if (key !== 'type' && key !== 'endpoint' && key !== 'authType') {
                                customHeaders.push({ key, value: String(value) });
                            }
                        });
                        initialFormProps.customHeaders = customHeaders.length > 0 ? customHeaders : [{ key: '', value: '' }];
                    }
                }
            } else {
                // Legacy format: parse from dataSource string
                // Try parsing as Local MCP (stdio) first
                const parsedLocal = parseMcpLocalDataSource(connector.dataSource || '', azPortalProxy.log);

                if (parsedLocal.type === 'stdio') {
                    initialFormProps.mcpConnectionType = McpConnectionType.Local;
                    initialFormProps.command = parsedLocal.command || '';
                    initialFormProps.args =
                        parsedLocal.args && parsedLocal.args.length > 0 ? parsedLocal.args.map(arg => ({ value: arg })) : [{ value: '' }];
                    initialFormProps.env =
                        parsedLocal.env && Object.keys(parsedLocal.env).length > 0
                            ? Object.entries(parsedLocal.env).map(([key, value]) => ({ key, value }))
                            : [{ key: '', value: '' }];
                } else {
                    // Remote MCP
                    initialFormProps.mcpConnectionType = McpConnectionType.Remote;

                    const parsed = parseMcpRemoteDataSource(connector.dataSource || '');

                    initialFormProps.url = parsed.endpoint || '';
                    initialFormProps.authType = parsed.authType as AuthType;

                    if (parsed.authType === AuthType.BearerToken) {
                        initialFormProps.patOrApiKey = parsed.bearerToken || '';
                    } else if (parsed.authType === AuthType.CustomHeaders && parsed.customHeaders) {
                        initialFormProps.customHeaders = parsed.customHeaders.length > 0 ? parsed.customHeaders : [{ key: '', value: '' }];
                    }
                }
            }
        }
        return initialFormProps;
    }, [connector, azPortalProxy.log]);

    const handleSubmit = useCallback(
        async (values: ConnectorFormProps, formikHelpers: FormikHelpers<ConnectorFormProps>) => {
            await handleConnectorSubmit({
                values,
                formikHelpers,
                onSubmit,
                onClose: () => onOpenChange(false),
            });
        },
        [onSubmit, onOpenChange]
    );

    const validationSchema = useMemo(() => getValidationSchema([], intl, true), [intl]);

    return (
        <Formik
            initialValues={initialFormValues}
            validationSchema={validationSchema}
            onSubmit={handleSubmit}
            enableReinitialize={true}
            validateOnChange={true}
        >
            <ConnectorEditDialog {...props} />
        </Formik>
    );
};
