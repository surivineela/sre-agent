import { Dropdown, Field, Option, OptionOnSelectData } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import InputFormik from '../../../Common/Components/Input/InputFormik';
import TextareaFormik from '../../../Common/Components/Textarea/TextareaFormik';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { AgentPromptTextarea } from '../../Components/AgentPromptTextarea';
import { ExtendedConnector } from '../../Contracts/ExtendedAgentGraph';
import { ParametersSection } from './Common/ParametersSection';
import { KustoToolFormProps } from './KustoToolUtilities';

interface CreateFormProps {
    connectors: ExtendedConnector[];
}

export const KustoToolCreateForm: FC<CreateFormProps> = ({ connectors }) => {
    const intl = useIntl();
    const { values, touched, errors, setFieldValue, setFieldTouched } = useFormikContext<KustoToolFormProps>();

    return (
        <>
            <InputFormik
                name="name"
                label={intl.formatMessage(ExtendedAgentsGraphResources.toolName)}
                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.toolNamePlaceholder)}
                orientation="vertical"
                required
            />
            <AgentPromptTextarea
                label={intl.formatMessage(ExtendedAgentsGraphResources.description)}
                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.descriptionPlaceholder)}
                prompt={values.description ?? ''}
                setPrompt={(description: string) => setFieldValue('description', description)}
                orientation="vertical"
                required
            />
            <Field
                label={intl.formatMessage(ExtendedAgentsGraphResources.connector)}
                orientation="vertical"
                validationState={touched.connector && errors.connector ? 'error' : undefined}
                validationMessage={touched.connector ? errors.connector : undefined}
                required
            >
                <Dropdown
                    value={values.connector || ''}
                    selectedOptions={values.connector ? [values.connector] : []}
                    onOptionSelect={(_, data: OptionOnSelectData) => {
                        const connectorName = data.selectedOptions[0];
                        const connector = connectors.find(c => c.name === connectorName);
                        // TODO: Check if dataSource parsing is correct
                        const databaseName = connector?.dataSource.split('.kusto.windows.net/')[1] || '';
                        setFieldValue('connector', connectorName || '');
                        setFieldValue('database', databaseName || '');
                    }}
                    onBlur={() => {
                        setFieldTouched('connector', true);
                    }}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.connectorPlaceholder)}
                >
                    {connectors?.map(connector => (
                        <Option key={connector.name} value={connector.name}>
                            {connector.name}
                        </Option>
                    ))}
                </Dropdown>
            </Field>
            <InputFormik
                name="database"
                label={intl.formatMessage(ExtendedAgentsGraphResources.kustoDatabaseLabel)}
                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.connectorPlaceholder)}
                orientation="vertical"
                disabled
            />
            <TextareaFormik
                name="query"
                label={intl.formatMessage(ExtendedAgentsGraphResources.kustoQueryLabel)}
                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.queryPlaceholder)}
                fieldProps={{
                    hint: (
                        <div>
                            {intl.formatMessage(SreAgentResources.kustoQueryTesterParameterUsage)}
                            <br />
                            {intl.formatMessage(SreAgentResources.kustoQueryTesterParameterExample)}
                            <br />
                            {intl.formatMessage(SreAgentResources.kustoQueryTesterParameterNote)}
                        </div>
                    ),
                }}
                orientation="vertical"
                required
            />
            <ParametersSection />
        </>
    );
};
