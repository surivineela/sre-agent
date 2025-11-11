import { Button, Card, CardHeader, Text } from '@fluentui/react-components';
import { Add20Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SearchBoxWithDebounce } from '../../../../Common/Components/SearchBox/SearchBoxWithDebounce';
import { Connector } from '../../../../Common/Contracts/Azure/SreAgent';
import { AntUxStringComparison, equals } from '../../../../Common/Helpers/Strings';
import { ConnectorsResources } from '../../../../Strings/SREAgentResources';
import { ConnectorType, ConnectorTypeOption, connectorTypeOptions } from './Common/ConnectorType';
import { useConnectorWizardStyles } from './ConnectorWizard.styles';
import { ConnectorFormProps } from './ConnectorWizardFormik';

interface ConnectorPickerProps {
    existingConnectors?: Connector[];
    goToNextStep: () => void;
}

export const ConnectorPicker: React.FC<ConnectorPickerProps> = props => {
    const { existingConnectors, goToNextStep } = props;
    const intl = useIntl();
    const styles = useConnectorWizardStyles();
    const { values, setFieldValue, setTouched, setErrors } = useFormikContext<ConnectorFormProps>();

    const [searchTerm, setSearchTerm] = useState('');

    const filteredConnectorOptions = useMemo(() => {
        return connectorTypeOptions(intl).filter((option: ConnectorTypeOption) => {
            const searchLower = searchTerm.toLowerCase().trim();
            const matchesSearch =
                !searchLower ||
                option.name.toLowerCase().includes(searchLower) ||
                option.service.toLowerCase().includes(searchLower) ||
                option.description.toLowerCase().includes(searchLower);

            return matchesSearch;
        });
    }, [intl, searchTerm]);

    const onConnectorSelected = useCallback(
        (connector: ConnectorTypeOption) => {
            setFieldValue('connectorType', connector.id);
            setFieldValue('name', '');
            setFieldValue('url', '');
            setFieldValue('identity', '');
            setFieldValue('email', '');
            setFieldValue('teamsChannelLink', '');
            setFieldValue('patOrApiKey', '');
            setFieldValue('customHeaders', [{ key: '', value: '' }]);
            setTouched({});
            setErrors({});
        },
        [setErrors, setFieldValue, setTouched]
    );

    const onCustomMcpAdd = useCallback(() => {
        onConnectorSelected({
            id: ConnectorType.McpServer,
            name: '',
            service: '',
            description: '',
            img: '',
        });
        goToNextStep();
    }, [onConnectorSelected, goToNextStep]);

    return (
        <>
            <h2 className={`${styles.title} ${styles.connectorPickerTitle}`}>{intl.formatMessage(ConnectorsResources.chooseAConnector)}</h2>
            <div className={styles.searchBarContainer}>
                <SearchBoxWithDebounce setSearchTerm={setSearchTerm} className={styles.searchBox} />
                <Button appearance="secondary" icon={<Add20Regular />} onClick={onCustomMcpAdd}>
                    {intl.formatMessage(ConnectorsResources.addMcpServer)}
                </Button>
            </div>
            <div className={styles.cardContainer}>
                <div className={styles.cardGrid}>
                    {filteredConnectorOptions.map((connector: ConnectorTypeOption, index: number) => {
                        const disabled =
                            (connector.id === ConnectorType.OutlookSendEmail || connector.id === ConnectorType.TeamsSendNotification) &&
                            existingConnectors?.some(existing =>
                                equals(existing.dataConnectorType, connector.id, AntUxStringComparison.IgnoreCase)
                            );
                        const selected = !disabled && connector.id === values.connectorType;
                        return (
                            <Card
                                key={`${connector.id}-${index}`}
                                className={styles.cardContent}
                                selected={selected}
                                disabled={disabled}
                                onClick={() => onConnectorSelected(connector)}
                            >
                                <CardHeader
                                    image={<img src={connector.img} alt={connector.name} className={styles.image} />}
                                    header={
                                        <div>
                                            <Text weight="semibold">{connector.name}</Text>
                                            <Text size={200} className={styles.serviceDescription}>
                                                {connector.service}
                                            </Text>
                                        </div>
                                    }
                                />
                                <Text size={200} className={styles.serviceMoreInfoText}>
                                    {connector.description}
                                </Text>
                            </Card>
                        );
                    })}
                </div>
            </div>
        </>
    );
};
