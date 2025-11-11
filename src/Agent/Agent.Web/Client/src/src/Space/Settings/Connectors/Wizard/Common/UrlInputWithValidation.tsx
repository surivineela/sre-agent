import { useFormikContext } from 'formik';
import React, { useMemo } from 'react';
import { useIntl } from 'react-intl';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { ConnectorFormProps } from '../ConnectorWizardFormik';
import { ConnectorType, getConnectorService } from './ConnectorType';

export const kustoDataSourceExample = 'https://cluster-url/database-name';

export const UrlInputWithValidation: React.FC = () => {
    const intl = useIntl();

    const { values } = useFormikContext<ConnectorFormProps>();

    const connectorType = useMemo(() => values.connectorType as ConnectorType, [values.connectorType]);

    const urlLabel = useMemo(() => {
        if (connectorType === ConnectorType.McpServer) {
            return intl.formatMessage(ConnectorsResources.url);
        }

        return connectorType
            ? `${getConnectorService(connectorType, intl)} ${intl.formatMessage(ConnectorsResources.repositoryUrl)}`
            : intl.formatMessage(ConnectorsResources.repositoryUrl);
    }, [connectorType, intl]);

    const urlPlaceholder = useMemo(() => {
        if (connectorType === ConnectorType.AzureDataExplorerQuery) {
            return kustoDataSourceExample;
        }

        return intl.formatMessage(ConnectorsResources.urlPlaceholder);
    }, [connectorType, intl]);

    return <InputFormik name="url" label={urlLabel} required orientation="vertical" placeholder={urlPlaceholder} />;
};
