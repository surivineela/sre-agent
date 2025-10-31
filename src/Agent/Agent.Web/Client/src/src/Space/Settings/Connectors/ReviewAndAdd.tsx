import { Card, CardHeader, Text } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { IdentityKeys } from '../../Contracts/Identity';
import { ConnectorType, connectorTypeOptions } from './ConnectorType';
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
            <Text className={styles.title}>{intl.formatMessage(ConnectorsResources.reviewAndCreate)}</Text>
            <div className={styles.reviewAndAddSection}>
                <Text className={styles.reviewAndAddSectionTitle}>{intl.formatMessage(ConnectorsResources.connectorCapital)}</Text>
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
            </div>
            <div className={styles.reviewAndAddSection}>
                <Text className={styles.reviewAndAddSectionTitle}>{intl.formatMessage(SreAgentResources.name)}</Text>
                <Text className={styles.reviewAndAddSectionValue}>{values.name || '-'}</Text>
            </div>
            <div className={styles.reviewAndAddSection}>
                <Text className={styles.reviewAndAddSectionTitle}>
                    {values.connectorType === ConnectorType.OutlookSendEmail
                        ? intl.formatMessage(ConnectorsResources.outlookAccount)
                        : intl.formatMessage(ConnectorsResources.repositoryUrl)}
                </Text>
                <Text className={styles.reviewAndAddSectionValue}>{values.url}</Text>
            </div>
            <div className={styles.reviewAndAddSection}>
                <Text className={styles.reviewAndAddSectionTitle}>{intl.formatMessage(ConnectorsResources.managedIdentity)}</Text>
                <Text className={styles.reviewAndAddSectionValue}>
                    {values.identity === IdentityKeys.system
                        ? intl.formatMessage(SreAgentResources.systemAssigned)
                        : userAssignedIdentities.find(option => option.id === values.identity)?.name || ''}
                </Text>
            </div>
        </div>
    );
};
