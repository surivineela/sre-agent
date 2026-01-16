import { Field, Radio, RadioGroup, Text } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { ConnectorType } from '../Common/ConnectorType';
import { NameInput } from '../Common/NameInput';
import { useConnectorWizardStyles } from '../ConnectorWizard.styles';
import { ConnectorFormProps, McpConnectionType } from '../ConnectorWizardFormik';
import { LocalMcpServerForm } from './LocalMcpServerForm';
import { RemoteMcpServerForm } from './RemoteMcpServerForm';

interface McpServerFormProps {
    isEditMode?: boolean;
    userAssignedIdentities?: { id: string; name: string }[];
    agentIdentity?: MsiIdentity;
    refreshAgent: () => void;
}

export const McpServerForm: React.FC<McpServerFormProps> = props => {
    const { isEditMode = false, userAssignedIdentities = [], agentIdentity, refreshAgent } = props;

    const intl = useIntl();
    const styles = useConnectorWizardStyles();

    const { values, setFieldValue } = useFormikContext<ConnectorFormProps>();

    const connectorType = useMemo(() => values.connectorType as ConnectorType, [values.connectorType]);

    return (
        <>
            <NameInput disabled={isEditMode} />

            <Field label={intl.formatMessage(ConnectorsResources.connectionType)} required orientation="vertical">
                <RadioGroup
                    layout="horizontal"
                    name="mcpConnectionType"
                    value={values.mcpConnectionType || McpConnectionType.Remote}
                    onChange={(_, data) => setFieldValue('mcpConnectionType', data.value)}
                >
                    <Radio
                        value={McpConnectionType.Remote}
                        label={
                            <div className={styles.accountText}>
                                <Text weight="semibold">{intl.formatMessage(ConnectorsResources.streamableHttp)}</Text>
                                <Text size={200}>{intl.formatMessage(ConnectorsResources.connectViaUrlEndpoint)}</Text>
                            </div>
                        }
                    />
                    <Radio
                        value={McpConnectionType.Local}
                        label={
                            <div className={styles.accountText}>
                                <Text weight="semibold">{intl.formatMessage(ConnectorsResources.localProcess)}</Text>
                                <Text size={200}>{intl.formatMessage(ConnectorsResources.runLocalExecutable)}</Text>
                            </div>
                        }
                    />
                </RadioGroup>
            </Field>

            {values.mcpConnectionType === McpConnectionType.Remote && <RemoteMcpServerForm connectorType={connectorType} />}

            {values.mcpConnectionType === McpConnectionType.Local && (
                <LocalMcpServerForm
                    userAssignedIdentities={userAssignedIdentities}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                />
            )}
        </>
    );
};
