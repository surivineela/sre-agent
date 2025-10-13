import {
    Button,
    Checkbox,
    CheckboxOnChangeData,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    InputOnChangeData,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    MessageBarTitle,
    SearchBox,
    SearchBoxChangeEvent,
    Spinner,
    Table,
    TableBody,
    TableCell,
    TableCellLayout,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import {
    Add16Regular,
    ArrowClockwise16Regular,
    ChevronDown16Regular,
    ChevronRight16Regular,
    Delete16Regular,
    Edit16Regular,
} from '@fluentui/react-icons';
import { debounce } from 'lodash';
import { ChangeEvent, FC, Fragment, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ComponentResources, ExtendedAgentsGraphResources, SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedConnector, ExtendedTool } from '../Contracts/ExtendedAgentGraph';
import { ExtendedEntityYamlEditor } from './ExtendedAgentYamlEditor';
import { ExtendedEntityType } from './ExtendedAgentYamlUtils';

const useListViewStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        paddingTop: '16px',
    },
    description: {
        color: tokens.colorNeutralForeground2,
        fontSize: '14px',
        lineHeight: '20px',
        marginBottom: '16px',
    },
    searchAndToolbar: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        marginBottom: '16px',
    },
    tableContainer: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        overflowX: 'hidden',
        overflowY: 'auto',
        backgroundColor: tokens.colorNeutralBackground1,
        maxHeight: 'calc(100vh - 280px)',
    },
    table: {
        width: '100%',
        borderCollapse: 'collapse',
    },
    categoryRow: {
        backgroundColor: tokens.colorNeutralBackground2,
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground2Hover,
        },
    },
    categoryCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '8px 16px',
        fontWeight: tokens.fontWeightSemibold,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    itemRow: {
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    selectedRow: {
        backgroundColor: tokens.colorNeutralBackground1Selected,
    },
    itemCell: {
        padding: '6px 16px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        ':last-child': {
            borderBottom: 'none',
        },
    },
    selectionCell: {
        width: '20px',
        minWidth: '20px',
        padding: '6px 0',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        '& :global(.fui-Checkbox)': {
            marginInlineStart: 0,
            columnGap: 0,
            marginInlineEnd: 0,
        },
        '& :global(.fui-Checkbox__indicator)': {
            marginInlineEnd: 0,
        },
    },
    nameCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    nameColumn: {
        width: '240px',
        minWidth: '260px',
        maxWidth: '300px',
    },
    descriptionColumn: {
        width: '45%',
        minWidth: '45%',
    },
    editButton: {
        opacity: 0,
        transition: 'opacity 0.2s',
    },
    itemRowHover: {
        '& .edit-button': {
            opacity: 1,
        },
    },
    emptyState: {
        padding: '40px',
        textAlign: 'center',
        color: tokens.colorNeutralForeground3,
    },
    errorBar: {
        marginBottom: '8px',
    },
    dangerButton: {
        backgroundColor: tokens.colorPaletteRedBackground3,
        color: tokens.colorNeutralForegroundOnBrand,
        ':hover': {
            backgroundColor: tokens.colorPaletteRedBackground2,
        },
        ':active': {
            backgroundColor: tokens.colorPaletteRedBackground1,
        },
    },
});

interface ExtendedAgentListViewProps {
    agents: ExtendedAgent[];
    tools: ExtendedTool[];
    connectors: ExtendedConnector[];
    isLoading: boolean;
    onRefresh: () => void;
    onCreateClick: () => void;
}

type EntityItem = {
    id: string;
    name: string;
    type: 'agent' | 'tool' | 'connector';
    description?: string;
    category: string;
    data: ExtendedAgent | ExtendedTool | ExtendedConnector;
};

