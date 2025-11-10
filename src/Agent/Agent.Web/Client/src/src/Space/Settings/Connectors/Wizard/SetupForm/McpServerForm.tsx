import { createTableColumn, TableCellLayout, TableColumnDefinition } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import DropdownFormik from '../../../../../Common/Components/Dropdown/DropdownFormik';
import { OptionType } from '../../../../../Common/Components/Dropdown/DropdownNoFormik';
import EditableGridFormik from '../../../../../Common/Components/EditableGrid/EditableGridFormik';
import InputFormik from '../../../../../Common/Components/Input/InputFormik';
import { Connector } from '../../../../../Common/Contracts/Azure/SreAgent';
import { ConnectorsResources } from '../../../../../Strings/SREAgentResources';
import { AuthType, ConnectorFormProps, CustomHeader } from '../../ConnectorWizardFormik';
import { NameInputWithValidation } from '../Common/NameInputWithValidation';
import { SetupConnectorFormWrapper } from '../Common/SetupConnectorFormWrapper';
import { UrlInputWithValidation } from '../Common/UrlInputWithValidation';

interface McpServerFormProps {
    existingConnectors: Connector[] | undefined;
    isEditMode?: false;
}

export const McpServerForm: React.FC<McpServerFormProps> = props => {
    const { existingConnectors, isEditMode = false } = props;

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

    const editableGridColumns: TableColumnDefinition<CustomHeader>[] = useMemo(() => {
        return [
            createTableColumn<CustomHeader>({
                columnId: 'key',
                compare: (a, b) => a.key.localeCompare(b.key),
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.key),
                renderCell: (item: CustomHeader) => {
                    const itemIndex = values.customHeaders?.indexOf(item) ?? -1;
                    return (
                        <TableCellLayout>
                            <InputFormik
                                name={`customHeaders[${itemIndex}].key`}
                                placeholder={intl.formatMessage(ConnectorsResources.customHeadersKeyPlaceholder)}
                            />
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<CustomHeader>({
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
                            />
                        </TableCellLayout>
                    );
                },
            }),
        ];
    }, [intl, values.customHeaders]);

    return (
        <SetupConnectorFormWrapper>
            <NameInputWithValidation disabled={isEditMode} existingConnectors={existingConnectors} />
            <UrlInputWithValidation />
            <DropdownFormik
                name="authType"
                label={intl.formatMessage(ConnectorsResources.authenticationMethod)}
                required
                orientation="vertical"
                options={authOptions}
                placeholder={intl.formatMessage(ConnectorsResources.authenticationMethodPlaceholder)}
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
                <EditableGridFormik<CustomHeader>
                    name={'customHeaders'}
                    as="table"
                    columns={editableGridColumns}
                    columnSizingOptions={{ key: { defaultWidth: 192, idealWidth: 192 }, value: { defaultWidth: 192, idealWidth: 192 } }}
                    emptyRow={{ key: '', value: '' }}
                />
            )}
        </SetupConnectorFormWrapper>
    );
};
