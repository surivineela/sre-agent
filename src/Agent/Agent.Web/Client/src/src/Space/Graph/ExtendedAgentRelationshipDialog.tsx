import type { ComboboxOpenChangeData, ComboboxOpenEvents, OptionOnSelectData, SelectionEvents } from '@fluentui/react-combobox';
import {
    Button,
    Combobox,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Dropdown,
    Field,
    MessageBar,
    MessageBarBody,
    Option,
    OptionGroup,
    Text,
    Tooltip,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { ChangeEvent, FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../Contracts/ExtendedAgentGraph';

type OperationResult = {
    success: boolean;
    message: string;
};

const TOOL_DESCRIPTION_PREVIEW_LENGTH = 80;

const getToolDescriptionParts = (description?: string): { preview: string; full: string; isTruncated: boolean } | null => {
    if (!description) {
        return null;
    }

    const normalized = description.replace(/\s+/g, ' ').trim();
    if (!normalized) {
        return null;
    }

    if (normalized.length <= TOOL_DESCRIPTION_PREVIEW_LENGTH) {
        return { preview: normalized, full: normalized, isTruncated: false };
    }

    const preview = `${normalized.slice(0, TOOL_DESCRIPTION_PREVIEW_LENGTH - 1).trimEnd()}…`;
    return { preview, full: normalized, isTruncated: true };
};

const useRelationshipDialogStyles = makeStyles({
    surface: {
        maxWidth: '720px',
        width: '90vw',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    row: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
        alignItems: 'flex-end',
    },
    field: {
        flex: 1,
        minWidth: '220px',
    },
    formGrid: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        padding: tokens.spacingHorizontalM,
    },
    formHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    actionsRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
    },
    messageStack: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    dropdownFilterContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    dropdownOption: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-start',
        gap: tokens.spacingVerticalXXS,
    },
    dropdownOptionMeta: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
    },
});

interface ExtendedAgentRelationshipDialogProps {
    open: boolean;
    agent?: ExtendedAgent;
    onOpenChange: (open: boolean) => void;
    existingAgents: ExtendedAgent[];
    existingTools: ExtendedTool[];
    systemTools: SystemTool[];
    onAddHandoff: (handoffAgentName: string) => Promise<OperationResult>;
    onAddTool: (toolName: string) => Promise<OperationResult>;
    onLaunchCreateEntity?: (type: 'agent' | 'tool', sourceAgentName: string) => void;
    initialAction?: 'handoff' | 'tool';
}

