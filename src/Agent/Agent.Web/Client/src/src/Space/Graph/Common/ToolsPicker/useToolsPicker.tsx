import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { McpConnection } from '../../ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { PillSetItem } from '../PillSet';
import { ToolPickerOption, ToolPickerTypeFilter, ToolsPickerProps } from './ToolsPicker';

export interface UseToolsPickerProps {
    selectedToolNames: string[];
    setSelectedToolNames: (toolNames: string[]) => void;
    selectedMcpToolNames: string[];
    setSelectedMcpToolNames: (toolNames: string[]) => void;
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    mcpConnections?: McpConnection[];
    excludedToolNames?: string[];
    excludedMcpToolNames?: string[];
}

export interface UseToolsPickerReturn extends ToolsPickerProps {
    pillItems: PillSetItem[];
    onClearSelectedTools: () => void;
    onClearSearchAndExpandedGroups: () => void;
}

export const useToolsPicker = (props: UseToolsPickerProps): UseToolsPickerReturn => {
    const intl = useIntl();
    const {
        selectedToolNames,
        setSelectedToolNames,
        selectedMcpToolNames,
        setSelectedMcpToolNames,
        existingTools,
        systemTools,
        mcpConnections,
        excludedToolNames,
        excludedMcpToolNames,
    } = props;

    const excludedToolKeys = useMemo(
        () => new Set((excludedToolNames ?? []).map(name => `${ToolTypePrefix.TOOL}${normalizeName(name)}`).filter(Boolean)),
        [excludedToolNames]
    );
    const excludedMcpToolKeys = useMemo(
        () => new Set((excludedMcpToolNames ?? []).map(name => `${ToolTypePrefix.MCP}${normalizeName(name)}`).filter(Boolean)),
        [excludedMcpToolNames]
    );

    const [toolType, setToolType] = useState<ToolPickerTypeFilter>('all');
    const toolTypeOptions: ToolsPickerProps['toolTypeOptions'] = useMemo(
        () => [
            { key: 'all' as ToolPickerTypeFilter, label: intl.formatMessage(SreAgentResources.all) },
            { key: 'custom' as ToolPickerTypeFilter, label: intl.formatMessage(ExtendedAgentsGraphResources.customTool) },
            { key: 'mcp' as ToolPickerTypeFilter, label: intl.formatMessage(ExtendedAgentsGraphResources.mcpTool) },
        ],
        [intl]
    );

    const [searchQuery, setSearchQuery] = useState<string>('');

    const [expandedGroupNames, setExpandedGroupNames] = useState<string[]>([]);
    const onGroupExpandedChange = useCallback(
        (groupName: string, expanded: boolean) => {
            setExpandedGroupNames(prev => {
                if (expanded) {
                    return [...prev, groupName];
                }
                return prev.filter(name => name !== groupName);
            });
        },
        [setExpandedGroupNames]
    );

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

    // Helper to get tools in a specific group from filtered options
    const getToolsInGroup = useCallback((groupName: string, groups: { category: string; tools: ToolPickerOption[] }[]) => {
        const group = groups.find(g => g.category === groupName);
        return group?.tools ?? [];
    }, []);

    const availableToolOptions = useMemo(() => {
        const addedToolsNormalized = new Set<string>();
        const options: ToolPickerOption[] = [];

        mcpConnections?.forEach(connection => {
            connection.tools?.forEach(mcpTool => {
                const name = normalizeName(mcpTool.name);
                const key = `${ToolTypePrefix.MCP}${name}`;
                if (!name || excludedMcpToolKeys.has(key) || addedToolsNormalized.has(key)) {
                    return;
                }
                addedToolsNormalized.add(key);

                options.push({
                    key,
                    name,
                    description: mcpTool.description,
                    groupLabel: connection.name,
                    categoryLabel: connection.name,
                    kind: 'mcp',
                    searchText: `${mcpTool.name} ${connection.name} ${mcpTool.description ?? ''}`.toLowerCase(),
                });
            });
        });

        systemTools?.forEach(systemTool => {
            const name = normalizeName(systemTool.name);
            const key = `${ToolTypePrefix.TOOL}${name}`;
            if (!name || excludedToolKeys.has(key) || addedToolsNormalized.has(key)) {
                return;
            }
            addedToolsNormalized.add(key);

            const category = systemTool.category || intl.formatMessage(ExtendedAgentsGraphResources.relationshipToolCategoryFallback);
            const pluginName = systemTool.pluginName ?? '';
            const resourceType = systemTool.resourceType ?? '';
            const description = systemTool.description ?? '';
            const searchText = `${name} ${category} ${pluginName} ${resourceType} ${description}`.toLowerCase();

            options.push({
                key,
                name,
                description: systemTool.description,
                groupLabel: category,
                categoryLabel: category,
                kind: 'system',
                pluginName: systemTool.pluginName,
                resourceType: systemTool.resourceType,
                searchText,
            });
        });

        existingTools?.forEach(tool => {
            const name = normalizeName(tool.name);
            const key = `${ToolTypePrefix.TOOL}${name}`;
            if (!name || excludedToolKeys.has(key) || addedToolsNormalized.has(key)) {
                return;
            }
            addedToolsNormalized.add(key);

            const category = getExtendedToolCategory(tool);
            const description = tool.description ?? '';
            const metadataCategory = tool.metadata?.category ?? '';
            const searchText = `${name} ${category} ${metadataCategory} ${description} ${tool.type ?? ''}`.toLowerCase();

            options.push({
                key,
                name,
                description: tool.description,
                connector: tool.connector,
                groupLabel: category,
                categoryLabel: category,
                kind: ToolType.TOOL,
                searchText,
            });
        });

        return options;
    }, [excludedMcpToolKeys, excludedToolKeys, existingTools, systemTools, mcpConnections, getExtendedToolCategory, intl]);

    const onSelectedToolChange = useCallback(
        (key: string, isSelected: boolean) => {
            const { toolKind, toolName } = key.startsWith(ToolTypePrefix.MCP)
                ? { toolKind: ToolType.MCP, toolName: key.slice(ToolTypePrefix.MCP.length) }
                : key.startsWith(ToolTypePrefix.TOOL)
                  ? { toolKind: ToolType.TOOL, toolName: key.slice(ToolTypePrefix.TOOL.length) }
                  : { toolKind: undefined, toolName: undefined };

            if (!toolKind || !toolName) {
                return;
            }
            const { values, setValues } =
                toolKind === ToolType.MCP
                    ? { values: selectedMcpToolNames, setValues: setSelectedMcpToolNames }
                    : { values: selectedToolNames, setValues: setSelectedToolNames };

            if (isSelected) {
                setValues([...values, toolName]);
            } else {
                setValues(values.filter(name => name !== toolName));
            }
        },
        [setSelectedToolNames, selectedToolNames, setSelectedMcpToolNames, selectedMcpToolNames]
    );

    const filteredToolOptions = useMemo(() => {
        const query = searchQuery.trim().toLowerCase();
        if (!query) {
            return availableToolOptions;
        }
        const matches = availableToolOptions.filter(
            option => option.name.toLowerCase().includes(query) || option.searchText.toLowerCase().includes(query)
        );

        return matches;
    }, [availableToolOptions, searchQuery]);

    const { mcpGroups, systemGroups, customGroups } = useMemo(() => {
        const filteredMcpTools: ToolPickerOption[] = [];
        const filteredSystemTools: ToolPickerOption[] = [];
        const filteredCustomTools: ToolPickerOption[] = [];

        filteredToolOptions.forEach(option => {
            if (option.kind === 'mcp') {
                filteredMcpTools.push(option);
            } else if (option.kind === 'system') {
                filteredSystemTools.push(option);
            } else {
                filteredCustomTools.push(option);
            }
        });
        const mcpGroups = getGroups(filteredMcpTools);
        const systemGroups = getGroups(filteredSystemTools);
        const customGroups = getGroups(filteredCustomTools);

        return { mcpGroups, systemGroups, customGroups };
    }, [filteredToolOptions]);

    const groups = useMemo(() => {
        if (toolType === 'custom') {
            return customGroups;
        }
        if (toolType === 'mcp') {
            return mcpGroups;
        }
        return [...customGroups, ...mcpGroups, ...systemGroups];
    }, [toolType, customGroups, mcpGroups, systemGroups]);

    const pillItems = useMemo(() => {
        const nonMcpItems = selectedToolNames?.map(name => ({ key: `${ToolTypePrefix.TOOL}${normalizeName(name)}`, label: name })) ?? [];
        const mcpItems = selectedMcpToolNames?.map(name => ({ key: `${ToolTypePrefix.MCP}${normalizeName(name)}`, label: name })) ?? [];
        return [...nonMcpItems, ...mcpItems];
    }, [selectedToolNames, selectedMcpToolNames]);

    const selectedToolKeys = useMemo(() => {
        const nonMcpKeys = selectedToolNames?.map(name => `${ToolTypePrefix.TOOL}${normalizeName(name)}`) ?? [];
        const mcpKeys = selectedMcpToolNames?.map(name => `${ToolTypePrefix.MCP}${normalizeName(name)}`) ?? [];
        return [...nonMcpKeys, ...mcpKeys];
    }, [selectedToolNames, selectedMcpToolNames]);

    const onClearSelectedTools = useCallback(() => {
        setSelectedToolNames([]);
        setSelectedMcpToolNames([]);
    }, []);

    const onClearSearchAndExpandedGroups = useCallback(() => {
        setSearchQuery('');
        setExpandedGroupNames([]);
    }, []);

    // Select/deselect all tools in a specific group
    const onSelectAllToolsInGroup = useCallback(
        (groupName: string, isSelected: boolean) => {
            const toolsInGroup = getToolsInGroup(groupName, groups);
            if (isSelected) {
                // Add all tools in the group that are not already selected
                const newSelectedTools = [...selectedToolNames];
                const newSelectedMcpTools = [...selectedMcpToolNames];
                toolsInGroup.forEach(tool => {
                    if (tool.kind === 'mcp') {
                        if (!newSelectedMcpTools.includes(tool.name)) {
                            newSelectedMcpTools.push(tool.name);
                        }
                    } else {
                        if (!newSelectedTools.includes(tool.name)) {
                            newSelectedTools.push(tool.name);
                        }
                    }
                });
                setSelectedToolNames(newSelectedTools);
                setSelectedMcpToolNames(newSelectedMcpTools);
            } else {
                // Remove all tools in the group from selection
                const nonMcpToolsInGroup = toolsInGroup.filter(tool => tool.kind !== 'mcp');
                const mcpToolsInGroup = toolsInGroup.filter(tool => tool.kind === 'mcp');

                setSelectedToolNames(selectedToolNames.filter(name => !nonMcpToolsInGroup.map(tool => tool.name).includes(name)));
                setSelectedMcpToolNames(selectedMcpToolNames.filter(name => !mcpToolsInGroup.map(tool => tool.name).includes(name)));
            }
        },
        [selectedToolNames, setSelectedToolNames, selectedMcpToolNames, setSelectedMcpToolNames, groups, getToolsInGroup]
    );

    // Select/deselect all tools across all groups
    const onSelectAllTools = useCallback(
        (isSelected: boolean) => {
            const allTools = groups.flatMap(group => group.tools);
            if (isSelected) {
                // Select all tools from all groups
                const newSelectedTools = [...selectedToolNames];
                const newSelectedMcpTools = [...selectedMcpToolNames];
                allTools.forEach(tool => {
                    if (tool.kind === 'mcp') {
                        if (!newSelectedMcpTools.includes(tool.name)) {
                            newSelectedMcpTools.push(tool.name);
                        }
                    } else {
                        if (!newSelectedTools.includes(tool.name)) {
                            newSelectedTools.push(tool.name);
                        }
                    }
                });
                setSelectedToolNames(newSelectedTools);
                setSelectedMcpToolNames(newSelectedMcpTools);
            } else {
                // Deselect all tools from all groups
                const allNonMcpTools = allTools.filter(tool => tool.kind !== 'mcp');
                const allMcpTools = allTools.filter(tool => tool.kind === 'mcp');
                setSelectedToolNames(selectedToolNames.filter(name => !allNonMcpTools.map(tool => tool.name).includes(name)));
                setSelectedMcpToolNames(selectedMcpToolNames.filter(name => !allMcpTools.map(tool => tool.name).includes(name)));
            }
        },
        [selectedToolNames, setSelectedToolNames, selectedMcpToolNames, setSelectedMcpToolNames, groups]
    );

    return {
        toolTypeOptions,
        toolType,
        onToolTypeChange: setToolType,
        expandedGroupNames,
        onGroupExpandedChange,
        selectedToolKeys,
        onSelectedToolChange,
        onSelectAllToolsInGroup,
        onSelectAllTools,
        onClearSelectedTools,
        onClearSearchAndExpandedGroups,
        searchQuery,
        setSearchQuery,
        groups,
        pillItems,
    };
};

enum ToolTypePrefix {
    TOOL = 'tool_',
    MCP = 'mcp_',
}

enum ToolType {
    TOOL = 'tool',
    MCP = 'mcp',
}

const normalizeName = (name?: string | null) => (name ?? '').trim();

const getGroups = (tools: ToolPickerOption[]) => {
    const groups = new Map<string, ToolPickerOption[]>();

    tools.forEach(option => {
        const existing = groups.get(option.groupLabel);
        if (existing) {
            existing.push(option);
        } else {
            groups.set(option.groupLabel, [option]);
        }
    });

    return Array.from(groups.entries())
        .map(([category, tools]) => ({
            category,
            tools: tools.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })),
        }))
        .filter(group => group.tools.length > 0)
        .sort((a, b) => a.category.localeCompare(b.category, undefined, { sensitivity: 'base' }));
};
