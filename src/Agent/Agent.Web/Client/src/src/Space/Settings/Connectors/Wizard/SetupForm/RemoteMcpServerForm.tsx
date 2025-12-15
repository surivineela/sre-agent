import { createTableColumn, TableCellLayout, TableColumnDefinition } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import DropdownFormik from '../../../../../Common/Components/Dropdown/DropdownFormik';
import { OptionType } from '../../../../../Common/Components/Dropdown/DropdownNoFormik';
import EditableGridFormik from '../../../../../Common/Components/EditableGrid/EditableGridFormik';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { ConnectorType } from '../Common/ConnectorType';
import { UrlInput } from '../Common/UrlInput';
import { AuthType, ConnectorFormProps, KeyValuePair } from '../ConnectorWizardFormik';

interface RemoteMcpServerFormProps {
    connectorType: ConnectorType;
}

export const RemoteMcpServerForm: React.FC<RemoteMcpServerFormProps> = props => {
    const { connectorType } = props;

    const intl = useIntl();

    const { values } = useFormikContext<ConnectorFormProps>();

    const authOptions = [
        {
            id: AuthType.BearerToken,
            text: intl.formatMessage(ConnectorsResources.bearerToken),
            type: OptionType.Option,
        },
        {
            id: AuthType.CustomHeaders,
            text: intl.formatMessage(ConnectorsResources.customHeaders),
            type: OptionType.Option,
        },
    ];

    const editableGridColumns: TableColumnDefinition<KeyValuePair>[] = useMemo(() => {
        return [
            createTableColumn<KeyValuePair>({
                columnId: 'key',
                compare: (a, b) => a.key.localeCompare(b.key),
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.key),
                renderCell: (item: KeyValuePair) => {
                    const itemIndex = values.customHeaders?.indexOf(item) ?? -1;
                    return (
                        <TableCellLayout>
                            <InputFormik
                                name={`customHeaders[${itemIndex}].key`}
                                placeholder={intl.formatMessage(ConnectorsResources.customHeadersKeyPlaceholder)}
                                size={'small'}
                            />
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<KeyValuePair>({
                columnId: 'value',
                compare: (a, b) => a.value.localeCompare(b.value),
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.value),
                renderCell: item => {
                    const itemIndex = values.customHeaders?.indexOf(item) ?? -1;
                    return (
                        <TableCellLayout>
                            <InputFormik
                                name={`customHeaders[${itemIndex}].value`}
                                placeholder={intl.formatMessage(ConnectorsResources.customHeadersValuePlaceholder)}
                                size={'small'}
                            />
                        </TableCellLayout>
                    );
                },
            }),
        ];
    }, [intl, values.customHeaders]);

    return (
        <>
            <UrlInput />
            <DropdownFormik
                name="authType"
                label={intl.formatMessage(ConnectorsResources.authenticationMethod)}
                required
                orientation="vertical"
                options={authOptions}
                value={
                    !values.authType
                        ? undefined
                        : values.authType === AuthType.BearerToken
                          ? intl.formatMessage(ConnectorsResources.bearerToken)
                          : intl.formatMessage(ConnectorsResources.customHeaders)
                }
                placeholder={intl.formatMessage(ConnectorsResources.authenticationMethodPlaceholder)}
                disabled={connectorType === ConnectorType.GitHub}
            />
            {values.authType === AuthType.BearerToken && (
                <InputFormik
                    name="patOrApiKey"
                    label={intl.formatMessage(ConnectorsResources.patOrApiKey)}
                    required
                    orientation="vertical"
                    type="password"
                    placeholder={intl.formatMessage(ConnectorsResources.patOrApiKeyPlaceholder)}
                />
            )}
            {values.authType === AuthType.CustomHeaders && (
                <EditableGridFormik<KeyValuePair>
                    name={'customHeaders'}
                    as="table"
                    columns={editableGridColumns}
                    columnSizingOptions={{
                        key: { defaultWidth: 192, idealWidth: 192 },
                        value: { defaultWidth: 192, idealWidth: 192 },
                    }}
                    emptyRow={{ key: '', value: '' }}
                />
            )}
        </>
    );
};