export const ExtendedAgentRelationshipDialog: FC<ExtendedAgentRelationshipDialogProps> = ({
    open,
    agent,
    onOpenChange,
    existingAgents,
    existingTools,
    systemTools,
    onAddHandoff,
    onAddTool,
    onLaunchCreateEntity,
    initialAction,
}) => {
    const styles = useRelationshipDialogStyles();
    const intl = useIntl();

    const [selectedHandoff, setSelectedHandoff] = useState<string>();
    const [selectedTool, setSelectedTool] = useState<string>();
    const [status, setStatus] = useState<{ intent: 'success' | 'error' | 'info'; message: string }>();
    const [busy, setBusy] = useState({ handoff: false, tool: false });
    const [toolSearch, setToolSearch] = useState('');

    type ToolPickerOption = {
        name: string;
        description?: string;
        connector?: string;
        groupLabel: string;
        categoryLabel: string;
        kind: 'tool' | 'system';
        pluginName?: string;
        resourceType?: string;
        searchText: string;
    };

    const handleToolSearchChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
        setToolSearch(event.target.value);
    }, []);

    const handleToolOptionSelect = useCallback((_event: SelectionEvents, data: OptionOnSelectData) => {
        const optionValue = data.optionValue ?? data.selectedOptions[data.selectedOptions.length - 1];
        setSelectedTool(optionValue);
        setToolSearch(optionValue ?? '');
    }, []);

    const handleToolOpenChange = useCallback(
        (_event: ComboboxOpenEvents, data: ComboboxOpenChangeData) => {
            if (!data.open) {
                setToolSearch(selectedTool ?? '');
            }
        },
        [selectedTool]
    );

    const showCreationSection = !initialAction;

    useEffect(() => {
        if (!open) {
            return;
        }

        setSelectedHandoff(undefined);
        setSelectedTool(undefined);
        setStatus(undefined);
        setBusy({ handoff: false, tool: false });
        setToolSearch('');
    }, [open, agent?.name]);

    useEffect(() => {
        if (!open || !initialAction || status) {
            return;
        }

        const message =
            initialAction === 'handoff'
                ? intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickActionAddHandoffInfo)
                : intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickActionAddToolInfo);
        setStatus({ intent: 'info', message });
    }, [open, initialAction, intl, status]);

    const availableHandoffs = useMemo(() => {
        if (!agent) {
            return [] as string[];
        }

        const current = new Set(agent.handoffs ?? []);
        return existingAgents
            .map(existing => existing.name)
            .filter((name): name is string => !!name && name !== agent.name && !current.has(name));
    }, [agent, existingAgents]);

    const getExtendedToolCategory = useCallback(
        (tool: ExtendedTool) => {
            const metadataCategory = tool.metadata?.category;
            if (metadataCategory && typeof metadataCategory === 'string') {
                return metadataCategory;
            }

            const attributeCategory = tool.attributes?.find(attribute => attribute?.toLowerCase().startsWith('category:'));
            if (attributeCategory) {
                const value = attributeCategory.split(':')[1];
                if (value) {
                    return value.trim();
                }
            }

            return tool.type || intl.formatMessage(ExtendedAgentsGraphResources.relationshipToolCategoryFallback);
        },
        [intl]
    );

    const availableToolOptions = useMemo(() => {
        if (!agent) {
            return [] as ToolPickerOption[];
        }

        const normalize = (value?: string | null) => (value ?? '').trim();
        const currentTools = new Set((agent.tools ?? []).map(normalize).filter(Boolean));
        const currentSystemTools = new Set((agent.systemTools ?? []).map(normalize).filter(Boolean));
        const systemGroupLabel = intl.formatMessage(ExtendedAgentsGraphResources.systemToolsSectionTitle);

        const options: ToolPickerOption[] = [];

        existingTools.forEach(tool => {
            const name = normalize(tool.name);
            if (!name || currentTools.has(name) || currentSystemTools.has(name)) {
                return;
            }

            const category = getExtendedToolCategory(tool);
            const description = tool.description ?? '';
            const metadataCategory = tool.metadata?.category ?? '';
            const searchText = `${name} ${category} ${metadataCategory} ${description} ${tool.type ?? ''}`.toLowerCase();

            options.push({
                name,
                description: tool.description,
                connector: tool.connector,
                groupLabel: category,
                categoryLabel: category,
                kind: 'tool',
                searchText,
            });
        });

        systemTools.forEach(systemTool => {
            const name = normalize(systemTool.name);
            if (!name || currentSystemTools.has(name) || currentTools.has(name)) {
                return;
            }

            const category = systemTool.category || intl.formatMessage(ExtendedAgentsGraphResources.relationshipToolCategoryFallback);
            const pluginName = systemTool.pluginName ?? '';
            const resourceType = systemTool.resourceType ?? '';
            const description = systemTool.description ?? '';
            const searchText = `${name} ${category} ${pluginName} ${resourceType} ${description}`.toLowerCase();

            options.push({
                name,
                description: systemTool.description,
                groupLabel: systemGroupLabel,
                categoryLabel: category,
                kind: 'system',
                pluginName: systemTool.pluginName,
                resourceType: systemTool.resourceType,
                searchText,
            });
        });

        return options;
    }, [agent, existingTools, systemTools, getExtendedToolCategory, intl]);

    const filteredTools = useMemo(() => {
        const query = toolSearch.trim().toLowerCase();
        const selectedName = (selectedTool ?? '').trim().toLowerCase();
        const isFiltering = !!query && query !== selectedName;

        if (!isFiltering) {
            return availableToolOptions;
        }

        const matches = availableToolOptions.filter(option => option.searchText.includes(query));

        if (selectedTool) {
            const selectedOption = availableToolOptions.find(option => option.name === selectedTool);
            if (selectedOption && !matches.some(option => option.name === selectedOption.name)) {
                return [selectedOption, ...matches];
            }
        }

        return matches;
    }, [availableToolOptions, selectedTool, toolSearch]);

    const groupedTools = useMemo(() => {
        const groups = new Map<string, ToolPickerOption[]>();

        filteredTools.forEach(option => {
            const existing = groups.get(option.groupLabel);
            if (existing) {
                existing.push(option);
            } else {
                groups.set(option.groupLabel, [option]);
            }
        });

        return Array.from(groups.entries())
            .sort((a, b) => a[0].localeCompare(b[0], undefined, { sensitivity: 'base' }))
            .map(([category, tools]) => ({
                category,
                tools: tools.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })),
            }));
    }, [filteredTools]);

    const hasHandoffOptions = availableHandoffs.length > 0;
    const hasToolOptions = availableToolOptions.length > 0;
    const showHandoffSection = !initialAction || initialAction === 'handoff';
    const showToolSection = !initialAction || initialAction === 'tool';
    const hasExistingSection = showHandoffSection || showToolSection;

    const notify = useCallback((intent: 'success' | 'error' | 'info', message: string) => {
        setStatus({ intent, message });
    }, []);

    const handleAddHandoff = useCallback(async () => {
        if (!selectedHandoff) {
            return;
        }

        setBusy(prev => ({ ...prev, handoff: true }));
        try {
            const result = await onAddHandoff(selectedHandoff);
            notify(result.success ? 'success' : 'error', result.message);
            if (result.success) {
                setSelectedHandoff(undefined);
            }
        } catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            notify('error', message);
        } finally {
            setBusy(prev => ({ ...prev, handoff: false }));
        }
    }, [notify, onAddHandoff, selectedHandoff]);

    const handleAddTool = useCallback(async () => {
        if (!selectedTool) {
            return;
        }

        setBusy(prev => ({ ...prev, tool: true }));
        try {
            const result = await onAddTool(selectedTool);
            notify(result.success ? 'success' : 'error', result.message);
            if (result.success) {
                setSelectedTool(undefined);
                setToolSearch('');
            }
        } catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            notify('error', message);
        } finally {
            setBusy(prev => ({ ...prev, tool: false }));
        }
    }, [notify, onAddTool, selectedTool]);

    return (
        <Dialog
            open={open}
            onOpenChange={(_, data) => {
                onOpenChange(data.open);
            }}
        >
            <DialogSurface className={styles.surface}>
                <DialogBody>
                    <DialogTitle>
                        {agent
                            ? intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickDialogTitle, {
                                  name: agent.name,
                              })
                            : intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickDialogTitleFallback)}
                    </DialogTitle>
                    <DialogContent className={styles.content}>
                        <div className={styles.messageStack}>
                            <Text>{intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickDialogDescription)}</Text>
                            <MessageBar intent="info">
                                <MessageBarBody>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickDelayNotice)}
                                </MessageBarBody>
                            </MessageBar>
                            {status && (
                                <MessageBar intent={status.intent}>
                                    <MessageBarBody>{status.message}</MessageBarBody>
                                </MessageBar>
                            )}
                        </div>

                        {!agent ? (
                            <MessageBar intent="warning">
                                <MessageBarBody>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickNoAgentSelected)}
                                </MessageBarBody>
                            </MessageBar>
                        ) : (
                            <>
                                {hasExistingSection && (
                                    <div className={styles.section}>
                                        <Text weight="semibold">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickExistingTitle)}
                                        </Text>
                                        {showHandoffSection && (
                                            <div className={styles.row}>
                                                <Field
                                                    label={intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddHandoffLabel)}
                                                    className={styles.field}
                                                >
                                                    <Dropdown
                                                        placeholder={intl.formatMessage(
                                                            ExtendedAgentsGraphResources.relationshipSelectAgent
                                                        )}
                                                        selectedOptions={selectedHandoff ? [selectedHandoff] : []}
                                                        onOptionSelect={(_, data) => setSelectedHandoff(data.optionValue as string)}
                                                        disabled={!hasHandoffOptions}
                                                    >
                                                        {availableHandoffs.map(name => (
                                                            <Option key={name} value={name}>
                                                                {name}
                                                            </Option>
                                                        ))}
                                                    </Dropdown>
                                                    {!hasHandoffOptions && (
                                                        <Text size={200} className={styles.dropdownOptionMeta}>
                                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipNoHandoffs)}
                                                        </Text>
                                                    )}
                                                </Field>
                                                <Button
                                                    appearance="primary"
                                                    onClick={handleAddHandoff}
                                                    disabled={!selectedHandoff || busy.handoff}
                                                >
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddButton)}
                                                </Button>
                                            </div>
                                        )}
                                        {showToolSection && (
                                            <div className={styles.row}>
                                                <Field
                                                    label={intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddToolLabel)}
                                                    className={styles.field}
                                                >
                                                    <div className={styles.dropdownFilterContainer}>
                                                        <Combobox
                                                            placeholder={intl.formatMessage(
                                                                ExtendedAgentsGraphResources.relationshipToolSearchPlaceholder
                                                            )}
                                                            aria-label={intl.formatMessage(
                                                                ExtendedAgentsGraphResources.relationshipAddToolLabel
                                                            )}
                                                            selectedOptions={selectedTool ? [selectedTool] : []}
                                                            value={toolSearch}
                                                            onChange={handleToolSearchChange}
                                                            onOptionSelect={handleToolOptionSelect}
                                                            onOpenChange={handleToolOpenChange}
                                                            disabled={!hasToolOptions}
                                                        >
                                                            {groupedTools.map(group => (
                                                                <OptionGroup key={group.category} label={group.category}>
                                                                    {group.tools.map(option => {
                                                                        const descriptionParts = getToolDescriptionParts(
                                                                            option.description
                                                                        );
                                                                        return (
                                                                            <Option
                                                                                key={option.name}
                                                                                value={option.name}
                                                                                text={`${option.name} (${option.categoryLabel})`}
                                                                            >
                                                                                <div className={styles.dropdownOption}>
                                                                                    <Text weight="semibold">{option.name}</Text>
                                                                                    <Text size={200} className={styles.dropdownOptionMeta}>
                                                                                        {intl.formatMessage(
                                                                                            ExtendedAgentsGraphResources.relationshipToolCategoryLabel,
                                                                                            { category: option.categoryLabel }
                                                                                        )}
                                                                                    </Text>
                                                                                    {option.kind === 'system' && option.pluginName && (
                                                                                        <Text
                                                                                            size={200}
                                                                                            className={styles.dropdownOptionMeta}
                                                                                        >
                                                                                            {intl.formatMessage(
                                                                                                ExtendedAgentsGraphResources.systemToolPluginLabel
                                                                                            )}
                                                                                            : {option.pluginName}
                                                                                        </Text>
                                                                                    )}
                                                                                    {option.kind === 'system' && option.resourceType && (
                                                                                        <Text
                                                                                            size={200}
                                                                                            className={styles.dropdownOptionMeta}
                                                                                        >
                                                                                            {intl.formatMessage(
                                                                                                SreAgentResources.resourceType
                                                                                            )}
                                                                                            : {option.resourceType}
                                                                                        </Text>
                                                                                    )}
                                                                                    {descriptionParts && (
                                                                                        <Tooltip
                                                                                            content={descriptionParts.full}
                                                                                            relationship="description"
                                                                                        >
                                                                                            <Text
                                                                                                size={200}
                                                                                                className={styles.dropdownOptionMeta}
                                                                                            >
                                                                                                {descriptionParts.preview}
                                                                                            </Text>
                                                                                        </Tooltip>
                                                                                    )}
                                                                                </div>
                                                                            </Option>
                                                                        );
                                                                    })}
                                                                </OptionGroup>
                                                            ))}
                                                            {groupedTools.length === 0 && (
                                                                <Option value="__no_results" disabled>
                                                                    {intl.formatMessage(
                                                                        ExtendedAgentsGraphResources.relationshipToolSearchEmpty
                                                                    )}
                                                                </Option>
                                                            )}
                                                        </Combobox>
                                                        {(filteredTools.length === 0 || !hasToolOptions) && (
                                                            <Text size={200} className={styles.dropdownOptionMeta}>
                                                                {intl.formatMessage(
                                                                    ExtendedAgentsGraphResources.relationshipToolSearchEmpty
                                                                )}
                                                            </Text>
                                                        )}
                                                    </div>
                                                </Field>
                                                <Button appearance="primary" onClick={handleAddTool} disabled={!selectedTool || busy.tool}>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipAddButton)}
                                                </Button>
                                            </div>
                                        )}
                                    </div>
                                )}

                                {showCreationSection && onLaunchCreateEntity && agent && (
                                    <div className={styles.section}>
                                        <Text weight="semibold">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateTitle)}
                                        </Text>
                                        <Text>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateAgentReminder, {
                                                agentName: agent.name,
                                            })}
                                        </Text>
                                        <Text>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipContextToolSubtext, {
                                                agentName: agent.name,
                                            })}
                                        </Text>
                                        <div className={styles.actionsRow}>
                                            <Button appearance="secondary" onClick={() => onLaunchCreateEntity('agent', agent.name)}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateAgentHeader)}
                                            </Button>
                                            <Button appearance="secondary" onClick={() => onLaunchCreateEntity('tool', agent.name)}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateToolHeader)}
                                            </Button>
                                        </div>
                                    </div>
                                )}
                            </>
                        )}
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={() => onOpenChange(false)}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.yamlCloseButton)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
