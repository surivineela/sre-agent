import { TreeGrid, TreeGridCell, TreeGridRow } from '@fluentui-contrib/react-tree-grid';
import {
    Checkbox,
    makeStyles,
    mergeClasses,
    RadioGroup,
    SearchBox,
    useTableCell_unstable,
    useTableCellStyles_unstable,
    useTableHeader_unstable,
    useTableHeaderCell_unstable,
    useTableHeaderCellStyles_unstable,
    useTableHeaderStyles_unstable,
    useTableRow_unstable,
    useTableRowStyles_unstable,
} from '@fluentui/react-components';
import { ChevronDownRegular, ChevronRightRegular } from '@fluentui/react-icons';
import { createRef, FC, useCallback } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { CopilotRadio } from '../../../Components/Common/CopilotRadio';

export interface ToolPickerOption {
    key: string;
    name: string;
    description?: string;
    connector?: string;
    groupLabel: string;
    categoryLabel: string;
    kind: 'tool' | 'system' | 'mcp';
    pluginName?: string;
    resourceType?: string;
    searchText: string;
}

export interface ToolTreeGridGroup {
    category: string;
    tools: ToolPickerOption[];
}

export interface ToolsPickerProps {
    toolType: 'mcp' | 'all';
    onToolTypeChange: (toolType: 'mcp' | 'all') => void;
    groups: ToolTreeGridGroup[];
    expandedGroupNames: string[];
    onGroupExpandedChange: (groupName: string, expanded: boolean) => void;
    selectedToolKeys: string[];
    onSelectedToolChange: (key: string, isSelected: boolean) => void;
    onSelectAllToolsInGroup: (groupName: string, isSelected: boolean) => void;
    onSelectAllTools: (isSelected: boolean) => void;
    searchQuery: string;
    setSearchQuery: React.Dispatch<React.SetStateAction<string>>;
    disabled?: boolean;
}

export const ToolsPicker: FC<ToolsPickerProps> = ({
    toolType,
    onToolTypeChange,
    groups,
    expandedGroupNames,
    onGroupExpandedChange,
    selectedToolKeys,
    onSelectedToolChange,
    onSelectAllToolsInGroup,
    onSelectAllTools,
    searchQuery,
    setSearchQuery,
    disabled,
}) => {
    const intl = useIntl();
    const tableRowStyle = useTableRowStyle();
    const tableCellStyle = useTableHeaderCellStyle();
    const tableHeaderStyle = useTableHeaderStyle();
    const styles = useToolsTreeGridStyles();

    // Calculate if all tools are selected
    const allToolKeys = groups.flatMap(group => group.tools.map(tool => tool.key));
    const allToolsSelected = allToolKeys.length > 0 && allToolKeys.every(key => selectedToolKeys.includes(key));
    const someToolsSelected = allToolKeys.some(key => selectedToolKeys.includes(key));

    return (
        <>
            <div className={styles.toolBar}>
                <RadioGroup value={toolType} layout="horizontal" onChange={(_, data) => onToolTypeChange(data.value as 'mcp' | 'all')}>
                    <CopilotRadio value="all" label={intl.formatMessage(ExtendedAgentsGraphResources.allTools)} />
                    <CopilotRadio value="mcp" label={intl.formatMessage(ExtendedAgentsGraphResources.mcpTools)} />
                </RadioGroup>
                <SearchBox
                    className={styles.searchBox}
                    placeholder={intl.formatMessage(SreAgentResources.search)}
                    value={searchQuery}
                    onChange={(_, data) => setSearchQuery(data.value)}
                    disabled={disabled}
                    size={'small'}
                />
            </div>
            <TreeGrid aria-label={intl.formatMessage(ExtendedAgentsGraphResources.allTools)} className={styles.treeGrid}>
                <div role="rowgroup" className={tableHeaderStyle}>
                    <div role="row" className={mergeClasses(tableRowStyle, styles.tableRow)}>
                        <div role="columnheader" className={styles.headerCheckboxCell}>
                            <div className={styles.chevronPlaceholder} />
                            <Checkbox
                                checked={allToolsSelected ? true : someToolsSelected ? 'mixed' : false}
                                onChange={(_, data) => onSelectAllTools(!!data.checked)}
                                aria-label={
                                    allToolsSelected
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.deselectAllTools)
                                        : intl.formatMessage(ExtendedAgentsGraphResources.selectAllTools)
                                }
                                disabled={disabled || allToolKeys.length === 0}
                            />
                        </div>
                        <div role="columnheader" className={tableCellStyle}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.toolName)}
                        </div>
                        <div role="columnheader" className={tableCellStyle}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.description)}
                        </div>
                    </div>
                </div>
                <div role="rowgroup" className={styles.body}>
                    {groups.map(group => (
                        <ToolGroup
                            key={group.category}
                            name={group.category}
                            expanded={!!expandedGroupNames.includes(group.category)}
                            onExpandedChange={expanded => onGroupExpandedChange(group.category, expanded)}
                            tools={group.tools}
                            selectedToolKeys={selectedToolKeys}
                            onSelectedToolChange={onSelectedToolChange}
                            onSelectAllToolsInGroup={onSelectAllToolsInGroup}
                            disabled={disabled}
                        />
                    ))}
                </div>
            </TreeGrid>
        </>
    );
};

