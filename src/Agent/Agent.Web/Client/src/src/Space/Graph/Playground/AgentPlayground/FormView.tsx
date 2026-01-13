import { Button, Field, Link, mergeClasses, Spinner, Switch, Text, Tooltip } from '@fluentui/react-components';
import { Info16Regular, LightbulbRegular, PenSparkleRegular, WrenchRegular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentMemoryClient } from '../../../../Common/Clients/AgentMemoryClient';
import DropdownFormik from '../../../../Common/Components/Dropdown/DropdownFormik';
import InputFormik from '../../../../Common/Components/Input/InputFormik';
import TextareaFormik from '../../../../Common/Components/Textarea/TextareaFormik';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { PillSet } from '../../Common/PillSet';
import { useAgentPlaygroundStyles } from './AgentPlayground.Styles';
import { AgentPlaygroundFormValues, FormViewProps } from './Contracts';
import { SuggestionsArea } from './SuggestionsArea';
import { ToolsPickerDialog } from './ToolsPickerDialog';

export const FormView: FC<FormViewProps> = ({
    disableControls,
    handoffAgentsHook,
    improvementsAndSuggestionsHook,
    showSuggestionsArea,
    setShowSuggestionsArea,
    toolsPickerHook,
    isExistingAgent: isEditScenario,
    isOverrideScenario,
}) => {
    const intl = useIntl();
    const styles = useAgentPlaygroundStyles();
    const { values, setFieldValue } = useFormikContext<AgentPlaygroundFormValues>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [toolsPickerOpen, setToolsPickerOpen] = useState(false);
    const [documentCount, setDocumentCount] = useState<number | null>(null);

    useEffect(() => {
        if (values.enableMemory) {
            const fetchDocumentCount = async () => {
                const agentMemoryClient = AgentMemoryClient.getInstance(sreAgentEndpoint);
                const response = await agentMemoryClient.getDocumentCount();
                if (response.isSuccessful && response.content) {
                    setDocumentCount(response.content.count ?? 0);
                }
            };
            fetchDocumentCount();
        } else {
            setDocumentCount(null);
        }
    }, [values.enableMemory, sreAgentEndpoint]);

    return (
        <div className={styles.dialogContentOuterWrapper}>
            {improvementsAndSuggestionsHook.loadingImprovements && (
                <div className={styles.loadingOverlay}>
                    <Spinner size="large" aria-hidden="true" />
                </div>
            )}
            <div className={styles.dialogContentInnerWrapper}>
                <div className={styles.dialogContentWrapper}>
                    <div className={styles.formSection}>
                        <InputFormik
                            name="agentName"
                            required={!isOverrideScenario}
                            label={intl.formatMessage(ExtendedAgentsGraphResources.subagentName)}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.subagentNamePlaceholder)}
                            disabled={disableControls || isEditScenario || isOverrideScenario}
                            className={styles.formControl}
                            orientation="vertical"
                        />
                    </div>
                    <div className={styles.formSection}>
                        <Text size={400} weight="semibold" as="h2" style={{ margin: 0 }}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.instructions)}
                        </Text>
                        <div className={styles.instructionsButtonsContainer}>
                            <Button
                                appearance="secondary"
                                icon={<PenSparkleRegular />}
                                onClick={() => improvementsAndSuggestionsHook.getImprovements()}
                                disabled={disableControls || !values.instructions}
                                className={styles.formControl}
                            >
                                {intl.formatMessage(ExtendedAgentsGraphResources.refineWithAi)}
                            </Button>
                            <Button
                                appearance="secondary"
                                icon={<LightbulbRegular />}
                                onClick={() => {
                                    if (!showSuggestionsArea) {
                                        improvementsAndSuggestionsHook.getSuggestions();
                                        setShowSuggestionsArea(true);
                                    } else {
                                        setShowSuggestionsArea(false);
                                    }
                                }}
                                disabled={disableControls || (!showSuggestionsArea && !values.instructions)}
                                className={styles.formControl}
                            >
                                {showSuggestionsArea
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.hideAiSuggestions)
                                    : intl.formatMessage(ExtendedAgentsGraphResources.viewAiSuggestions)}
                            </Button>
                        </div>
                        {showSuggestionsArea && (
                            <SuggestionsArea
                                isLoading={improvementsAndSuggestionsHook.loadingSuggestions}
                                suggestions={improvementsAndSuggestionsHook.suggestions?.suggestions}
                                warnings={improvementsAndSuggestionsHook.suggestions?.warnings}
                                improvedPrompt={improvementsAndSuggestionsHook.suggestions?.improvedPrompt}
                                handoffDescription={improvementsAndSuggestionsHook.suggestions?.handoffDescription}
                            />
                        )}
                        <Field label={intl.formatMessage(ExtendedAgentsGraphResources.instructions)} required>
                            <TextareaFormik
                                name="instructions"
                                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.instructionsPlaceholder)}
                                disabled={disableControls}
                                className={mergeClasses(styles.formControl, styles.instructionsTextArea)}
                                orientation="vertical"
                                resize="vertical"
                                rows={6}
                            />
                        </Field>
                        <TextareaFormik
                            name="handoffInstructions"
                            required
                            label={intl.formatMessage(ExtendedAgentsGraphResources.agentHandoffInstructions)}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.agentHandoffInstructionsPlaceholder)}
                            disabled={disableControls}
                            className={styles.formControl}
                            orientation="vertical"
                            resize="vertical"
                            rows={3}
                        />
                    </div>
                    <div className={styles.formSection}>
                        <Text size={400} weight="semibold" as="h2" style={{ margin: 0 }}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.advancedSettings)}
                        </Text>
                        <DropdownFormik
                            name="handoffSubagents"
                            multiselect
                            label={intl.formatMessage(ExtendedAgentsGraphResources.handoffSubagents)}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.handoffSubagentsPlaceholder)}
                            value={handoffAgentsHook.dropdownDisplay}
                            options={handoffAgentsHook.handoffAgentOptions}
                            disabled={disableControls}
                            className={styles.formControl}
                            orientation="vertical"
                        />
                        <PillSet
                            items={handoffAgentsHook.pillItems}
                            onRemoveItem={agentName => handoffAgentsHook.onSelectedAgentChange(agentName, false)}
                            onClearAll={handoffAgentsHook.clear}
                            disabled={disableControls}
                            className={styles.formControl}
                        />
                        <Field label={intl.formatMessage(ExtendedAgentsGraphResources.tools)}>
                            <div>
                                <Button
                                    appearance="secondary"
                                    icon={<WrenchRegular />}
                                    onClick={() => setToolsPickerOpen(true)}
                                    disabled={disableControls || toolsPickerOpen}
                                    className={styles.formControl}
                                >
                                    {intl.formatMessage(ExtendedAgentsGraphResources.chooseTools)}
                                </Button>
                            </div>
                        </Field>
                        <PillSet
                            items={toolsPickerHook.pillItems}
                            onRemoveItem={toolName => toolsPickerHook.onSelectedToolChange(toolName, false)}
                            onClearAll={toolsPickerHook.onClearSelectedTools}
                            disabled={disableControls}
                            className={styles.formControl}
                        />
                        <Field>
                            <div className={styles.memoryToggleContainer}>
                                <Switch
                                    checked={values.enableMemory === true}
                                    onChange={(_, data) => {
                                        setFieldValue('enableMemory', data.checked);
                                    }}
                                    disabled={disableControls}
                                />
                                <span>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.agentMemoryLabel)}
                                    {values.enableMemory && documentCount !== null && documentCount > 0 && (
                                        <>
                                            {' ('}
                                            <Link href="#/views/settings/knowledgeBase">
                                                {documentCount} {documentCount === 1 ? 'document' : 'documents'}
                                            </Link>
                                            {')'}
                                        </>
                                    )}
                                </span>
                                <Tooltip
                                    content={intl.formatMessage(ExtendedAgentsGraphResources.agentMemoryHelp)}
                                    relationship="description"
                                >
                                    <Info16Regular className={styles.memoryInfoIcon} />
                                </Tooltip>
                            </div>
                        </Field>
                    </div>
                </div>
            </div>
            <ToolsPickerDialog {...toolsPickerHook} open={toolsPickerOpen} onClose={() => setToolsPickerOpen(false)} />
        </div>
    );
};
