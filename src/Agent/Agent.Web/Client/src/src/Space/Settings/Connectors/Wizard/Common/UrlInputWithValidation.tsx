import { useFormikContext } from 'formik';
import React, { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { ConnectorsResources, SreAgentResources } from '../../../../../Strings/SREAgentResources';
import { ConnectorFormProps } from '../../ConnectorWizardFormik';
import { ConnectorType, getConnectorService } from './ConnectorType';

const kustoDataSourceExample = 'https://cluster-url/database-name';

export const UrlInputWithValidation: React.FC = () => {
    const intl = useIntl();

    const { values } = useFormikContext<ConnectorFormProps>();

    const connectorType = useMemo(() => values.connectorType as ConnectorType, [values.connectorType]);

    const validateUrl = useCallback(
        (url: string) => {
            if (!url) {
                return intl.formatMessage(SreAgentResources.fieldRequired);
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

    return (
        <InputFormik
            name="url"
            label={urlLabel}
            required
            orientation="vertical"
            placeholder={urlPlaceholder}
            validate={url => validateUrl(url)}
        />
    );
};
