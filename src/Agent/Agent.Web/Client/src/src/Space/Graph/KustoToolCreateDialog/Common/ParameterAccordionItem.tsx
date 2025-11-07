import {
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Button,
    Checkbox,
    CheckboxOnChangeData,
    OptionOnSelectData,
    Text,
} from '@fluentui/react-components';
import { DeleteRegular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import DropdownNoFormik, { DropdownOptionBase, OptionType } from '../../../../Common/Components/Dropdown/DropdownNoFormik';
import InputFormik from '../../../../Common/Components/Input/InputFormik';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { AgentPromptTextarea } from '../../../Components/AgentPromptTextarea';
import { ToolParameter } from '../../../Contracts/ExtendedAgentGraph';
import { useKustoToolCreateDialogStyles } from '../KustoToolCreateDialog.Styles';
import { KustoToolFormProps } from '../KustoToolUtilities';

interface ParameterAccordionItemProps {
    parameter?: ToolParameter;
    index: number;
}

export const ParameterAccordionItem: FC<ParameterAccordionItemProps> = ({ parameter, index }) => {
    const intl = useIntl();
    const styles = useKustoToolCreateDialogStyles();
    const { values, setFieldValue } = useFormikContext<KustoToolFormProps>();

    const parameterRequired = useMemo(() => !!values.parameters?.[index]?.required, [index, values.parameters]);
    const parameterDefaultName = useMemo(() => `Parameter ${index + 1}`, [index]);
    const parameterType = useMemo(() => values.parameters?.[index]?.type, [index, values.parameters]);
    const parameterDescription = useMemo(() => values.parameters?.[index]?.description, [index, values.parameters]);

    // TODO: Double check these values
    const parameterTypeOptions = useMemo<DropdownOptionBase[]>(
        () => [
            {
                id: 'string',
                text: intl.formatMessage(ExtendedAgentsGraphResources.string),
                type: OptionType.Option,
            },
            {
                id: 'int',
                text: intl.formatMessage(ExtendedAgentsGraphResources.number),
                type: OptionType.Option,
            },
            {
                id: 'bool',
                text: intl.formatMessage(ExtendedAgentsGraphResources.boolean),
                type: OptionType.Option,
            },
            {
                id: 'datetime',
                text: intl.formatMessage(ExtendedAgentsGraphResources.datetime),
                type: OptionType.Option,
            },
        ],
        [intl]
    );

    const onCheckRequired = useCallback(
        (_: any, data: CheckboxOnChangeData) => {
            if (values.parameters && values.parameters[index]) {
                const updatedParameters = values.parameters.map((param, i) =>
                    i === index ? { ...param, required: data.checked as boolean } : param
                );
                setFieldValue('parameters', updatedParameters);
            }
        },
        [index, setFieldValue, values.parameters]
    );

    const onDelete = useCallback(() => {
        const updatedParameters = values.parameters?.filter((_param, i) => i !== index);
        setFieldValue('parameters', updatedParameters);
    }, [index, setFieldValue, values.parameters]);

    const onTypeSelect = useCallback(
        (_: any, data: OptionOnSelectData) => {
            if (values.parameters && values.parameters[index]) {
                const updatedParameters = values.parameters.map((param, i) => (i === index ? { ...param, type: data.optionValue } : param));
                setFieldValue('parameters', updatedParameters);
            }
        },
        [index, setFieldValue, values.parameters]
    );

    const setDescription = useCallback(
        (description: string) => {
            if (values.parameters && values.parameters[index]) {
                const updatedParameters = values.parameters.map((param, i) => (i === index ? { ...param, description } : param));
                setFieldValue('parameters', updatedParameters);
            }
        },
        [index, setFieldValue, values.parameters]
    );

    return (
        <AccordionItem value={index.toString()} key={index} className={styles.parameterAccordionItem}>
            <AccordionHeader>
                <div className={styles.parameterAccordionHeader}>
                    <div>
                        <Text weight="semibold">{parameter?.name || parameterDefaultName}</Text>
                    </div>
                    <div onClick={e => e.stopPropagation()}>
                        <Checkbox
                            label={intl.formatMessage(ExtendedAgentsGraphResources.required)}
                            checked={parameterRequired}
                            onChange={onCheckRequired}
                        />
                        <Button
                            size="small"
                            appearance="transparent"
                            icon={<DeleteRegular />}
                            aria-label={intl.formatMessage(SreAgentResources.delete)}
                            onClick={onDelete}
                        />
                    </div>
                </div>
            </AccordionHeader>
            <AccordionPanel className={styles.parameterAccordionDescription}>
                <div className={styles.parameterNameAndType}>
                    <InputFormik
                        name={`parameters.${index}.name`}
                        label={intl.formatMessage(ExtendedAgentsGraphResources.parameterName)}
                        placeholder={intl.formatMessage(ExtendedAgentsGraphResources.parameterNamePlaceholder)}
                        orientation="vertical"
                        required
                    />
                    <DropdownNoFormik
                        label={intl.formatMessage(ExtendedAgentsGraphResources.inputType)}
                        placeholder={intl.formatMessage(ExtendedAgentsGraphResources.inputTypePlaceholder)}
                        options={parameterTypeOptions}
                        onOptionSelect={onTypeSelect}
                        value={parameterType ? parameterTypeOptions.find(option => option.id === parameterType)?.text : ''}
                        selectedOptions={parameterType ? [parameterType] : []}
                        orientation="vertical"
                        required
                    />
                </div>
                <AgentPromptTextarea
                    label={intl.formatMessage(ExtendedAgentsGraphResources.description)}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.descriptionPlaceholder)}
                    prompt={parameterDescription}
                    setPrompt={setDescription}
                    orientation="vertical"
                />
            </AccordionPanel>
        </AccordionItem>
    );
};
