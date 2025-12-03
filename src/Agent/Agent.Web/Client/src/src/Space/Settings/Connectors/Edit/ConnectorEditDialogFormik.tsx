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
        };

        if (connectorType === ConnectorType.TeamsSendNotification) {
            const parts = connector.dataSource?.split(';');
            if (parts && parts.length >= 3) {
                initialFormProps.url = parts[0];
                initialFormProps.teamsGroupId = parts[1];
                initialFormProps.channelId = parts[2];
            }
        } else if (connectorType === ConnectorType.McpServer || connectorType === ConnectorType.GitHub) {
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
