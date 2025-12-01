import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { McpConnection } from '../../ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { ToolPickerOption, ToolsPickerProps } from './ToolsPicker';

export interface UseToolsPickerProps {
    selectedToolNames: string[];
    setSelectedToolNames: (toolNames: string[]) => void;
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    mcpConnections?: McpConnection[];
    excludedToolNames?: string[];
}

export interface UseToolsPickerReturn extends ToolsPickerProps {
    pillItems: { key: string; label: string }[];
    onClearSelectedTools: () => void;
    onClearSearchAndExpandedGroups: () => void;
}

export const useToolsPicker = (props: UseToolsPickerProps): UseToolsPickerReturn => {
    const { selectedToolNames, setSelectedToolNames, existingTools, systemTools, mcpConnections, excludedToolNames } = props;
    const intl = useIntl();

    const [toolType, setToolType] = useState<'mcp' | 'all'>('all');
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

    const onSelectedToolChange = useCallback(
        (toolName: string, isSelected: boolean) => {
            if (isSelected) {
                setSelectedToolNames([...selectedToolNames, toolName]);
            } else {
                setSelectedToolNames(selectedToolNames.filter(name => name !== toolName));
            }
        },
        [setSelectedToolNames, selectedToolNames]
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

    // Helper to get tool names in a specific group from filtered options
    const getToolNamesInGroup = useCallback((groupName: string, groups: { category: string; tools: { name: string }[] }[]) => {
        const group = groups.find(g => g.category === groupName);
        return group?.tools.map(t => t.name) ?? [];
    }, []);

    const availableToolOptions = useMemo(() => {
        const normalize = (value?: string | null) => (value ?? '').trim();
        const currentToolsNormalized = new Set((excludedToolNames ?? []).map(normalize).filter(Boolean));

        const addedToolsNormalized = new Set<string>();
        const options: ToolPickerOption[] = [];

        mcpConnections?.forEach(connection => {
            connection.tools?.forEach(mcpTool => {
                const name = normalize(mcpTool.name);
                if (!name || currentToolsNormalized.has(name)) {
                    return;
                }
                options.push({
                    name: normalize(mcpTool.name),
                    description: mcpTool.description,
                    groupLabel: connection.name,
                    categoryLabel: connection.name,
                    kind: 'mcp',
                    searchText: `${mcpTool.name} ${connection.name} ${mcpTool.description ?? ''}`.toLowerCase(),
                });
                addedToolsNormalized.add(name);
            });
        });

        systemTools?.forEach(systemTool => {
            const name = normalize(systemTool.name);
            if (!name || currentToolsNormalized.has(name) || addedToolsNormalized.has(name)) {
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
                groupLabel: category,
                categoryLabel: category,
                kind: 'system',
                pluginName: systemTool.pluginName,
                resourceType: systemTool.resourceType,
                searchText,
            });
        });

        existingTools?.forEach(tool => {
            const name = normalize(tool.name);
            if (!name || currentToolsNormalized.has(name) || addedToolsNormalized.has(name)) {
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

        return options;
    }, [excludedToolNames, existingTools, systemTools, mcpConnections, getExtendedToolCategory, intl]);

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

    const mcpGroups = useMemo(() => {
        const filteredMcpTools = filteredToolOptions.filter(option => option.kind === 'mcp');
        const groups = getGroups(filteredMcpTools);
        return [...groups];
    }, [filteredToolOptions]);

    const nonMcpGroups = useMemo(() => {
        const filteredMcpTools = filteredToolOptions.filter(option => option.kind !== 'mcp');
        const groups = getGroups(filteredMcpTools);
        return [...groups];
    }, [filteredToolOptions]);

    const groups = useMemo(() => {
        if (toolType === 'mcp') {
            return mcpGroups;
        }
        return [...mcpGroups, ...nonMcpGroups];
    }, [toolType, mcpGroups, nonMcpGroups]);

    const pillItems = useMemo(() => {
        return selectedToolNames.map(name => ({ key: name, label: name }));
    }, [selectedToolNames]);

    const onClearSelectedTools = useCallback(() => {
        setSelectedToolNames([]);
    }, []);

    const onClearSearchAndExpandedGroups = useCallback(() => {
        setSearchQuery('');
        setExpandedGroupNames([]);
    }, []);

    // Select/deselect all tools in a specific group
    const onSelectAllToolsInGroup = useCallback(
        (groupName: string, isSelected: boolean) => {
            const toolNamesInGroup = getToolNamesInGroup(groupName, groups);
            if (isSelected) {
                // Add all tools in the group that are not already selected
                const newSelectedTools = [...selectedToolNames];
                toolNamesInGroup.forEach(name => {
                    if (!newSelectedTools.includes(name)) {
                        newSelectedTools.push(name);
                    }
                });
                setSelectedToolNames(newSelectedTools);
            } else {
                // Remove all tools in the group from selection
                setSelectedToolNames(selectedToolNames.filter(name => !toolNamesInGroup.includes(name)));
            }
        },
        [selectedToolNames, setSelectedToolNames, groups, getToolNamesInGroup]
    );

    // Select/deselect all tools across all groups
    const onSelectAllTools = useCallback(
        (isSelected: boolean) => {
            if (isSelected) {
                // Select all tools from all groups
                const allToolNames = groups.flatMap(group => group.tools.map(tool => tool.name));
                const newSelectedTools = [...selectedToolNames];
                allToolNames.forEach(name => {
                    if (!newSelectedTools.includes(name)) {
                        newSelectedTools.push(name);
                    }
                });
                setSelectedToolNames(newSelectedTools);
            } else {
                // Deselect all tools from all groups
                const allToolNames = new Set(groups.flatMap(group => group.tools.map(tool => tool.name)));
                setSelectedToolNames(selectedToolNames.filter(name => !allToolNames.has(name)));
            }
        },
        [selectedToolNames, setSelectedToolNames, groups]
    );

    return {
        toolType,
        onToolTypeChange: setToolType,
        expandedGroupNames,
        onGroupExpandedChange,
        selectedToolNames,
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
