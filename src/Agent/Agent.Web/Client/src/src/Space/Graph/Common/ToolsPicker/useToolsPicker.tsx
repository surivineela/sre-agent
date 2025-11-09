import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { ToolPickerOption, ToolsPickerProps } from './ToolsPicker';

export interface UseToolsPickerProps {
    selectedToolNames: string[];
    setSelectedToolNames: (toolNames: string[]) => void;
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    excludedToolNames?: string[];
}

export interface UseToolsPickerReturn extends ToolsPickerProps {
    pillItems: { key: string; label: string }[];
    onClearSelectedTools: () => void;
    onClearSearchAndExpandedGroups: () => void;
}

export const useToolsPicker = (props: UseToolsPickerProps): UseToolsPickerReturn => {
    const { selectedToolNames, setSelectedToolNames, existingTools, systemTools, excludedToolNames } = props;
    const intl = useIntl();

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

    const availableToolOptions = useMemo(() => {
        const normalize = (value?: string | null) => (value ?? '').trim();
        const currentToolsNormalized = new Set((excludedToolNames ?? []).map(normalize).filter(Boolean));
        const options: ToolPickerOption[] = [];

        existingTools?.forEach(tool => {
            const name = normalize(tool.name);
            if (!name || currentToolsNormalized.has(name)) {
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

        systemTools?.forEach(systemTool => {
            const name = normalize(systemTool.name);
            if (!name || currentToolsNormalized.has(name)) {
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

        return options;
    }, [excludedToolNames, existingTools, systemTools, getExtendedToolCategory, intl]);

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

    const groups = useMemo(() => {
        const groups = new Map<string, ToolPickerOption[]>();

        filteredToolOptions.forEach(option => {
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
    }, [filteredToolOptions]);

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

    return {
        expandedGroupNames,
        onGroupExpandedChange,
        selectedToolNames,
        onSelectedToolChange,
        onClearSelectedTools,
        onClearSearchAndExpandedGroups,
        searchQuery,
        setSearchQuery,
        groups,
        pillItems,
    };
};
