import {
    Button,
    Combobox,
    Dropdown,
    Field,
    Input,
    InputProps,
    Option,
    OptionGroup,
    Spinner,
    Switch,
    Text,
    Textarea,
    TextareaProps,
    Tooltip,
} from '@fluentui/react-components';
import {
    ChevronDown12Regular,
    ChevronRight12Regular,
    Info16Regular,
    Lightbulb24Regular,
    Wand24Regular,
    Warning16Regular,
} from '@fluentui/react-icons';
import { ChangeEventHandler, FC, KeyboardEvent, MouseEvent, useContext, useEffect, useMemo, useState } from 'react';
import { IntlShape } from 'react-intl';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { improvePrompt, PromptImprovementResponse } from '../services/promptImprovementService';
import { useCreationDialogStyles } from '../styles';
import { ENTITY_NAME_MAX_LENGTH, isEntityNameValid, sanitizeEntityName } from '../utils/nameValidation';

const MAX_TOOL_DESCRIPTION_LENGTH = 220;

interface AgentDetailsStepProps {
    agent: Partial<ExtendedAgent>;
    existingAgents: ExtendedAgent[];
    existingTools: ExtendedTool[];
    systemTools: SystemTool[];
    onChange: (agent: Partial<ExtendedAgent>) => void;
    intl: IntlShape;
    allowAgentNameEdit?: boolean;
    showMetaAgentOverride?: boolean;
}
export const AgentDetailsStep: FC<AgentDetailsStepProps> = ({
    agent,
    existingAgents,
    existingTools,
    systemTools,
    onChange,
    intl,
    allowAgentNameEdit = true,
    showMetaAgentOverride = true,
}) => {
    const styles = useCreationDialogStyles();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [isFetchingSuggestions, setIsFetchingSuggestions] = useState(false);
    const [isApplyingImprovement, setIsApplyingImprovement] = useState(false);
    const [promptImprovement, setPromptImprovement] = useState<PromptImprovementResponse | null>(null);
    const [promptImprovementMode, setPromptImprovementMode] = useState<'suggestions' | 'improvement' | null>(null);
    const [promptImprovementError, setPromptImprovementError] = useState<string | null>(null);
    const [systemToolSearch, setSystemToolSearch] = useState('');
    const [collapsedCategories, setCollapsedCategories] = useState<Record<string, boolean>>({});

    const handleAgentNameChange: NonNullable<InputProps['onChange']> = (_event, data) => {
        const sanitized = sanitizeEntityName(data.value ?? '');
        onChange({ ...agent, name: sanitized });
    };

    const agentNameValue = agent.name ?? '';
    const metaAgentDisplayName = agent.name?.trim() || intl.formatMessage(ExtendedAgentsGraphResources.metaAgentOverridePlaceholderName);
    const agentNameValidationState = agentNameValue.length === 0 || isEntityNameValid(agentNameValue) ? 'none' : 'error';
    const agentNameValidationMessage =
        agentNameValidationState === 'error'
            ? intl.formatMessage(ExtendedAgentsGraphResources.entityNameValidationMessage, {
                  maxLength: ENTITY_NAME_MAX_LENGTH,
              })
            : undefined;

    const handleAgentInstructionsChange: NonNullable<TextareaProps['onChange']> = (_event, data) => {
        onChange({ ...agent, instructions: data.value });
    };

    const handleSystemToolSearchChange: ChangeEventHandler<HTMLInputElement> = event => {
        setSystemToolSearch(event.target.value);
    };

    // MCP Tools state
    const [mcpToolsByConnection, setMcpToolsByConnection] = useState<
        Array<{ connectionName: string; connectionId: string; serviceType?: string; tools: Array<{ name: string; description?: string }> }>
    >([]);
    const [isFetchingMcpTools, setIsFetchingMcpTools] = useState(false);
    const [mcpToolsError, setMcpToolsError] = useState<string | null>(null);

    const currentTools = useMemo(() => agent.tools ?? [], [agent.tools]);
    const currentSystemTools = useMemo(() => agent.systemTools ?? [], [agent.systemTools]);
    const currentMcpTools = useMemo(() => agent.mcpTools ?? [], [agent.mcpTools]);
    const currentHandoffs = useMemo(() => agent.handoffs ?? [], [agent.handoffs]);
    const hasActiveSystemToolSearch = systemToolSearch.trim().length > 0;

    const toggleCategoryCollapse = (category: string) => {
        setCollapsedCategories(previous => ({
            ...previous,
            [category]: previous[category] === false ? true : false,
        }));
    };

    const handleCategoryClick = (event: MouseEvent<HTMLSpanElement>, category: string) => {
        event.preventDefault();
        event.stopPropagation();
        toggleCategoryCollapse(category);
    };

    const handleCategoryKeyDown = (event: KeyboardEvent<HTMLSpanElement>, category: string) => {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            event.stopPropagation();
            toggleCategoryCollapse(category);
        }
    };

    const resolveExtendedToolCategory = (tool: ExtendedTool): string => {
        const metadataCategory = typeof tool.metadata?.category === 'string' ? tool.metadata.category.trim() : '';
        if (metadataCategory) {
            return metadataCategory;
        }

        const attributeCategory = tool.attributes?.find(attribute => attribute?.toLowerCase().startsWith('category:'));
        if (attributeCategory) {
            const value = attributeCategory.split(':')[1];
            if (value) {
                const trimmed = value.trim();
                if (trimmed) {
                    return trimmed;
                }
            }
        }

        return tool.type?.trim() || systemToolCategoryFallback;
    };

    const normalizedSelectedSystemTools = useMemo(() => {
        return new Set(currentSystemTools.map(tool => tool.trim().toLowerCase()).filter(Boolean));
    }, [currentSystemTools]);

    const systemToolCategoryFallback = useMemo(
        () => intl.formatMessage(ExtendedAgentsGraphResources.relationshipToolCategoryFallback),
        [intl]
    );

    const systemToolLookup = useMemo(() => {
        const lookup = new Map<string, SystemTool>();
        systemTools.forEach(tool => {
            const trimmedName = tool.name?.trim();
            if (!trimmedName) {
                return;
            }
            lookup.set(trimmedName, tool);
        });
        return lookup;
    }, [systemTools]);

    const filteredSystemToolGroups = useMemo(() => {
        if (systemTools.length === 0) {
            return [] as Array<{ category: string; tools: SystemTool[] }>;
        }

        const query = systemToolSearch.trim().toLowerCase();
        const groups = new Map<string, SystemTool[]>();

        systemTools.forEach(tool => {
            const name = tool.name?.trim();
            if (!name) {
                return;
            }

            const category = tool.category?.trim() || systemToolCategoryFallback;
            const searchableText =
                `${name} ${category} ${tool.pluginName ?? ''} ${tool.resourceType ?? ''} ${tool.description ?? ''}`.toLowerCase();
            const isSelected = normalizedSelectedSystemTools.has(name.toLowerCase());

            if (query && !searchableText.includes(query) && !isSelected) {
                return;
            }

            const existing = groups.get(category);
            if (existing) {
                existing.push(tool);
            } else {
                groups.set(category, [tool]);
            }
        });

        return Array.from(groups.entries())
            .filter(([, toolsInGroup]) => toolsInGroup.length > 0)
            .sort((a, b) => a[0].localeCompare(b[0], undefined, { sensitivity: 'base' }))
            .map(([category, toolsInGroup]) => ({
                category,
                tools: toolsInGroup.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })),
            }));
    }, [systemToolSearch, systemTools, systemToolCategoryFallback, normalizedSelectedSystemTools]);

    const selectedSystemToolDetails = useMemo(() => {
        if (currentSystemTools.length === 0) {
            return [] as Array<{ name: string; category: string }>;
        }

        return currentSystemTools
            .map(name => name?.trim())
            .filter((name): name is string => !!name)
            .map(name => {
                const tool = systemToolLookup.get(name);
                if (!tool) {
                    return { name, category: systemToolCategoryFallback };
                }

                return {
                    name,
                    category: tool.category?.trim() || systemToolCategoryFallback,
                };
            });
    }, [currentSystemTools, systemToolLookup, systemToolCategoryFallback]);

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
            setPromptImprovement(result);
            setPromptImprovementMode('suggestions');
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
            setPromptImprovementMode('improvement');

            const updatedAgent: Partial<ExtendedAgent> = { ...agent };

            const improvedPrompt = result.improvedPrompt?.trim();
            if (improvedPrompt) {
                updatedAgent.instructions = improvedPrompt;
            }

            const improvedHandoff = result.handoffDescription?.trim();
            if (improvedHandoff) {
                updatedAgent.handoffDescription = improvedHandoff;
            }

            onChange(updatedAgent);
        } catch (error) {
            console.error('Failed to apply AI improvements:', error);
            setPromptImprovementError(getPromptErrorMessage(error));
        } finally {
            setIsApplyingImprovement(false);
        }
    };

    // Fetch MCP tools on component mount
    useEffect(() => {
        const fetchMcpTools = async () => {
            setIsFetchingMcpTools(true);
            setMcpToolsError(null);

            try {
                // Fetch tools grouped by connection for better UX
                const { getMcpToolsByConnection } = await import('../api/mcpConnectionsApi');
                const toolsByConn = await getMcpToolsByConnection(sreAgentEndpoint);
                setMcpToolsByConnection(toolsByConn);
            } catch (error) {
                console.error('Failed to fetch MCP tools:', error);
                setMcpToolsError(
                    error instanceof Error && error.message
                        ? error.message
                        : intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsLoadErrorFallback)
                );
            } finally {
                setIsFetchingMcpTools(false);
            }
        };

        fetchMcpTools();
    }, [sreAgentEndpoint, intl]);

    return (
        <div className={styles.formSection}>
            <Field
                label={
                    <div className={styles.fieldLabelRow}>
                        <span className={styles.fieldLabelText}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.agentName)}
                            <span className={styles.fieldRequiredStar} aria-hidden="true">
                                *
                            </span>
                        </span>
                        {showMetaAgentOverride && (
                            <div className={styles.fieldLabelMeta}>
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
                        )}
                    </div>
                }
                validationState={agentNameValidationState}
                validationMessage={agentNameValidationMessage}
            >
                <Input
                    disabled={!allowAgentNameEdit}
                    value={agent.name || ''}
                    onChange={handleAgentNameChange}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.agentNamePlaceholder)}
                    input={{ className: styles.textInput }}
                />
                <div className={styles.helpText}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.agentNameHelp, {
                        maxLength: ENTITY_NAME_MAX_LENGTH,
                    })}
                    {showMetaAgentOverride && (agent as any).metaAgentOverride && (
                        <div className={styles.metaAgentInfo}>
                            <Info16Regular aria-hidden className={styles.metaAgentInfoIcon} />
                            <span>
                                {intl.formatMessage(ExtendedAgentsGraphResources.metaAgentOverrideInfo, {
                                    agentName: metaAgentDisplayName,
                                })}
                            </span>
                        </div>
                    )}
                </div>
            </Field>

            <Field
                label={
                    <div className={styles.fieldLabelRow}>
                        <span className={styles.fieldLabelText}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.instructions)}
                            <span className={styles.fieldRequiredStar} aria-hidden="true">
                                *
                            </span>
                        </span>
                        <div className={styles.fieldActionGroup}>
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
                                    disabled={!agent.instructions?.trim() || isApplyingImprovement || isFetchingSuggestions}
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

                    {promptImprovement && promptImprovementMode === 'suggestions' && (
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

                            {promptImprovementMode === 'suggestions' && promptImprovement.handoffDescription?.trim() && (
                                <div className={styles.promptImprovementInlineGroup}>
                                    <Text className={styles.promptImprovementSectionTitle}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.handoffDescriptionSuggestionHeading)}
                                    </Text>
                                    <div className={styles.promptPreview}>{promptImprovement.handoffDescription}</div>
                                </div>
                            )}
                        </div>
                    )}

                    <Textarea
                        aria-required={true}
                        value={agent.instructions || ''}
                        onChange={handleAgentInstructionsChange}
                        placeholder={intl.formatMessage(ExtendedAgentsGraphResources.instructionsPlaceholder)}
                        rows={8}
                        textarea={{ className: styles.instructionsTextarea }}
                    />
                </div>
                <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.instructionsHelp)}</div>
            </Field>

            <Field
                label={
                    <div className={styles.fieldLabelRow}>
                        <span className={styles.fieldLabelText}>{intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsOptional)}</span>
                    </div>
                }
            >
                {isFetchingMcpTools ? (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '8px' }}>
                        <Spinner size="tiny" />
                        <Text size={200}>{intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsLoading)}</Text>
                    </div>
                ) : mcpToolsError ? (
                    <div style={{ padding: '8px' }}>
                        <Text size={200} style={{ color: '#c53030' }}>
                            {mcpToolsError}
                        </Text>
                    </div>
                ) : (
                    <div className={styles.systemToolPicker}>
                        <Combobox
                            multiselect
                            freeform
                            disabled={mcpToolsByConnection.length === 0}
                            placeholder={
                                mcpToolsByConnection.length === 0
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsPlaceholderEmpty)
                                    : intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsPlaceholder)
                            }
                            selectedOptions={currentMcpTools}
                            onOptionSelect={(_, data) => {
                                const selected = data.selectedOptions;
                                onChange({ ...agent, mcpTools: selected } as any);
                            }}
                        >
                            {mcpToolsByConnection.map(conn => {
                                const connectionLabel = conn.serviceType || conn.connectionName;
                                return (
                                    <OptionGroup
                                        key={conn.connectionId}
                                        label={
                                            <span className={styles.systemToolGroupHeader}>
                                                <span className={styles.systemToolGroupLabel}>
                                                    <span>{connectionLabel}</span>
                                                </span>
                                                <span className={styles.systemToolGroupCount}>{conn.tools.length}</span>
                                            </span>
                                        }
                                    >
                                        {conn.tools.map(tool => {
                                            // tool.name from API is already prefixed: {connectionId}_{toolName}
                                            // Extract the display name by removing the prefix
                                            const displayName = tool.name.startsWith(`${conn.connectionId}_`)
                                                ? tool.name.substring(`${conn.connectionId}_`.length)
                                                : tool.name;
                                            const truncatedDescription = tool.description
                                                ? tool.description.length > MAX_TOOL_DESCRIPTION_LENGTH
                                                    ? `${tool.description.slice(0, MAX_TOOL_DESCRIPTION_LENGTH)}…`
                                                    : tool.description
                                                : undefined;

                                            return (
                                                <Option key={tool.name} value={tool.name} text={`${displayName} (${connectionLabel})`}>
                                                    <div className={styles.systemToolOption}>
                                                        <div className={styles.systemToolOptionHeader}>
                                                            <span className={styles.systemToolOptionTitle}>{displayName}</span>
                                                            <span className={styles.systemToolCategoryPill}>{connectionLabel}</span>
                                                        </div>
                                                        {truncatedDescription && (
                                                            <span className={styles.systemToolOptionDescription} title={tool.description}>
                                                                {truncatedDescription}
                                                            </span>
                                                        )}
                                                    </div>
                                                </Option>
                                            );
                                        })}
                                    </OptionGroup>
                                );
                            })}
                        </Combobox>

                        {currentMcpTools.length > 0 && (
                            <Text className={styles.indicatorBadge} role="status">
                                {intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsSelectedCount, {
                                    count: currentMcpTools.length,
                                })}
                            </Text>
                        )}

                        {mcpToolsByConnection.length === 0 && (
                            <Text className={styles.systemToolEmpty}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsNoConnections)}
                            </Text>
                        )}

                        <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsHelpText)}</div>
                    </div>
                )}
            </Field>

            <Field
                label={
                    <div className={styles.fieldLabelRow}>
                        <span className={styles.fieldLabelText}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.handoffDescriptionLabel)}
                            <span className={styles.fieldRequiredStar} aria-hidden="true">
                                *
                            </span>
                        </span>
                    </div>
                }
            >
                <Textarea
                    value={agent.handoffDescription || ''}
                    onChange={(_, data) => onChange({ ...agent, handoffDescription: data.value })}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.handoffDescriptionPlaceholder)}
                    rows={2}
                    textarea={{ className: styles.handoffTextarea }}
                />
                <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.handoffDescriptionHelp)}</div>
            </Field>

            <Field
                label={
                    <div className={styles.fieldLabelRow}>
                        <span className={styles.fieldLabelText}>{intl.formatMessage(ExtendedAgentsGraphResources.toolsOptional)}</span>
                    </div>
                }
            >
                <div className={styles.systemToolPicker}>
                    <Dropdown
                        multiselect
                        disabled={existingTools.length === 0}
                        placeholder={
                            existingTools.length === 0
                                ? intl.formatMessage(ExtendedAgentsGraphResources.extendedToolsUnavailable)
                                : intl.formatMessage(ExtendedAgentsGraphResources.toolsPlaceholder)
                        }
                        selectedOptions={currentTools}
                        onOptionSelect={(_, data) => {
                            const selected = data.selectedOptions;
                            onChange({ ...agent, tools: selected });
                        }}
                    >
                        {existingTools.map(tool => {
                            const category = resolveExtendedToolCategory(tool);
                            const rawDescription = tool.description?.trim();
                            const truncatedDescription = rawDescription
                                ? rawDescription.length > MAX_TOOL_DESCRIPTION_LENGTH
                                    ? `${rawDescription.slice(0, MAX_TOOL_DESCRIPTION_LENGTH)}…`
                                    : rawDescription
                                : undefined;
                            const metadataParts = [tool.connector?.trim(), tool.type?.trim()].filter(
                                value => !!value && value !== category
                            ) as string[];

                            return (
                                <Option key={tool.name} value={tool.name} text={`${tool.name} (${category})`}>
                                    <div className={styles.systemToolOption}>
                                        <div className={styles.systemToolOptionHeader}>
                                            <span className={styles.systemToolOptionTitle}>{tool.name}</span>
                                            <span className={styles.systemToolCategoryPill}>{category}</span>
                                        </div>
                                        {truncatedDescription && (
                                            <span className={styles.systemToolOptionDescription} title={rawDescription}>
                                                {truncatedDescription}
                                            </span>
                                        )}
                                        {metadataParts.length > 0 && (
                                            <span className={styles.systemToolOptionMeta}>{metadataParts.join(' • ')}</span>
                                        )}
                                    </div>
                                </Option>
                            );
                        })}
                    </Dropdown>
                    {currentTools.length > 0 && (
                        <Text className={styles.indicatorBadge} role="status">
                            {intl.formatMessage(ExtendedAgentsGraphResources.toolsCountBadge, {
                                count: currentTools.length,
                            })}
                        </Text>
                    )}
                    <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.toolsHelp)}</div>
                </div>
            </Field>

            <Field
                label={
                    <div className={styles.fieldLabelRow}>
                        <span className={styles.fieldLabelText}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.systemToolsOptional)}
                        </span>
                    </div>
                }
            >
                <div className={styles.systemToolPicker}>
                    <Combobox
                        multiselect
                        freeform
                        disabled={systemTools.length === 0}
                        placeholder={
                            systemTools.length === 0
                                ? intl.formatMessage(ExtendedAgentsGraphResources.systemToolsUnavailable)
                                : intl.formatMessage(ExtendedAgentsGraphResources.systemToolsPlaceholder)
                        }
                        selectedOptions={currentSystemTools}
                        value={systemToolSearch}
                        onChange={handleSystemToolSearchChange}
                        onOptionSelect={(_, data) => {
                            const selected = data.selectedOptions;
                            onChange({ ...agent, systemTools: selected });
                        }}
                    >
                        {filteredSystemToolGroups.map(group => {
                            const userCollapsed = collapsedCategories[group.category];
                            const isCollapsed = hasActiveSystemToolSearch ? false : userCollapsed !== false;
                            const groupTools = isCollapsed ? [] : group.tools;

                            return (
                                <OptionGroup
                                    key={group.category}
                                    label={
                                        <span
                                            className={styles.systemToolGroupHeader}
                                            role="button"
                                            tabIndex={0}
                                            aria-expanded={!isCollapsed}
                                            onClick={event => handleCategoryClick(event, group.category)}
                                            onKeyDown={event => handleCategoryKeyDown(event, group.category)}
                                        >
                                            <span className={styles.systemToolGroupLabel}>
                                                {isCollapsed ? <ChevronRight12Regular /> : <ChevronDown12Regular />}
                                                <span>{group.category}</span>
                                            </span>
                                            <span className={styles.systemToolGroupCount}>{group.tools.length}</span>
                                        </span>
                                    }
                                >
                                    {groupTools.map(tool => {
                                        const category = tool.category?.trim() || systemToolCategoryFallback;
                                        const metadataParts = [tool.pluginName, tool.resourceType].filter(Boolean) as string[];
                                        const rawDescription = tool.description?.trim();
                                        const truncatedDescription = rawDescription
                                            ? rawDescription.length > MAX_TOOL_DESCRIPTION_LENGTH
                                                ? `${rawDescription.slice(0, MAX_TOOL_DESCRIPTION_LENGTH)}…`
                                                : rawDescription
                                            : undefined;

                                        return (
                                            <Option key={tool.name} value={tool.name} text={`${tool.name} (${category})`}>
                                                <div className={styles.systemToolOption}>
                                                    <div className={styles.systemToolOptionHeader}>
                                                        <span className={styles.systemToolOptionTitle}>{tool.name}</span>
                                                        <span className={styles.systemToolCategoryPill}>{category}</span>
                                                    </div>
                                                    {truncatedDescription && (
                                                        <span className={styles.systemToolOptionDescription} title={rawDescription}>
                                                            {truncatedDescription}
                                                        </span>
                                                    )}
                                                    {metadataParts.length > 0 && (
                                                        <span className={styles.systemToolOptionMeta}>{metadataParts.join(' • ')}</span>
                                                    )}
                                                </div>
                                            </Option>
                                        );
                                    })}
                                </OptionGroup>
                            );
                        })}
                    </Combobox>

                    {currentSystemTools.length > 0 && (
                        <Text className={styles.indicatorBadge} role="status">
                            {intl.formatMessage(ExtendedAgentsGraphResources.systemToolsCountBadge, {
                                count: currentSystemTools.length,
                            })}
                        </Text>
                    )}

                    {systemTools.length === 0 && (
                        <Text className={styles.systemToolEmpty}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.systemToolsUnavailable)}
                        </Text>
                    )}

                    {systemTools.length > 0 && filteredSystemToolGroups.length === 0 && (
                        <Text className={styles.systemToolEmpty}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.systemToolsSearchNoResults)}
                        </Text>
                    )}

                    {selectedSystemToolDetails.length > 0 && (
                        <div className={styles.chipsRow}>
                            {selectedSystemToolDetails.map(tool => (
                                <div key={tool.name} className={styles.pill}>
                                    <strong>{tool.name}</strong>
                                    <span>• {tool.category}</span>
                                </div>
                            ))}
                        </div>
                    )}

                    <div className={styles.helpText}>{intl.formatMessage(ExtendedAgentsGraphResources.systemToolsHelp)}</div>
                </div>
            </Field>

            <Field
                label={
                    <div className={styles.fieldLabelRow}>
                        <span className={styles.fieldLabelText}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipCurrentHandoffs)}
                        </span>
                    </div>
                }
            >
                <Dropdown
                    multiselect
                    disabled={availableHandoffs.length === 0}
                    placeholder={
                        availableHandoffs.length === 0
                            ? intl.formatMessage(ExtendedAgentsGraphResources.relationshipNoHandoffAgents)
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
