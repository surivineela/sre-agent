import { Button, Dialog, DialogBody, DialogSurface, DialogTitle, SearchBox, ToolbarButton } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { useAddExistingToolDialogStyles } from './AddExistingToolDialog.Styles';
import { ToolsPillSet } from './ToolsPillSet';
import { ToolPickerOption, ToolsTreeGrid } from './ToolsTreeGrid';

export interface AddExistingToolDialogProps {
    onDismiss: () => void;
    addToolsToAgent: (agentName: string, toolsNames: string[]) => void;
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    toolPickerInfo?: { agent: ExtendedAgent };
}

export const AddExistingToolDialog: FC<AddExistingToolDialogProps> = ({
    onDismiss,
    addToolsToAgent,
    existingTools,
    systemTools,
    toolPickerInfo,
}) => {
    const intl = useIntl();
    const styles = useAddExistingToolDialogStyles();
    const agent = useMemo(() => toolPickerInfo?.agent, [toolPickerInfo]);

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

    const [selectedToolNames, setSelectedToolNames] = useState<string[]>([]);
    const onSelectedToolChange = useCallback(
        (toolName: string, isSelected: boolean) => {
            if (isSelected) {
                setSelectedToolNames(prev => [...prev, toolName]);
            } else {
                setSelectedToolNames(prev => prev.filter(name => name !== toolName));
            }
        },
        [setSelectedToolNames]
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
        if (!agent) {
            return [] as ToolPickerOption[];
        }

        const normalize = (value?: string | null) => (value ?? '').trim();
        const currentTools = new Set((agent.tools ?? []).map(normalize).filter(Boolean));
        const currentSystemTools = new Set((agent.systemTools ?? []).map(normalize).filter(Boolean));
        const options: ToolPickerOption[] = [];

        existingTools?.forEach(tool => {
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

        systemTools?.forEach(systemTool => {
            const name = normalize(systemTool.name);
            if (!name) {
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
    }, [agent, existingTools, systemTools, getExtendedToolCategory, intl]);

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

    const toolGroups = useMemo(() => {
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

    const clearAndDismiss = useCallback(() => {
        setSearchQuery('');
        setExpandedGroupNames([]);
        setSelectedToolNames([]);
        onDismiss();
    }, [onDismiss]);

    return (
        <Dialog
            open={!!toolPickerInfo}
            onOpenChange={(_, data) => {
                if (!data.open) {
                    clearAndDismiss();
                }
            }}
        >
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <div className={styles.dialogTitleWrapper}>
                        <DialogTitle
                            className={styles.dialogTitle}
                            action={
                                <ToolbarButton
                                    aria-label={intl.formatMessage(SreAgentResources.close)}
                                    appearance="transparent"
                                    icon={<Dismiss24Regular />}
                                    onClick={clearAndDismiss}
                                />
                            }
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddExistingTools)}
                        </DialogTitle>
                    </div>
                    <ToolsPillSet
                        toolNames={selectedToolNames}
                        onRemoveTool={toolName => setSelectedToolNames(prev => prev.filter(name => name !== toolName))}
                        onClearAll={() => setSelectedToolNames([])}
                    />
                    <div className={styles.dialogContentWrapper}>
                        <SearchBox
                            className={styles.searchBox}
                            placeholder={intl.formatMessage(SreAgentResources.search)}
                            value={searchQuery}
                            onChange={(_, data) => setSearchQuery(data.value)}
                        />
                        <ToolsTreeGrid
                            groups={toolGroups}
                            expandedGroupNames={expandedGroupNames}
                            onGroupExpandedChange={onGroupExpandedChange}
                            selectedToolsNames={selectedToolNames}
                            onSelectedToolChange={onSelectedToolChange}
                        />
                    </div>
                    <div className={styles.buttonsContainer}>
                        <Button
                            appearance="primary"
                            onClick={() => {
                                addToolsToAgent(agent!.name, selectedToolNames);
                                clearAndDismiss();
                            }}
                            disabled={!selectedToolNames.length}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.addTools)}
                        </Button>
                        <Button appearance="secondary" onClick={clearAndDismiss}>
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </div>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