export const ExtendedAgentListView: FC<ExtendedAgentListViewProps> = ({
    agents,
    tools,
    connectors,
    isLoading,
    onRefresh,
    onCreateClick,
}) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const intl = useIntl();

    const [searchText, setSearchText] = useState<string>('');
    const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
    const [selectedItems, setSelectedItems] = useState<EntityItem[]>([]);
    const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
    const [yamlEditorEntity, setYamlEditorEntity] = useState<ExtendedAgent | ExtendedTool | ExtendedConnector | undefined>();
    const [yamlEditorType, setYamlEditorType] = useState<ExtendedEntityType>('agent');
    const [isDeleting, setIsDeleting] = useState(false);
    const [errorMessage, setErrorMessage] = useState<string | undefined>();
    const [collapsedSections, setCollapsedSections] = useState<Set<string>>(
        new Set([
            intl.formatMessage(SreAgentResources.agents),
            intl.formatMessage(ExtendedAgentsGraphResources.tools),
            intl.formatMessage(SettingsTabResources.dataConnectors),
        ])
    );

    // Convert entities to list items with categories
    const allItems = useMemo<EntityItem[]>(() => {
        const items: EntityItem[] = [];

        agents.forEach(agent => {
            items.push({
                id: `agent_${agent.name}`,
                name: agent.name,
                type: 'agent',
                category: intl.formatMessage(SreAgentResources.agents),
                description: agent.instructions?.substring(0, 100),
                data: agent,
            });
        });

        tools.forEach(tool => {
            items.push({
                id: `tool_${tool.name}`,
                name: tool.name,
                type: 'tool',
                category: intl.formatMessage(ExtendedAgentsGraphResources.tools),
                description: tool.description,
                data: tool,
            });
        });

        connectors.forEach(connector => {
            items.push({
                id: `connector_${connector.name}`,
                name: connector.name,
                type: 'connector',
                category: intl.formatMessage(SettingsTabResources.dataConnectors),
                description: connector.description,
                data: connector,
            });
        });

        return items;
    }, [agents, tools, connectors, intl]);

    // Filter items based on search and group by category
    const filteredItemsByCategory = useMemo(() => {
        let items = allItems;

        if (searchText.trim()) {
            const query = searchText.toLowerCase();
            items = allItems.filter(
                item =>
                    item.name.toLowerCase().includes(query) ||
                    item.type.toLowerCase().includes(query) ||
                    (item.description && item.description.toLowerCase().includes(query))
            );
        }

        // Group by category
        const groups = new Map<string, EntityItem[]>();
        items.forEach(item => {
            const categoryItems = groups.get(item.category) || [];
            categoryItems.push(item);
            groups.set(item.category, categoryItems);
        });

        return Array.from(groups.entries()).map(([category, categoryItems]) => ({
            category,
            items: categoryItems,
        }));
    }, [allItems, searchText]);

    const flattenedFilteredItems = useMemo(() => {
        return filteredItemsByCategory.flatMap(group => group.items);
    }, [filteredItemsByCategory]);

    const allVisibleItemIds = useMemo(() => flattenedFilteredItems.map(item => item.id), [flattenedFilteredItems]);

    const { allVisibleSelected, anyVisibleSelected } = useMemo(() => {
        if (allVisibleItemIds.length === 0) {
            return { allVisibleSelected: false, anyVisibleSelected: false };
        }

        const selectedCount = allVisibleItemIds.reduce((count, id) => (selectedKeys.includes(id) ? count + 1 : count), 0);
        return {
            allVisibleSelected: selectedCount === allVisibleItemIds.length,
            anyVisibleSelected: selectedCount > 0,
        };
    }, [allVisibleItemIds, selectedKeys]);

    const selectAllCheckboxState = useMemo(() => {
        if (allVisibleSelected) {
            return true;
        }
        if (anyVisibleSelected) {
            return 'mixed' as const;
        }
        return false;
    }, [allVisibleSelected, anyVisibleSelected]);

    const handleEditEntityYaml = useCallback((item: EntityItem) => {
        if (item.type === 'connector') {
            // Connectors are not editable via YAML
            return;
        }
        setYamlEditorEntity(item.data);
        setYamlEditorType(item.type as ExtendedEntityType);
    }, []);

    const toggleCategory = useCallback((category: string) => {
        setCollapsedSections(prev => {
            const newSet = new Set(prev);
            if (newSet.has(category)) {
                newSet.delete(category);
            } else {
                newSet.add(category);
            }
            return newSet;
        });
    }, []);

    const isDeleteDisabled = useMemo(() => {
        if (selectedItems.length === 0 || isDeleting) return true;
        // Only allow deletion if all selected items are agents or tools (not connectors)
        return selectedItems.some(item => item.type === 'connector');
    }, [isDeleting, selectedItems]);

    const handleDelete = useCallback(async () => {
        setIsDeleting(true);
        setShowDeleteConfirmationDialog(false);
        setErrorMessage(undefined);

        try {
            const agentHeaders = getAgentHeaders();

            const typePathMap: Record<'agent' | 'tool' | 'connector', string> = {
                agent: 'agents',
                tool: 'tools',
                connector: 'dataconnectors',
            };

            // Only delete agents and tools, skip connectors
            const deletableItems = selectedItems.filter(item => item.type !== 'connector');

            await Promise.all(
                deletableItems.map(async item => {
                    const pathSegment = typePathMap[item.type];
                    const endpoint = `${sreAgentEndpoint}/api/v1/extendedAgent/${pathSegment}/${encodeURIComponent(item.name)}`;

                    const response = await fetch(endpoint, {
                        method: 'DELETE',
                        headers: agentHeaders,
                    });

                    if (!response.ok) {
                        let details: string;
                        try {
                            details = (await response.text()) || `${response.status} ${response.statusText}`;
                        } catch (readError) {
                            console.debug('Failed to read delete error response text', readError);
                            details = `${response.status} ${response.statusText}`;
                        }

                        throw new Error(`Failed to delete ${item.name}: ${details}`);
                    }
                })
            );

            setSelectedItems([]);
            setSelectedKeys([]);
            onRefresh();
        } catch (error) {
            console.error('Error deleting entities:', error);
            const message = error instanceof Error ? error.message : 'Unexpected error during deletion.';
            setErrorMessage(message);
        } finally {
            setIsDeleting(false);
        }
    }, [selectedItems, sreAgentEndpoint, onRefresh]);

    const styles = useListViewStyles();

    const toggleItemSelection = useCallback((item: EntityItem, shouldSelect: boolean) => {
        setSelectedKeys(prev => {
            if (shouldSelect) {
                return prev.includes(item.id) ? prev : [...prev, item.id];
            }
            return prev.filter(key => key !== item.id);
        });

        setSelectedItems(prev => {
            if (shouldSelect) {
                const exists = prev.some(selected => selected.id === item.id);
                return exists ? prev : [...prev, item];
            }
            return prev.filter(selected => selected.id !== item.id);
        });
    }, []);

    const handleSelectAllChange = useCallback(
        (_event: ChangeEvent<HTMLInputElement>, data: CheckboxOnChangeData) => {
            const isChecked = !!data.checked;

            if (isChecked) {
                setSelectedKeys(prev => {
                    const next = new Set(prev);
                    allVisibleItemIds.forEach(id => next.add(id));
                    return Array.from(next);
                });

                setSelectedItems(prev => {
                    const existingIds = new Set(prev.map(item => item.id));
                    const additional = flattenedFilteredItems.filter(item => !existingIds.has(item.id));
                    return [...prev, ...additional];
                });
            } else {
                setSelectedKeys(prev => prev.filter(id => !allVisibleItemIds.includes(id)));
                setSelectedItems(prev => prev.filter(item => !allVisibleItemIds.includes(item.id)));
            }
        },
        [allVisibleItemIds, flattenedFilteredItems]
    );

    const handleItemClick = useCallback(
        (item: EntityItem) => {
            const isCurrentlySelected = selectedKeys.includes(item.id);
            toggleItemSelection(item, !isCurrentlySelected);
        },
        [selectedKeys, toggleItemSelection]
    );

    return (
        <div className={styles.container}>
            <Text className={styles.description}>{intl.formatMessage(ExtendedAgentsGraphResources.subAgentBuilderDescription)}</Text>

            <div className={styles.searchAndToolbar}>
                <SearchBox
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchPlaceholder)}
                    value={searchText}
                    onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''), 300)}
                />

                <Toolbar>
                    <ToolbarButton icon={<Add16Regular />} appearance="subtle" disabled={isLoading} onClick={onCreateClick}>
                        {intl.formatMessage(SreAgentResources.create)}
                    </ToolbarButton>
                    <ToolbarButton icon={<ArrowClockwise16Regular />} appearance="subtle" disabled={isLoading} onClick={onRefresh}>
                        {intl.formatMessage(SreAgentResources.refresh)}
                    </ToolbarButton>
                    <ToolbarDivider />
                    <ToolbarButton
                        appearance="subtle"
                        icon={<Delete16Regular />}
                        onClick={() => setShowDeleteConfirmationDialog(true)}
                        disabled={isDeleteDisabled}
                    >
                        {intl.formatMessage(SreAgentResources.delete)}
                    </ToolbarButton>
                </Toolbar>
            </div>

            {errorMessage && (
                <MessageBar intent="error" className={styles.errorBar} role="alert">
                    <MessageBarBody>
                        <MessageBarTitle>{intl.formatMessage(SreAgentResources.error)}</MessageBarTitle>
                        {errorMessage}
                    </MessageBarBody>
                    <MessageBarActions>
                        <Button appearance="transparent" onClick={() => setErrorMessage(undefined)}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.relationshipDismiss)}
                        </Button>
                    </MessageBarActions>
                </MessageBar>
            )}

            <div className={styles.tableContainer}>
                {isLoading ? (
                    <div className={styles.emptyState}>
                        <Spinner />
                        <Text>{intl.formatMessage(ComponentResources.loading)}</Text>
                    </div>
                ) : filteredItemsByCategory.length === 0 ? (
                    <div className={styles.emptyState}>
                        <Text>No items found{searchText ? ` matching "${searchText}"` : ''}.</Text>
                    </div>
                ) : (
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell className={styles.selectionCell}>
                                    <Checkbox
                                        aria-label={intl.formatMessage(ExtendedAgentsGraphResources.listViewSelectAll)}
                                        checked={selectAllCheckboxState}
                                        onChange={handleSelectAllChange}
                                        disabled={flattenedFilteredItems.length === 0}
                                    />
                                </TableHeaderCell>
                                <TableHeaderCell className={styles.nameColumn}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.listViewNameColumn)}
                                </TableHeaderCell>
                                <TableHeaderCell>{intl.formatMessage(ExtendedAgentsGraphResources.listViewTypeColumn)}</TableHeaderCell>
                                <TableHeaderCell className={styles.descriptionColumn}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.listViewDescriptionColumn)}
                                </TableHeaderCell>
                                <TableHeaderCell>{intl.formatMessage(ExtendedAgentsGraphResources.listViewActionsColumn)}</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {filteredItemsByCategory.map(group => {
                                const isCollapsed = collapsedSections.has(group.category);

                                return (
                                    <Fragment key={group.category}>
                                        <TableRow className={styles.categoryRow} onClick={() => toggleCategory(group.category)}>
                                            <TableCell className={styles.categoryCell} colSpan={5}>
                                                <TableCellLayout
                                                    media={
                                                        <Button
                                                            appearance="subtle"
                                                            size="small"
                                                            icon={isCollapsed ? <ChevronRight16Regular /> : <ChevronDown16Regular />}
                                                            onClick={event => {
                                                                event.stopPropagation();
                                                                toggleCategory(group.category);
                                                            }}
                                                        />
                                                    }
                                                >
                                                    <Text weight="semibold">
                                                        {group.category} ({group.items.length})
                                                    </Text>
                                                </TableCellLayout>
                                            </TableCell>
                                        </TableRow>
                                        {!isCollapsed &&
                                            group.items.map(item => {
                                                const isSelected = selectedKeys.includes(item.id);
                                                return (
                                                    <TableRow
                                                        key={item.id}
                                                        className={`${styles.itemRow} ${isSelected ? styles.selectedRow : ''}`}
                                                        onClick={() => handleItemClick(item)}
                                                    >
                                                        <TableCell className={styles.selectionCell}>
                                                            <Checkbox
                                                                checked={isSelected}
                                                                onChange={(event, data) => {
                                                                    event.stopPropagation();
                                                                    toggleItemSelection(item, !!data.checked);
                                                                }}
                                                            />
                                                        </TableCell>
                                                        <TableCell className={`${styles.itemCell} ${styles.nameColumn}`}>
                                                            <TableCellLayout className={styles.nameCell}>
                                                                <Text weight="semibold">{item.name}</Text>
                                                            </TableCellLayout>
                                                        </TableCell>
                                                        <TableCell className={styles.itemCell}>
                                                            <Text style={{ textTransform: 'capitalize' }}>{item.type}</Text>
                                                        </TableCell>
                                                        <TableCell className={`${styles.itemCell} ${styles.descriptionColumn}`}>
                                                            <Text>
                                                                {item.description ||
                                                                    intl.formatMessage(
                                                                        ExtendedAgentsGraphResources.listViewDescriptionFallback
                                                                    )}
                                                            </Text>
                                                        </TableCell>
                                                        <TableCell className={styles.itemCell}>
                                                            {isSelected && item.type !== 'connector' && (
                                                                <Button
                                                                    appearance="subtle"
                                                                    size="small"
                                                                    icon={<Edit16Regular />}
                                                                    onClick={e => {
                                                                        e.stopPropagation();
                                                                        handleEditEntityYaml(item);
                                                                    }}
                                                                    className="edit-button"
                                                                />
                                                            )}
                                                        </TableCell>
                                                    </TableRow>
                                                );
                                            })}
                                    </Fragment>
                                );
                            })}
                        </TableBody>
                    </Table>
                )}
            </div>

            {/* Delete Confirmation Dialog */}
            <Dialog open={showDeleteConfirmationDialog} onOpenChange={(_, data) => setShowDeleteConfirmationDialog(data.open)}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(ExtendedAgentsGraphResources.deleteConfirmTitle)}</DialogTitle>
                        <DialogContent>
                            {intl.formatMessage(ExtendedAgentsGraphResources.deleteConfirmMessage, { count: selectedItems.length })}
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="primary" onClick={handleDelete} disabled={isDeleting} className={styles.dangerButton}>
                                {intl.formatMessage(SreAgentResources.yes)}
                            </Button>
                            <Button appearance="secondary" onClick={() => setShowDeleteConfirmationDialog(false)}>
                                {intl.formatMessage(SreAgentResources.no)}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            {/* YAML Editor */}
            <ExtendedEntityYamlEditor
                entity={yamlEditorEntity}
                entityType={yamlEditorType}
                sreAgentEndpoint={sreAgentEndpoint}
                isOpen={!!yamlEditorEntity}
                onClose={() => setYamlEditorEntity(undefined)}
                onApplied={async () => {
                    await onRefresh();
                    setYamlEditorEntity(undefined);
                }}
            />
        </div>
    );
};

export default ExtendedAgentListView;
