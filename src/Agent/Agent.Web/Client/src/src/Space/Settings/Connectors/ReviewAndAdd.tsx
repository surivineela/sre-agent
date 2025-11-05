import { Card, CardHeader, Label, Text } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { IdentityKeys } from '../../Contracts/Identity';
import { connectorTypeOptions } from './ConnectorType';
import { useConnectorWizardStyles } from './ConnectorWizard.styles';
import { ConnectorFormProps } from './ConnectorWizardFormik';

export interface ReviewAndAddProps {
    userAssignedIdentities: { id: string; name: string }[];
}

export const ReviewAndAdd: React.FC<ReviewAndAddProps> = ({ userAssignedIdentities }) => {
    const intl = useIntl();
    const styles = useConnectorWizardStyles();
    const { values } = useFormikContext<ConnectorFormProps>();

    const selectedConnector = useMemo(() => {
        return connectorTypeOptions(intl).find(opt => opt.id === values.connectorType);
    }, [intl, values.connectorType]);

    return (
        <div className={styles.reviewAndAddContainer}>
            <Label className={styles.title}>{intl.formatMessage(ConnectorsResources.reviewAndCreate)}</Label>
            <VerticalLabelWithContent label={intl.formatMessage(ConnectorsResources.connectorCapital)}>
                {selectedConnector && (
                    <Card>
                        <CardHeader
                            image={<img src={selectedConnector.img} alt={selectedConnector.name} />}
                            header={
                                <div className={styles.reviewAndAddCardContent}>
                                    <Text weight="semibold">{selectedConnector.name}</Text>
                                    <Text className={styles.reviewAndAddSectionValue}>{selectedConnector.service}</Text>
                                </div>
                            }
                        />
                    </Card>
                )}
            </VerticalLabelWithContent>
            <VerticalLabelWithContent label={intl.formatMessage(SreAgentResources.name)}>
                <Text className={styles.reviewAndAddSectionValue}>{values.name || '-'}</Text>
            </VerticalLabelWithContent>
            {values.email ? (
                <VerticalLabelWithContent label={intl.formatMessage(ConnectorsResources.outlookAccount)}>
                    <Text className={styles.reviewAndAddSectionValue}>{values.email}</Text>
                </VerticalLabelWithContent>
            ) : values.url ? (
                <VerticalLabelWithContent label={intl.formatMessage(ConnectorsResources.repositoryUrl)}>
                    <Text className={styles.reviewAndAddSectionValue}>{values.url}</Text>
                </VerticalLabelWithContent>
            ) : undefined}
            <VerticalLabelWithContent label={intl.formatMessage(ConnectorsResources.managedIdentity)}>
                <Text className={styles.reviewAndAddSectionValue}>
                    {values.identity === IdentityKeys.system
                        ? intl.formatMessage(SreAgentResources.systemAssigned)
                        : userAssignedIdentities.find(option => option.id === values.identity)?.name || ''}
                </Text>
            </VerticalLabelWithContent>
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
