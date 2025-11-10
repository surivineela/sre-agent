import { Card, CardHeader, Label, Text } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { IdentityKeys } from '../../Contracts/Identity';
import { useConnectorWizardStyles } from './ConnectorWizard.styles';
import { AuthType, ConnectorFormProps } from './ConnectorWizardFormik';
import { ConnectorType, getConnectorIcon, getConnectorName, getConnectorService } from './Wizard/Common/ConnectorType';
import { getBearerTokenConnectionString, getCustomHeadersConnectionString } from './Wizard/Common/CustomConnector';

export interface ReviewAndAddProps {
    userAssignedIdentities: { id: string; name: string }[];
}

export const ReviewAndAdd: React.FC<ReviewAndAddProps> = ({ userAssignedIdentities }) => {
    const intl = useIntl();
    const styles = useConnectorWizardStyles();
    const { values } = useFormikContext<ConnectorFormProps>();

    const selectedConnector = useMemo(() => values.connectorType as ConnectorType, [values.connectorType]);

    const contentList = useMemo(() => {
        const labelValuePairs: { label: string; value: string }[] = [];
        if (selectedConnector === ConnectorType.McpServer) {
            if (values.authType === AuthType.BearerToken) {
                labelValuePairs.push({
                    label: intl.formatMessage(ConnectorsResources.authenticationMethod),
                    value: intl.formatMessage(ConnectorsResources.bearerToken),
                });
                labelValuePairs.push({
                    label: intl.formatMessage(ConnectorsResources.compiledConnectionString),
                    value: getBearerTokenConnectionString(values.url, values.patOrApiKey || ''),
                });
            } else {
                labelValuePairs.push({
                    label: intl.formatMessage(ConnectorsResources.authenticationMethod),
                    value: intl.formatMessage(ConnectorsResources.customHeaders),
                });
                labelValuePairs.push({
                    label: intl.formatMessage(ConnectorsResources.compiledConnectionString),
                    value: getCustomHeadersConnectionString(values.url, values.customHeaders || []),
                });
            }
        } else {
            if (values.email) {
                labelValuePairs.push({ label: intl.formatMessage(ConnectorsResources.outlookAccount), value: values.email });
            } else if (values.url) {
                labelValuePairs.push({ label: intl.formatMessage(ConnectorsResources.repositoryUrl), value: values.url });
            }

            if (values.identity) {
                labelValuePairs.push({
                    label: intl.formatMessage(ConnectorsResources.managedIdentity),
                    value:
                        values.identity === IdentityKeys.system
                            ? intl.formatMessage(SreAgentResources.systemAssigned)
                            : userAssignedIdentities.find(option => option.id === values.identity)?.name || '',
                });
            }
        }

        return labelValuePairs;
    }, [values, userAssignedIdentities, intl, selectedConnector]);

    return (
        <div className={styles.reviewAndAddContainer}>
            <Label className={styles.title}>{intl.formatMessage(ConnectorsResources.reviewAndCreate)}</Label>
            <VerticalLabelWithContent label={intl.formatMessage(ConnectorsResources.connectorCapital)}>
                {selectedConnector && (
                    <Card>
                        <CardHeader
                            image={<img src={getConnectorIcon(selectedConnector, intl)} alt={getConnectorName(selectedConnector, intl)} />}
                            header={
                                <div className={styles.reviewAndAddCardContent}>
                                    <Text weight="semibold">{getConnectorName(selectedConnector, intl)}</Text>
                                    <Text className={styles.reviewAndAddSectionValue}>{getConnectorService(selectedConnector, intl)}</Text>
                                </div>
                            }
                        />
                    </Card>
                )}
            </VerticalLabelWithContent>
            <VerticalLabelWithContent label={intl.formatMessage(SreAgentResources.name)}>
                <Text className={styles.reviewAndAddSectionValue}>{values.name || '-'}</Text>
            </VerticalLabelWithContent>
            {contentList.map(({ label, value }) => (
                <VerticalLabelWithContent label={label}>
                    <Text className={styles.reviewAndAddSectionValue}>{value}</Text>
                </VerticalLabelWithContent>
            ))}
        </div>
    );
};

export const VerticalLabelWithContent: React.FC<{ label: string; children: React.ReactNode }> = ({ label, children }) => {
    const styles = useConnectorWizardStyles();

    return (
        <div className={styles.reviewAndAddSection}>
            <Label className={styles.reviewAndAddSectionTitle}>{label}</Label>
            {children}
        </div>
    );
};
