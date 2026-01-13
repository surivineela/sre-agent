import { Button, Divider, Field, Link, Spinner, Switch, Text, Tooltip } from '@fluentui/react-components';
import { Info16Regular, LightbulbRegular, PenSparkleRegular, WrenchRegular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentMemoryClient } from '../../../Common/Clients/AgentMemoryClient';
import DropdownFormik from '../../../Common/Components/Dropdown/DropdownFormik';
import InputFormik from '../../../Common/Components/Input/InputFormik';
import TextareaFormik from '../../../Common/Components/Textarea/TextareaFormik';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../../Hooks/useAgentSiteNavigate';
import { PillSet } from '../Common/PillSet';
import { useAgentCreateDialogStyles } from './AgentCreateDialog.Styles';
import { AgentCreateFormValues, FormViewProps } from './Contracts';
import { SuggestionsPanel } from './SuggestionsPanel';
import { TestPanel } from './TestPanel';
import { ToolsPanel } from './ToolsPanel';

export const FormView: FC<FormViewProps> = ({
    disableControls,
    handoffAgentsHook,
    improvementsAndSuggestionsHook,
    toolsPickerHook,
    openedPanel,
    closePanel,
    openPanel,
    isEditScenario,
    isOverrideScenario,
    testPanelProps,
}) => {
    const intl = useIntl();
    const styles = useAgentCreateDialogStyles();
    const { values, setFieldValue } = useFormikContext<AgentCreateFormValues>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [documentCount, setDocumentCount] = useState<number | null>(null);

    const navigate = useAgentSiteNavigate();

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
                                    improvementsAndSuggestionsHook.getSuggestions();
                                    openPanel('suggestions');
                                }}
                                disabled={disableControls || !values.instructions || openedPanel === 'suggestions'}
                                className={styles.formControl}
                            >
                                {intl.formatMessage(ExtendedAgentsGraphResources.viewAiSuggestions)}
                            </Button>
                        </div>
                        <TextareaFormik
                            name="instructions"
                            required
                            label={intl.formatMessage(ExtendedAgentsGraphResources.instructions)}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.instructionsPlaceholder)}
                            disabled={disableControls}
                            className={styles.formControl}
                            orientation="vertical"
                            resize="vertical"
                            rows={6}
                        />
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
                                    onClick={() => openPanel('tools')}
                                    disabled={disableControls || openedPanel === 'tools'}
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
                                            <Link
                                                onClick={() => {
                                                    navigate({
                                                        primaryNavItemValue: PrimaryNavItemValues.Settings,
                                                        secondaryNavItemValue: SecondaryNavItemValues.KnowledgeBase,
                                                    });
                                                }}
                                            >
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
                {openedPanel && <Divider vertical className={styles.dialogContentVerticalDivider} />}
                {openedPanel && <Divider className={styles.dialogContentHorizontalDivider} />}
                {openedPanel === 'tools' && (
                    <ToolsPanel
                        {...toolsPickerHook}
                        close={() => {
                            closePanel();
                            toolsPickerHook.onClearSearchAndExpandedGroups();
                        }}
                    />
                )}
                {openedPanel === 'suggestions' && (
                    <SuggestionsPanel
                        close={() => closePanel()}
                        isLoading={improvementsAndSuggestionsHook.loadingSuggestions}
                        suggestions={improvementsAndSuggestionsHook.suggestions?.suggestions}
                        warnings={improvementsAndSuggestionsHook.suggestions?.warnings}
                        improvedPrompt={improvementsAndSuggestionsHook.suggestions?.improvedPrompt}
                        handoffDescription={improvementsAndSuggestionsHook.suggestions?.handoffDescription}
                    />
                )}
                {openedPanel === 'test' && <TestPanel {...testPanelProps} />}
            </div>
        </div>
    );
};
