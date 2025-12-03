import { createTableColumn, Field, TableCellLayout, TableColumnDefinition } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import EditableGridFormik from '../../../../../Common/Components/EditableGrid/EditableGridFormik';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { MsiIdentity } from '../../../../../Common/Contracts/Azure/ArmObj';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { ManagedIdentityDropdownWithValidation } from '../Common/ManagedIdentityDropdownWithValidation';
import { ConnectorFormProps, KeyValuePair } from '../ConnectorWizardFormik';

interface LocalMcpServerFormProps {
    userAssignedIdentities?: { id: string; name: string }[];
    agentIdentity?: MsiIdentity;
    refreshAgent: () => void;
}

export const LocalMcpServerForm: React.FC<LocalMcpServerFormProps> = props => {
    const { userAssignedIdentities = [], agentIdentity, refreshAgent } = props;

    const intl = useIntl();

    const { values } = useFormikContext<ConnectorFormProps>();

    const argsColumns: TableColumnDefinition<{ value: string }>[] = useMemo(() => {
        return [
            createTableColumn<{ value: string }>({
                columnId: 'value',
                compare: (a, b) => a.value.localeCompare(b.value),
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.value),
                renderCell: item => {
                    const itemIndex = values.args?.indexOf(item) ?? -1;
                    return (
                        <TableCellLayout>
                            <InputFormik
                                name={`args[${itemIndex}].value`}
                                placeholder={intl.formatMessage(ConnectorsResources.argumentPlaceholder)}
                            />
                        </TableCellLayout>
                    );
                },
            }),
        ];
    }, [intl, values.args]);

    const envColumns: TableColumnDefinition<KeyValuePair>[] = useMemo(() => {
        return [
            createTableColumn<KeyValuePair>({
                columnId: 'key',
                compare: (a, b) => a.key.localeCompare(b.key),
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.key),
                renderCell: (item: KeyValuePair) => {
                    const itemIndex = values.env?.indexOf(item) ?? -1;
                    return (
                        <TableCellLayout>
                            <InputFormik name={`env[${itemIndex}].key`} placeholder={intl.formatMessage(ConnectorsResources.key)} />
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<KeyValuePair>({
                columnId: 'value',
                compare: (a, b) => a.value.localeCompare(b.value),
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.value),
                renderCell: item => {
                    const itemIndex = values.env?.indexOf(item) ?? -1;
                    return (
                        <TableCellLayout>
                            <InputFormik name={`env[${itemIndex}].value`} placeholder={intl.formatMessage(ConnectorsResources.value)} />
                        </TableCellLayout>
                    );
                },
            }),
        ];
    }, [intl, values.env]);

    return (
        <>
            <InputFormik
                name="command"
                label={intl.formatMessage(ConnectorsResources.command)}
                required
                orientation="vertical"
                placeholder={intl.formatMessage(ConnectorsResources.commandPlaceholder)}
            />

            <Field label={intl.formatMessage(ConnectorsResources.arguments)} orientation="vertical">
                <EditableGridFormik<{ value: string }>
                    name={'args'}
                    as="table"
                    columns={argsColumns}
                    columnSizingOptions={{
                        value: { defaultWidth: 396, idealWidth: 396, minWidth: 396 },
                    }}
                    emptyRow={{ value: '' }}
                />
            </Field>

            <Field label={intl.formatMessage(ConnectorsResources.environmentVariables)} orientation="vertical">
                <EditableGridFormik<KeyValuePair>
                    name={'env'}
                    as="table"
                    columns={envColumns}
                    columnSizingOptions={{
                        key: { defaultWidth: 192, idealWidth: 192 },
                        value: { defaultWidth: 192, idealWidth: 192 },
                    }}
                    emptyRow={{ key: '', value: '' }}
                />
            </Field>

            {agentIdentity && (
                <ManagedIdentityDropdownWithValidation
                    userAssignedIdentities={userAssignedIdentities}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                    required={false}
                />
            )}
        </>
    );
};
