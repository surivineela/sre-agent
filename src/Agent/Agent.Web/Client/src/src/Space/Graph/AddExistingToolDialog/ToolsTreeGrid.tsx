import { TreeGrid, TreeGridCell, TreeGridRow } from '@fluentui-contrib/react-tree-grid';
import {
    Checkbox,
    makeStyles,
    mergeClasses,
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
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';

export interface ToolPickerOption {
    name: string;
    description?: string;
    connector?: string;
    groupLabel: string;
    categoryLabel: string;
    kind: 'tool' | 'system';
    pluginName?: string;
    resourceType?: string;
    searchText: string;
}

export interface ToolTreeGridGroup {
    category: string;
    tools: ToolPickerOption[];
}

export interface ToolsTreeGridProps {
    groups: ToolTreeGridGroup[];
    expandedGroupNames: string[];
    onGroupExpandedChange: (groupName: string, expanded: boolean) => void;
    selectedToolsNames: string[];
    onSelectedToolChange: (toolName: string, isSelected: boolean) => void;
}

export const ToolsTreeGrid: FC<ToolsTreeGridProps> = ({
    groups,
    expandedGroupNames,
    onGroupExpandedChange,
    selectedToolsNames,
    onSelectedToolChange,
}) => {
    const intl = useIntl();
    const tableRowStyle = useTableRowStyle();
    const tableCellStyle = useTableHeaderCellStyle();
    const tableHeaderStyle = useTableHeaderStyle();
    const styles = useToolsTreeGridStyles();

    return (
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
                        selectedToolsNames={selectedToolsNames}
                        onSelectedToolChange={onSelectedToolChange}
                    />
                ))}
            </div>
        </TreeGrid>
    );
};

interface ToolListProps {
    tools?: ToolPickerOption[];
    selectedToolsNames: string[];
    onSelectedToolChange: (toolName: string, isSelected: boolean) => void;
}

const ToolList: FC<ToolListProps> = ({ tools, selectedToolsNames, onSelectedToolChange }) => {
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
                        />
                    </TreeGridCell>
                    <TreeGridCell className={tableCellStyle}>{tool.name}</TreeGridCell>
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
}

const ToolGroup: FC<ToolGroupProps> = ({ name, tools, expanded, onExpandedChange, selectedToolsNames, onSelectedToolChange }) => {
    const tableRowStyle = useTableRowStyle();
    const tableCellStyle = useTableCellStyle();
    const styles = useToolsTreeGridStyles();

    return (
        <TreeGridRow
            open={expanded}
            onOpenChange={(_, data) => onExpandedChange(data.open)}
            subtree={<ToolList tools={tools} selectedToolsNames={selectedToolsNames} onSelectedToolChange={onSelectedToolChange} />}
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
        height: 'calc(100% - 48px)',
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
});
