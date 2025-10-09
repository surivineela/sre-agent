import { Dropdown, Field, Input, Option, Textarea } from '@fluentui/react-components';
import { FC } from 'react';
import { IntlShape } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedConnector } from '../../../Contracts/ExtendedAgentGraph';
import { useCreationDialogStyles } from '../styles';

interface ConnectorDetailsStepProps {
    connector: Partial<ExtendedConnector>;
    onChange: (connector: Partial<ExtendedConnector>) => void;
    intl: IntlShape;
}

export const ConnectorDetailsStep: FC<ConnectorDetailsStepProps> = ({ connector, onChange, intl }) => {
    const styles = useCreationDialogStyles();
    return (
        <div className={styles.formSection}>
            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.connectorName)} required>
                <Input
                    value={connector.name || ''}
                    onChange={(_, data) => onChange({ ...connector, name: data.value })}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.connectorNamePlaceholder)}
                />
                <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.connectorNameHelp)}</div>
            </Field>

            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.connectorType)} required>
                <Dropdown
                    value={connector.type || 'Kusto'}
                    selectedOptions={[connector.type || 'Kusto']}
                    onOptionSelect={(_, data) => onChange({ ...connector, type: data.optionValue as string })}
                >
                    <Option value="Kusto">{intl.formatMessage(ExtendedAgentsGraphResources.kusto)}</Option>
                </Dropdown>
                <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.connectorTypeHelp)}</div>
            </Field>

            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.descriptionOptional)}>
                <Textarea
                    value={connector.description || ''}
                    onChange={(_, data) => onChange({ ...connector, description: data.value })}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.descriptionConnectorPlaceholder)}
                    rows={3}
                />
                <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.descriptionConnectorHelp)}</div>
            </Field>
        </div>
    );
};
