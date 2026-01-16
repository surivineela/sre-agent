import { useFormikContext } from 'formik';
import React, { useMemo } from 'react';
import { useIntl } from 'react-intl';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { ConnectorFormProps } from '../ConnectorWizardFormik';
import { ConnectorType, getConnectorService } from './ConnectorType';

export const kustoDataSourceExample = 'https://cluster-url/database-name';
export const azureDevOpsUrlExample = 'Enter repository or wiki URL';

export const UrlInput: React.FC = () => {
    const intl = useIntl();

    const { values } = useFormikContext<ConnectorFormProps>();

    const connectorType = useMemo(() => values.connectorType as ConnectorType, [values.connectorType]);

    const urlLabel = useMemo(() => {
        switch (connectorType) {
            case ConnectorType.McpServer:
            case ConnectorType.GitHub:
                return intl.formatMessage(ConnectorsResources.url);
            case ConnectorType.AzureDevOpsDocumentation:
                return intl.formatMessage(ConnectorsResources.azureDevOpsUrl);
            case ConnectorType.AzureDataExplorerQuery:
            case ConnectorType.AzureDataExplorerIndexing:
                return intl.formatMessage(ConnectorsResources.dataExplorerUrl);
            default:
                return connectorType
                    ? intl.formatMessage(ConnectorsResources.serviceRepositoryUrl, { 0: getConnectorService(connectorType, intl) })
                    : intl.formatMessage(ConnectorsResources.repositoryUrl);
        }
    }, [connectorType, intl]);

    const urlPlaceholder = useMemo(() => {
        switch (connectorType) {
            case ConnectorType.AzureDataExplorerQuery:
                return kustoDataSourceExample;
            case ConnectorType.AzureDevOpsDocumentation:
                return azureDevOpsUrlExample;
            default:
                return intl.formatMessage(ConnectorsResources.urlPlaceholder);
        }
    }, [connectorType, intl]);

    return <InputFormik name="url" label={urlLabel} required orientation="vertical" placeholder={urlPlaceholder} />;
};
