import { Accordion, AccordionHeader, AccordionItem, AccordionPanel, Text, tokens } from '@fluentui/react-components';
import { Checkmark16Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import InputFormik from '../../../../Common/Components/Input/InputFormik';
import InputNoFormik from '../../../../Common/Components/Input/InputNoFormik';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ToolParameter } from '../../../Contracts/ExtendedAgentGraph';
import { useKustoToolCreateDialogStyles } from '../KustoToolDialog.Styles';
import { KustoToolFormProps } from '../KustoToolUtilities';

export const TestValueAccordion: FC = () => {
    const intl = useIntl();
    const styles = useKustoToolCreateDialogStyles();
    const { values } = useFormikContext<KustoToolFormProps>();
    const [openItems, setOpenItems] = useState<string[]>(['test-values']);

    const onToggleItem = useCallback((_: any, data: { value: string; openItems: string[] }) => {
        setOpenItems(data.openItems);
    }, []);

    return values.parameters && values.parameters.length > 0 ? (
        <Accordion openItems={openItems} onToggle={onToggleItem} multiple collapsible style={{ width: '100%' }}>
            <AccordionItem value="test-values" className={styles.testValueAccordionItem}>
                <AccordionHeader>
                    <Text size={300} weight="semibold">
                        {intl.formatMessage(ExtendedAgentsGraphResources.testValues)}
                    </Text>
                </AccordionHeader>
                <AccordionPanel>
                    <div className={styles.testValueColumnHeaders}>
                        <Text size={300} style={{ color: tokens.colorNeutralForeground2 }}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.parameterName)}
                        </Text>
                        <Text size={300} style={{ color: tokens.colorNeutralForeground2 }}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.inputValue)}
                        </Text>
                        <Text size={300} style={{ color: tokens.colorNeutralForeground2 }}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.required)}
                        </Text>
                    </div>
                    {values.parameters.map((parameter, index) => (
                        <div key={index} className={styles.testParameterInputs}>
                            {/* TODO: Double check with designs */}
                            <InputNoFormik value={parameter.name || 'Parameter name will show here'} disabled />
                            <ParameterValueInput parameter={parameter} index={index} />
                            {parameter.required ? (
                                <Checkmark16Regular style={{ marginTop: '6px', marginLeft: tokens.spacingHorizontalS }} />
                            ) : null}
                        </div>
                    ))}
                </AccordionPanel>
            </AccordionItem>
        </Accordion>
    ) : null;
};

interface ParameterValueInputProps {
    parameter: ToolParameter;
    index: number;
}

const ParameterValueInput = ({ parameter, index }: ParameterValueInputProps) => {
    const intl = useIntl();

    const placeholder = useMemo(() => {
        switch (parameter.type) {
            case 'string':
                return intl.formatMessage(ExtendedAgentsGraphResources.inputStringValuePlaceholder);
            case 'number':
                return intl.formatMessage(ExtendedAgentsGraphResources.inputNumberValuePlaceholder);
            case 'boolean':
                return intl.formatMessage(ExtendedAgentsGraphResources.inputBooleanValuePlaceholder);
            case 'datetime':
                return intl.formatMessage(ExtendedAgentsGraphResources.inputDatetimeValuePlaceholder);
            default:
                return intl.formatMessage(ExtendedAgentsGraphResources.inputValuePlaceholder);
        }
    }, [intl, parameter.type]);

    return <InputFormik name={`parameters.${index}.value`} placeholder={placeholder} orientation="vertical" />;
};
