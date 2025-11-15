import { TreeGrid, TreeGridCell, TreeGridRow } from '@fluentui-contrib/react-tree-grid';
import {
    Checkbox,
    makeStyles,
    mergeClasses,
    Radio,
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
import { createRef, FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';

export interface ToolPickerOption {
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
    selectedToolNames: string[];
    onSelectedToolChange: (toolName: string, isSelected: boolean) => void;
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
    selectedToolNames,
    onSelectedToolChange,
    searchQuery,
    setSearchQuery,
    disabled,
}) => {
    const intl = useIntl();
    const tableRowStyle = useTableRowStyle();
    const tableCellStyle = useTableHeaderCellStyle();
    const tableHeaderStyle = useTableHeaderStyle();
    const styles = useToolsTreeGridStyles();

    return (
        <>
            <div className={styles.toolBar}>
                <RadioGroup value={toolType} layout="horizontal" onChange={(_, data) => onToolTypeChange(data.value as 'mcp' | 'all')}>
                    <Radio value="all" label={intl.formatMessage(ExtendedAgentsGraphResources.allTools)} />
                    <Radio value="mcp" label={intl.formatMessage(ExtendedAgentsGraphResources.mcpTools)} />
                </RadioGroup>
                <SearchBox
                    className={styles.searchBox}
                    placeholder={intl.formatMessage(SreAgentResources.search)}
                    value={searchQuery}
                    onChange={(_, data) => setSearchQuery(data.value)}
                    disabled={disabled}
                />
            </div>
            <TreeGrid aria-label={intl.formatMessage(ExtendedAgentsGraphResources.allTools)} className={styles.treeGrid}>
                <div role="rowgroup" className={tableHeaderStyle}>
                    <div role="row" className={mergeClasses(tableRowStyle, styles.tableRow)}>
                        <div
                            role="columnheader"
                            className={styles.checkboxCell}
                            aria-label={intl.formatMessage(ExtendedAgentsGraphResources.selectTool)}
                        />
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
                            name={group.category}
                            expanded={!!expandedGroupNames.includes(group.category)}
                            onExpandedChange={expanded => onGroupExpandedChange(group.category, expanded)}
                            tools={group.tools}
                            selectedToolsNames={selectedToolNames}
                            onSelectedToolChange={onSelectedToolChange}
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
    selectedToolsNames: string[];
    onSelectedToolChange: (toolName: string, isSelected: boolean) => void;
    disabled?: boolean;
}

const ToolList: FC<ToolListProps> = ({ tools, selectedToolsNames, onSelectedToolChange, disabled }) => {
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
                        <Checkbox
                            checked={selectedToolsNames.includes(tool.name)}
                            onChange={(_, data) => onSelectedToolChange(tool.name, !!data.checked)}
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
    selectedToolsNames: string[];
    onSelectedToolChange: (toolName: string, isSelected: boolean) => void;
    disabled?: boolean;
}

const ToolGroup: FC<ToolGroupProps> = ({ name, tools, expanded, onExpandedChange, selectedToolsNames, onSelectedToolChange, disabled }) => {
    const tableRowStyle = useTableRowStyle();
    const tableCellStyle = useTableCellStyle();
    const styles = useToolsTreeGridStyles();

    return (
        <TreeGridRow
            open={expanded}
            onOpenChange={(_, data) => onExpandedChange(data.open)}
            subtree={
                <ToolList
                    tools={tools}
                    selectedToolsNames={selectedToolsNames}
                    onSelectedToolChange={onSelectedToolChange}
                    disabled={disabled}
                />
            }
            className={mergeClasses(tableRowStyle, styles.tableRow)}
        >
            <TreeGridCell className={mergeClasses(tableCellStyle, styles.groupHeaderCell)} header>
                <span className={styles.groupHeaderContent}>
                    <div className={styles.checkboxCell}>
                        {expanded ? <ChevronDownRegular aria-hidden /> : <ChevronRightRegular aria-hidden />}
                    </div>
                </span>
                {expanded ? name : `${name} (${tools?.length ?? 0})`}
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
        overflowX: 'hidden',
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
        gap: '5px',
    },
    checkboxCell: {
        width: '40px',
        flex: 'none',
        display: 'flex',
        justifyContent: 'center',
    },
    toolNameCell: {
        overflowX: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
});
