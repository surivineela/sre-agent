import { Card, CardHeader, mergeClasses, Text, Tooltip } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SearchBoxWithDebounce } from '../../../../Common/Components/SearchBox/SearchBoxWithDebounce';
import { Connector } from '../../../../Common/Contracts/Azure/SreAgent';
import { FirstPartyHelper } from '../../../../Common/Helpers/FirstPartyHelper';
import { Guid } from '../../../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../../../Common/Helpers/Strings';
import { ConnectorsResources } from '../../../../Strings/SREAgentResources';
import { ConnectorType, ConnectorTypeOption, connectorTypeOptions } from './Common/ConnectorType';
import { useConnectorWizardStyles } from './ConnectorWizard.styles';
import { AuthType, ConnectorFormProps } from './ConnectorWizardFormik';

interface ConnectorPickerProps {
    existingConnectors?: Connector[];
}

export const ConnectorPicker: React.FC<ConnectorPickerProps> = props => {
    const { existingConnectors } = props;
    const intl = useIntl();
    const styles = useConnectorWizardStyles();
    const { values, setFieldValue, setTouched, setErrors } = useFormikContext<ConnectorFormProps>();
    const { userInfo } = useContext(EnvironmentContext);

    const [searchTerm, setSearchTerm] = useState('');

    const filteredConnectorOptions = useMemo(() => {
        return connectorTypeOptions(intl).filter((option: ConnectorTypeOption) => {
            // Hide IcM Connector support for non 1p customers
            if (!FirstPartyHelper.isFirstPartyAgent(userInfo?.directoryId || '') && option.id === ConnectorType.Icm) {
                return false;
            }
            const searchLower = searchTerm.toLowerCase().trim();
            const matchesSearch =
                !searchLower ||
                option.name.toLowerCase().includes(searchLower) ||
                option.service.toLowerCase().includes(searchLower) ||
                option.description.toLowerCase().includes(searchLower);

            return matchesSearch;
        });
    }, [intl, searchTerm, userInfo?.directoryId]);

    const onConnectorSelected = useCallback(
        (connector: ConnectorTypeOption) => {
            setFieldValue('connectorType', connector.id);
            if (connector.id === ConnectorType.GitHub) {
                setFieldValue('url', 'https://api.githubcopilot.com/mcp/');
                setFieldValue('authType', AuthType.BearerToken);
            } else {
                setFieldValue('url', '');
                setFieldValue('authType', '');
            }

            if (connector.id === ConnectorType.OutlookSendEmail || connector.id === ConnectorType.TeamsSendNotification) {
                setFieldValue('name', `connector-${Guid.newTinyGuid()}`); // default connector name
            } else {
                setFieldValue('name', '');
            }
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

    return (
        <>
            <h2 className={mergeClasses(styles.title, styles.connectorPickerTitle)}>
                {intl.formatMessage(ConnectorsResources.chooseAConnector)}
            </h2>
            <div className={styles.searchBarContainer}>
                <SearchBoxWithDebounce setSearchTerm={setSearchTerm} className={styles.searchBox} size={'small'} />
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
                        const disabledTooltipContent =
                            connector.id === ConnectorType.TeamsSendNotification
                                ? intl.formatMessage(ConnectorsResources.onlyOneTeamsConnector)
                                : intl.formatMessage(ConnectorsResources.onlyOneOutlookConnector);

                        const cardContent = (
                            <Card
                                className={styles.cardContent}
                                selected={selected}
                                disabled={disabled}
                                onClick={() => onConnectorSelected(connector)}
                            >
                                <CardHeader
                                    image={<img src={connector.img} alt={connector.name} className={styles.image} />}
                                    header={<Text weight="semibold">{connector.name}</Text>}
                                    description={
                                        <Text size={200} className={styles.serviceDescription}>
                                            {connector.service}
                                        </Text>
                                    }
                                />
                                <Text size={200} className={styles.cardDescription}>
                                    {connector.description}
                                </Text>
                            </Card>
                        );

                        return disabled ? (
                            <Tooltip content={disabledTooltipContent} relationship="description" key={`${connector.id}-${index}`}>
                                <span tabIndex={0} role="group" aria-label={connector.name}>
                                    {cardContent}
                                </span>
                            </Tooltip>
                        ) : (
                            <span key={`${connector.id}-${index}`}>{cardContent}</span>
                        );
                    })}
                </div>
            </div>
        </>
    );
};