interface ToolListProps {
    tools?: ToolPickerOption[];
    selectedToolKeys: string[];
    onSelectedToolChange: (key: string, isSelected: boolean) => void;
    disabled?: boolean;
}

const ToolList: FC<ToolListProps> = ({ tools, selectedToolKeys, onSelectedToolChange, disabled }) => {
    const intl = useIntl();
    const tableRowStyle = useTableRowStyle();
    const tableCellStyle = useTableCellStyle();
    const styles = useToolsTreeGridStyles();

    if (!tools?.length) {
        return null;
    }

    return (
        <>
            {tools.map(tool => (
                <TreeGridRow key={tool.name} className={mergeClasses(tableRowStyle, styles.tableRow)}>
                    <TreeGridCell className={styles.checkboxCell}>
                        <div className={styles.chevronPlaceholder} />
                        <Checkbox
                            checked={selectedToolKeys.includes(tool.key)}
                            onChange={(_, data) => onSelectedToolChange(tool.key, !!data.checked)}
                            aria-label={intl.formatMessage(ExtendedAgentsGraphResources.selectToolWithName, { toolName: tool.name })}
                            disabled={disabled}
                        />
                    </TreeGridCell>
                    <TreeGridCell className={mergeClasses(tableCellStyle, styles.toolNameCell)}>{tool.name}</TreeGridCell>
                    <TreeGridCell className={tableCellStyle}>{tool.description}</TreeGridCell>
                </TreeGridRow>
            ))}
        </>
    );
};

interface ToolGroupProps {
    name: string;
    expanded: boolean;
    onExpandedChange: (expanded: boolean) => void;
    tools?: ToolPickerOption[];
    selectedToolKeys: string[];
    onSelectedToolChange: (key: string, isSelected: boolean) => void;
    onSelectAllToolsInGroup: (groupName: string, isSelected: boolean) => void;
    disabled?: boolean;
}

