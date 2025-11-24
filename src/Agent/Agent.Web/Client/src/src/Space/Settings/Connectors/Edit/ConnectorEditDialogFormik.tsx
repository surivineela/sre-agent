import { Formik, FormikHelpers } from 'formik';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { MsiIdentity } from '../../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../../Common/Contracts/Azure/SreAgent';
import { ConnectorType } from '../Wizard/Common/ConnectorType';
import { handleConnectorSubmit } from '../Wizard/Common/DialogHelper';
import { getValidationSchema } from '../Wizard/Common/ValidationHelper';
import { AuthType, ConnectorFormProps } from '../Wizard/ConnectorWizardFormik';
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
            const parts = connector.dataSource?.split(';');
            if (parts && parts.length >= 2) {
                const endpointPart = parts.find(part => part.includes('Endpoint='));
                const endpoint = endpointPart?.split('=')[1];
                initialFormProps.url = endpoint || '';

                const authTypePart = parts.find(part => part.includes('AuthType='));
                const authType = authTypePart?.split('=')[1] as AuthType;
                initialFormProps.authType = authType;

                if (authType === AuthType.BearerToken) {
                    const bearerTokenPart = parts.find(part => part.includes('BearerToken='));
                    const bearerToken = bearerTokenPart?.split('=')[1];

                    initialFormProps.patOrApiKey = bearerToken || '';
                } else if (authType === AuthType.CustomHeaders) {
                    const customHeaders = parts
                        .filter(part => !part.includes('Endpoint=') && !part.includes('AuthType='))
                        .map(part => {
                            const partKeyValuePair = part.split('=');
                            return { key: partKeyValuePair[0], value: partKeyValuePair[1] };
                        });

                    initialFormProps.customHeaders = customHeaders;
                }
            }
        }
        return initialFormProps;
    }, [connector]);

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
