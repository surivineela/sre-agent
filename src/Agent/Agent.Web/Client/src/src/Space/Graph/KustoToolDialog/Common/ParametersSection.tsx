import { Accordion, Text, tokens, Toolbar, ToolbarButton } from '@fluentui/react-components';
import { AddRegular, ChevronDownUpRegular, ChevronUpDownRegular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useKustoToolCreateDialogStyles } from '../KustoToolDialog.Styles';
import { KustoToolFormProps } from '../KustoToolUtilities';
import { ParameterAccordionItem } from './ParameterAccordionItem';

export const ParametersSection = () => {
    const intl = useIntl();
    const styles = useKustoToolCreateDialogStyles();
    const { values, setFieldValue } = useFormikContext<KustoToolFormProps>();
    const [openItems, setOpenItems] = useState<string[]>([]);

    const parameters = useMemo(() => values.parameters || [], [values.parameters]);

    const disableExpandAllButton = useMemo(
        () => openItems.length === values.parameters?.length,
        [openItems.length, values.parameters?.length]
    );
    const disableCollapseAllButton = useMemo(() => openItems.length === 0, [openItems.length]);

    const onAddParameter = useCallback(() => {
        const newParameter = {
            name: '',
            type: '',
            required: true,
            description: '',
            value: '',
        };
        const updatedParameters = [...parameters, newParameter];
        setFieldValue('parameters', updatedParameters);
        setOpenItems(prevOpenItems => {
            return [...prevOpenItems, parameters.length.toString()];
        });
    }, [parameters, setFieldValue]);

    const onExpandAll = useCallback(() => {
        const allIndexes = parameters.map((_, index) => index.toString());
        setOpenItems(allIndexes);
    }, [parameters]);

    const onCollapseAll = useCallback(() => {
        setOpenItems([]);
    }, []);

    const onToggleItem = useCallback((_: any, data: { value: string; openItems: string[] }) => {
        setOpenItems(data.openItems);
    }, []);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingHorizontalM }}>
            <Text as="h3" weight="semibold" size={400} style={{ margin: 0 }}>
                {intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}
            </Text>
            <Toolbar style={{ padding: 0 }} size="small">
                <ToolbarButton className={styles.toolbarButton} icon={<AddRegular />} onClick={onAddParameter}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.addParameter)}
                </ToolbarButton>
                <ToolbarButton
                    className={styles.toolbarButton}
                    icon={<ChevronUpDownRegular />}
                    onClick={onExpandAll}
                    disabled={disableExpandAllButton}
                >
                    {intl.formatMessage(SreAgentResources.expandAll)}
                </ToolbarButton>
                <ToolbarButton
                    className={styles.toolbarButton}
                    icon={<ChevronDownUpRegular />}
                    onClick={onCollapseAll}
                    disabled={disableCollapseAllButton}
                >
                    {intl.formatMessage(SreAgentResources.collapseAll)}
                </ToolbarButton>
            </Toolbar>
            <Accordion openItems={openItems} onToggle={onToggleItem} multiple collapsible className={styles.parametersAccordion}>
                {parameters.map((parameter, index) => (
                    <ParameterAccordionItem key={index} parameter={parameter} index={index} />
                ))}
            </Accordion>
        </div>
    );
};
