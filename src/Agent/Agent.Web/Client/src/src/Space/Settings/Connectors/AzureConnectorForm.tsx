import { useFormikContext } from 'formik';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import InputFormik from '../../../Common/Components/Input/InputFormik';
import { ConnectorsResources } from '../../../Strings/SREAgentResources';
import { ConnectorType, getConnectorService } from './ConnectorType';
import { ConnectorWithManagedIdentityFormBase, ConnectorWithManagedIdentityFormBaseProps } from './ConnectorWithManagedIdentityFormBase';
import { ConnectorFormProps } from './ConnectorWizardFormik';

const kustoDataSourceExample = 'https://cluster-url/database-name';

export const AzureConnectorForm: React.FC<Omit<ConnectorWithManagedIdentityFormBaseProps, 'children'>> = props => {
    const { userAssignedIdentities, selectedConnector, agentIdentity, existingConnectors, refreshAgent } = props;

    const intl = useIntl();

    const { values } = useFormikContext<ConnectorFormProps>();

    const connectorType = useMemo(() => {
        return selectedConnector ? (selectedConnector.dataConnectorType as ConnectorType) : (values.connectorType as ConnectorType);
    }, [selectedConnector, values.connectorType]);

    const validateUrl = useCallback(
        (url: string, dataConnectorType: ConnectorType | undefined) => {
            if (!url || dataConnectorType !== ConnectorType.AzureDataExplorerQuery) {
                return undefined;
            }

            let isValidUri = false;
            try {
                const urlFormat = new URL(url);
                isValidUri =
                    urlFormat.protocol === 'https:' && !!urlFormat.host.trim() && !!urlFormat.pathname && urlFormat.pathname.trim() !== '/';
            } catch {
                isValidUri = false;
            }

            return !isValidUri
                ? intl.formatMessage(ConnectorsResources.urlKustoFormatError, { format: kustoDataSourceExample })
                : undefined;
        },
        [intl]
    );

    const getUrlPlaceholder = useCallback(
        (dataConnectorType: ConnectorType | undefined) => {
            if (dataConnectorType === ConnectorType.AzureDataExplorerQuery) {
                return kustoDataSourceExample;
            }

            return intl.formatMessage(ConnectorsResources.urlPlaceholder);
        },
        [intl]
    );

    return (
        <ConnectorWithManagedIdentityFormBase
            userAssignedIdentities={userAssignedIdentities}
            refreshAgent={refreshAgent}
            selectedConnector={selectedConnector}
            agentIdentity={agentIdentity}
            existingConnectors={existingConnectors}
        >
            <InputFormik
                name="url"
                label={
                    connectorType
                        ? `${getConnectorService(connectorType, intl)} ${intl.formatMessage(ConnectorsResources.repositoryUrl)}`
                        : intl.formatMessage(ConnectorsResources.repositoryUrl)
                }
                required
                orientation="vertical"
                placeholder={getUrlPlaceholder(connectorType)}
                validate={url => validateUrl(url, connectorType)}
            />
        </ConnectorWithManagedIdentityFormBase>
    );
};
