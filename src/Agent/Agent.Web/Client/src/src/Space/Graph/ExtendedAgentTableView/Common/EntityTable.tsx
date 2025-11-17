import {
    createTableColumn,
    Spinner,
    Table,
    TableBody,
    TableColumnDefinition,
    TableHeader,
    TableRow,
    TableSelectionCell,
    Text,
    useArrowNavigationGroup,
    useTableFeatures,
    useTableSelection,
} from '@fluentui/react-components';
import { FC, useMemo } from 'react';
import { useIntl } from 'react-intl';
import {
    ComponentResources,
    ExtendedAgentsGraphResources,
    ScheduledTasksResources,
    SettingsTabResources,
    SreAgentResources,
} from '../../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedConnector, ExtendedTool, ExtendedTrigger } from '../../../Contracts/ExtendedAgentGraph';
import { BaseTableItem, TableViewTabValue } from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';

interface EntityTableProps {
    activeTab: TableViewTabValue;
    items: ExtendedAgent[] | ExtendedTool[] | ExtendedTrigger[] | ExtendedConnector[];
    setSelectedItems: (items: BaseTableItem[]) => void;
    renderTableHeaders: () => JSX.Element;
    renderTableCells: (item: BaseTableItem) => JSX.Element;
    isLoading?: boolean;
    searchText?: string;
}

export const EntityTable: FC<EntityTableProps> = ({
    activeTab,
    items,
    setSelectedItems,
    renderTableHeaders,
    renderTableCells,
    isLoading,
    searchText,
}) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const keyboardNavAttr = useArrowNavigationGroup({ axis: 'grid' });

    const genericColumns: TableColumnDefinition<BaseTableItem>[] = [
        createTableColumn<BaseTableItem>({
            columnId: 'name',
            compare: (a, b) => a.name.localeCompare(b.name),
        }),
    ];

    const {
        getRows,
        selection: { allRowsSelected, someRowsSelected, toggleAllRows, toggleRow, isRowSelected },
    } = useTableFeatures(
        {
            columns: genericColumns,
            items: items,
        },
        [
            useTableSelection({
                selectionMode: 'multiselect',
                defaultSelectedItems: new Set(),
                onSelectionChange: (_, data) => {
                    const selectedItems = Array.from(data.selectedItems)
                        .map(rowId => items[rowId as number])
                        .filter(Boolean);
                    setSelectedItems(selectedItems);
                },
            }),
        ]
    );

    const rows = useMemo(
        () =>
            getRows(row => {
                const selected = isRowSelected(row.rowId);
                return {
                    ...row,
                    selected,
                    appearance: selected ? ('brand' as const) : ('none' as const),
                };
            }),
        [getRows, isRowSelected]
    );

    return (
        <div>
            {isLoading ? (
                <EntityTableLoadingState />
            ) : items.length === 0 ? (
                <EntityTableEmptyState activeTab={activeTab} searchText={searchText} />
            ) : (
                <Table
                    {...keyboardNavAttr}
                    role="grid"
                    aria-label={intl.formatMessage(ExtendedAgentsGraphResources.agentDatagrid)}
                    className={styles.minWidthTable}
                >
                    <TableHeader>
                        <TableRow>
                            <TableSelectionCell
                                checked={allRowsSelected ? true : someRowsSelected ? 'mixed' : false}
                                aria-checked={allRowsSelected ? true : someRowsSelected ? 'mixed' : false}
                                role="checkbox"
                                onClick={toggleAllRows}
                                checkboxIndicator={{ 'aria-label': intl.formatMessage(SreAgentResources.selectAllRowsAriaLabel) }}
                            />
                            {renderTableHeaders()}
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {rows.map(({ item, selected, appearance, rowId }) => (
                            <TableRow key={item.name} aria-selected={selected} appearance={appearance}>
                                <TableSelectionCell
                                    role="gridcell"
                                    aria-selected={selected}
                                    checked={selected}
                                    onClick={(e: React.MouseEvent) => toggleRow(e, rowId)}
                                    checkboxIndicator={{ 'aria-label': intl.formatMessage(SreAgentResources.selectRowAriaLabel) }}
                                />
                                {renderTableCells(item)}
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            )}
        </div>
    );
};

const EntityTableLoadingState = () => {
    const intl = useIntl();
    const styles = useListViewStyles();
    return (
        <div className={styles.emptyState}>
            <Spinner />
            <Text>{intl.formatMessage(ComponentResources.loading)}</Text>
        </div>
    );
};

interface EntityTableEmptyStateProps {
    activeTab: TableViewTabValue;
    searchText?: string;
}

const EntityTableEmptyState: FC<EntityTableEmptyStateProps> = ({ activeTab, searchText }) => {
    const intl = useIntl();
    const styles = useListViewStyles();

    const entityString = useMemo(() => {
        switch (activeTab) {
            case 'agents':
                return intl.formatMessage(SettingsTabResources.subAgents);
            case 'incidentTriggers':
                return intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggers);
            case 'scheduledTasks':
                return intl.formatMessage(ScheduledTasksResources.scheduledTasks);
            case 'kustoTools':
                return intl.formatMessage(ExtendedAgentsGraphResources.kustoTools);
            default:
                return activeTab;
        }
    }, [activeTab, intl]);

    return (
        <div className={styles.emptyState}>
            <Text>
                {searchText
                    ? intl.formatMessage(ComponentResources.noResultsFoundFor, { searchString: searchText })
                    : intl.formatMessage(ExtendedAgentsGraphResources.noEntityFound, { entity: entityString })}
            </Text>
        </div>
    );
};
