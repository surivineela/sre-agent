import { Button, Dropdown, Field, Input, Option, Spinner, Switch, Text, Textarea, Tooltip } from '@fluentui/react-components';
import { Info16Regular, Lightbulb24Regular, Wand24Regular, Warning16Regular } from '@fluentui/react-icons';
import { FC, useContext, useMemo, useState } from 'react';
import { IntlShape } from 'react-intl';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { improvePrompt, PromptImprovementResponse } from '../services/promptImprovementService';
import { useCreationDialogStyles } from '../styles';

interface AgentDetailsStepProps {
    agent: Partial<ExtendedAgent>;
    existingAgents: ExtendedAgent[];
    existingTools: ExtendedTool[];
    systemTools: SystemTool[];
    onChange: (agent: Partial<ExtendedAgent>) => void;
    intl: IntlShape;
}

export const AgentDetailsStep: FC<AgentDetailsStepProps> = ({ agent, existingAgents, existingTools, systemTools, onChange, intl }) => {
    const styles = useCreationDialogStyles();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [isFetchingSuggestions, setIsFetchingSuggestions] = useState(false);
    const [isApplyingImprovement, setIsApplyingImprovement] = useState(false);
    const [promptImprovement, setPromptImprovement] = useState<PromptImprovementResponse | null>(null);
    const [promptImprovementError, setPromptImprovementError] = useState<string | null>(null);

    const currentTools = useMemo(() => agent.tools ?? [], [agent.tools]);
    const currentSystemTools = useMemo(() => agent.systemTools ?? [], [agent.systemTools]);
    const currentHandoffs = useMemo(() => agent.handoffs ?? [], [agent.handoffs]);

    const availableHandoffs = useMemo(() => {
        const names = new Set(existingAgents.map(a => a.name).filter(name => !!name && name !== agent.name) as string[]);
        currentHandoffs.forEach(name => {
            if (name) {
                names.add(name);
            }
        });
        return Array.from(names);
    }, [agent.name, currentHandoffs, existingAgents]);

    const getPromptErrorMessage = (error: unknown): string => {
        const errorMessage = typeof error === 'string' ? error : ((error as any)?.message?.toString?.() ?? '');

        if (errorMessage.includes('400')) {
            if (errorMessage.includes('Chat client is not available')) {
                return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsChatUnavailable);
            }
            return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsInvalidRequest);
        }

        if (errorMessage.includes('500')) {
            return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsServerError);
        }

        if (errorMessage.includes('403')) {
            return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsForbidden);
        }

        return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsError);
    };

    const handleFetchSuggestions = async () => {
        if (!agent.instructions?.trim()) {
            return;
        }

        setIsFetchingSuggestions(true);
        setPromptImprovementError(null);

        try {
            const result = await improvePrompt(sreAgentEndpoint, agent.instructions);
            console.log('AI Improvement API Response:', result);
            console.log('Suggestions length:', result.suggestions?.length);
            console.log('Warnings length:', result.warnings?.length);
            setPromptImprovement(result);
        } catch (error) {
            console.error('Failed to fetch AI suggestions:', error);
            setPromptImprovementError(getPromptErrorMessage(error));
        } finally {
            setIsFetchingSuggestions(false);
        }
    };

    const handleImproveWithAI = async () => {
        if (!agent.instructions?.trim()) {
            return;
        }

        setIsApplyingImprovement(true);
        setPromptImprovementError(null);

        try {
            const result = await improvePrompt(sreAgentEndpoint, agent.instructions);
            setPromptImprovement(result);

            if (result.improvedPrompt?.trim()) {
                onChange({ ...agent, instructions: result.improvedPrompt });
            }
        } catch (error) {
            console.error('Failed to apply AI improvements:', error);
            setPromptImprovementError(getPromptErrorMessage(error));
        } finally {
            setIsApplyingImprovement(false);
        }
    };

    return (
        <div className={styles.formSection}>
            <Field
                label={
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
                        <span>
                            {intl.formatMessage(ExtendedAgentsGraphResources.agentName)} <span style={{ color: '#c53030' }}>*</span>
                        </span>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <Text size={200}>{intl.formatMessage(ExtendedAgentsGraphResources.metaAgentOverrideLabel)}</Text>
                            <Switch
                                checked={(agent as any).metaAgentOverride === true}
                                onChange={(_, data) => onChange({ ...agent, metaAgentOverride: data.checked } as any)}
                            />
                            <Tooltip
                                content={intl.formatMessage(ExtendedAgentsGraphResources.metaAgentOverrideReasonTooltip)}
                                relationship="description"
                            >
                                <Info16Regular style={{ fontSize: '14px', color: '#6264A7', cursor: 'help' }} />
                            </Tooltip>
                        </div>
                    </div>
                }
            >
                <Input
                    value={agent.name || ''}
                    onChange={(_, data) => onChange({ ...agent, name: data.value })}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.agentNamePlaceholder)}
                />
                <div className={styles.helpText}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.agentNameHelp)}
                    {(agent as any).metaAgentOverride && (
                        <div className={styles.metaAgentInfo}>
                            <Info16Regular aria-hidden className={styles.metaAgentInfoIcon} />
                            <span>
                                This will create both your agent ("{agent.name || 'YourAgent'}") and a separate "meta_agent" for
                                orchestration.
                            </span>
                        </div>
                    )}
                </div>
            </Field>

            <Field
                label={
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
                        <span>
                            {intl.formatMessage(ExtendedAgentsGraphResources.instructions)}
                            <span style={{ color: 'red', marginLeft: '4px' }}>*</span>
                        </span>
                        <div className={styles.promptImprovementActions}>
                            <Tooltip
                                content={intl.formatMessage(ExtendedAgentsGraphResources.suggestionsTooltip)}
                                relationship="description"
                            >
                                <Button
                                    appearance="secondary"
                                    size="small"
                                    disabled={!agent.instructions?.trim() || isFetchingSuggestions || isApplyingImprovement}
                                    onClick={handleFetchSuggestions}
                                    className={styles.promptImprovementButton}
                                >
                                    {isFetchingSuggestions ? (
                                        <>
                                            <Spinner size="tiny" />
                                            {intl.formatMessage(ExtendedAgentsGraphResources.loadingSuggestions)}
                                        </>
                                    ) : (
                                        <>
                                            <Lightbulb24Regular />
                                            {intl.formatMessage(ExtendedAgentsGraphResources.suggestionsButton)}
                                        </>
                                    )}
                                </Button>
                            </Tooltip>
                            <Tooltip
                                content={intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsTooltip)}
                                relationship="description"
                            >
                                <Button
                                    appearance="primary"
                                    size="small"
                                    disabled={!agent.instructions?.trim() || isApplyingImprovement}
                                    onClick={handleImproveWithAI}
                                    className={styles.promptImprovementButton}
                                >
                                    {isApplyingImprovement ? (
                                        <>
                                            <Spinner size="tiny" />
                                            {intl.formatMessage(ExtendedAgentsGraphResources.improvingInstructions)}
                                        </>
                                    ) : (
                                        <>
                                            <Wand24Regular />
                                            {intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsButton)}
                                        </>
                                    )}
                                </Button>
                            </Tooltip>
                        </div>
                    </div>
                }
                className={styles.wideInstructionsField}
            >
                <div className={styles.promptImprovementContainer}>
                    {promptImprovementError && <Text className={styles.inlineError}>{promptImprovementError}</Text>}

                    {promptImprovement && (
                        <div className={styles.promptImprovementInline}>
                            {promptImprovement.improvedPrompt?.trim() && (
                                <div className={styles.promptImprovementInlineGroup}>
                                    <Text className={styles.promptImprovementSectionTitle}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.improvedInstructionsLabel)}
                                    </Text>
                                    <div className={styles.promptPreview}>{promptImprovement.improvedPrompt}</div>
                                </div>
                            )}

                            <div className={styles.promptImprovementInlineGroup}>
                                <Text className={styles.promptImprovementSectionTitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.improvementSuggestions)}
                                </Text>
                                <div className={styles.promptImprovementList}>
                                    {promptImprovement.suggestions.length > 0 ? (
                                        promptImprovement.suggestions.map((suggestion, index) => (
                                            <Text key={`suggestion-${index}`} className={styles.promptImprovementItem}>
                                                • {suggestion}
                                            </Text>
                                        ))
                                    ) : (
                                        <Text className={styles.promptImprovementEmpty}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.noImprovementSuggestions)}
                                        </Text>
                                    )}
                                </div>
                            </div>

                            <div className={styles.promptImprovementInlineGroup}>
                                <Text className={styles.promptImprovementSectionTitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.improvementWarnings)}
                                </Text>
                                <div className={styles.promptImprovementList}>
                                    {promptImprovement.warnings.length > 0 ? (
                                        promptImprovement.warnings.map((warning, index) => (
                                            <Text key={`warning-${index}`} className={styles.promptImprovementItem}>
                                                <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                                                    <Warning16Regular aria-hidden />
                                                    {warning}
                                                </span>
                                            </Text>
                                        ))
                                    ) : (
                                        <Text className={styles.promptImprovementEmpty}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.noImprovementWarnings)}
                                        </Text>
                                    )}
                                </div>
                            </div>
                        </div>
                    )}

                    <Textarea
                        value={agent.instructions || ''}
                        onChange={(_, data) => onChange({ ...agent, instructions: data.value })}
                        placeholder={intl.formatMessage(ExtendedAgentsGraphResources.instructionsPlaceholder)}
                        rows={8}
                        style={{
                            minHeight: '140px',
                            fontSize: '14px',
                            lineHeight: '1.5',
                        }}
                    />
                </div>
                <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.instructionsHelp)}</div>
            </Field>

            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.toolsOptional)}>
                <Dropdown
                    multiselect
                    disabled={existingTools.length === 0}
                    placeholder={
                        existingTools.length === 0
                            ? 'No extended tools available'
                            : intl.formatMessage(ExtendedAgentsGraphResources.toolsPlaceholder)
                    }
                    selectedOptions={currentTools}
                    onOptionSelect={(_, data) => {
                        const selected = data.selectedOptions;
                        onChange({ ...agent, tools: selected });
                    }}
                >
                    {existingTools.map(tool => (
                        <Option key={tool.name} value={tool.name}>
                            {tool.name}
                        </Option>
                    ))}
                </Dropdown>
                <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.toolsHelp)}</div>
            </Field>

            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.systemToolsOptional)}>
                <Dropdown
                    multiselect
                    disabled={systemTools.length === 0}
                    placeholder={
                        systemTools.length === 0
                            ? 'No system tools available'
                            : intl.formatMessage(ExtendedAgentsGraphResources.systemToolsPlaceholder)
                    }
                    selectedOptions={currentSystemTools}
                    onOptionSelect={(_, data) => {
                        const selected = data.selectedOptions;
                        onChange({ ...agent, systemTools: selected });
                    }}
                >
                    {systemTools.map(tool => (
                        <Option key={tool.name} value={tool.name} text={`${tool.name} (${tool.category})`}>
                            {tool.name} ({tool.category})
                        </Option>
                    ))}
                </Dropdown>
                <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.systemToolsHelp)}</div>
            </Field>

            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.relationshipCurrentHandoffs)}>
                <Dropdown
                    multiselect
                    disabled={availableHandoffs.length === 0}
                    placeholder={
                        availableHandoffs.length === 0
                            ? 'No handoff agents available'
                            : intl.formatMessage(ExtendedAgentsGraphResources.relationshipSelectAgent)
                    }
                    selectedOptions={currentHandoffs}
                    onOptionSelect={(_, data) => {
                        const selected = data.selectedOptions;
                        onChange({ ...agent, handoffs: selected });
                    }}
                >
                    {availableHandoffs.map(name => (
                        <Option key={name} value={name}>
                            {name}
                        </Option>
                    ))}
                </Dropdown>
            </Field>
        </div>
    );
};