const ToolGroup: FC<ToolGroupProps> = ({
    name,
    tools,
    expanded,
    onExpandedChange,
    selectedToolKeys,
    onSelectedToolChange,
    onSelectAllToolsInGroup,
    disabled,
}) => {
    const intl = useIntl();
    const tableRowStyle = useTableRowStyle();
    const tableCellStyle = useTableCellStyle();
    const styles = useToolsTreeGridStyles();

    // Calculate selection state for this group
    const toolKeysInGroup = tools?.map(tool => tool.key) ?? [];
    const allToolsInGroupSelected = toolKeysInGroup.length > 0 && toolKeysInGroup.every(key => selectedToolKeys.includes(key));
    const someToolsInGroupSelected = toolKeysInGroup.some(key => selectedToolKeys.includes(key));

    const handleGroupCheckboxChange = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            onSelectAllToolsInGroup(name, !allToolsInGroupSelected);
        },
        [name, allToolsInGroupSelected, onSelectAllToolsInGroup]
    );

    return (
        <TreeGridRow
            open={expanded}
            onOpenChange={(_, data) => onExpandedChange(data.open)}
            subtree={
                <ToolList
                    tools={tools}
                    selectedToolKeys={selectedToolKeys}
                    onSelectedToolChange={onSelectedToolChange}
                    disabled={disabled}
                />
            }
            className={mergeClasses(tableRowStyle, styles.tableRow)}
        >
            <TreeGridCell className={mergeClasses(tableCellStyle, styles.groupHeaderCell)} header>
                <span className={styles.groupHeaderContent}>
                    <div className={styles.chevronWrapper}>
                        {expanded ? <ChevronDownRegular aria-hidden /> : <ChevronRightRegular aria-hidden />}
                    </div>
                    <div className={styles.groupCheckboxWrapper} onClick={handleGroupCheckboxChange}>
                        <Checkbox
                            checked={allToolsInGroupSelected ? true : someToolsInGroupSelected ? 'mixed' : false}
                            onChange={() => {}} // Handled by onClick on wrapper
                            aria-label={
                                allToolsInGroupSelected
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.deselectAllToolsInGroup, { groupName: name })
                                    : intl.formatMessage(ExtendedAgentsGraphResources.selectAllToolsInGroup, { groupName: name })
                            }
                            disabled={disabled || toolKeysInGroup.length === 0}
                        />
                    </div>
                    <span className={styles.groupNameText}>{expanded ? name : `${name} (${tools?.length ?? 0})`}</span>
                </span>
            </TreeGridCell>
        </TreeGridRow>
    );
};

const useTableHeaderStyle = () =>
    useTableHeaderStyles_unstable({
        ...useTableHeader_unstable({}, createRef()),
        noNativeElements: true,
    }).root.className;

const useTableHeaderCellStyle = () => {
    const { root, button } = useTableHeaderCellStyles_unstable({
        ...useTableHeaderCell_unstable({}, createRef()),
        noNativeElements: true,
    });
    return mergeClasses(root.className, button.className);
};

const useTableRowStyle = () =>
    useTableRowStyles_unstable({
        ...useTableRow_unstable({}, createRef()),
        noNativeElements: true,
    }).root.className;

const useTableCellStyle = () =>
    useTableCellStyles_unstable({
        ...useTableCell_unstable({}, createRef()),
        noNativeElements: true,
    }).root.className;

const useToolsTreeGridStyles = makeStyles({
    treeGrid: {
        display: 'flex',
        flexDirection: 'column',
        flex: '1 1 auto',
        height: '0%',
    },
    toolBar: {
        display: 'flex',
        justifyContent: 'flex-start',
        gap: '8px',
    },
    searchBox: {
        minWidth: '75px',
        maxWidth: '265px',
    },
    body: {
        flexGrow: 1,
        overflowY: 'auto',
        overflowX: 'auto',
    },
    tableRow: {
        margin: '2px',
    },
    groupHeaderCell: {
        paddingLeft: '0px',
    },
    groupHeaderContent: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
        width: '100%',
    },
    groupCheckboxWrapper: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        cursor: 'pointer',
    },
    chevronWrapper: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        cursor: 'pointer',
        width: '20px',
        flexShrink: 0,
    },
    groupNameText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    headerCheckboxCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
        flex: 'none',
    },
    chevronPlaceholder: {
        width: '20px',
        flexShrink: 0,
    },
    checkboxCell: {
        flex: 'none',
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
    },
    toolNameCell: {
        overflowX: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
});
