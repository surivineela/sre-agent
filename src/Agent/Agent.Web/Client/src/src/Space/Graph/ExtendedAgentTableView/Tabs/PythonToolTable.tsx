import { Button, TableCell, TableHeaderCell, Text } from '@fluentui/react-components';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentNodeType, ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';
import { EntityTable } from '../Common/EntityTable';
import { ToolTableToolbar } from '../Common/ToolTableToolbar';
import { BaseTableItem, EntityTableProps, PythonToolItem } from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';

interface PythonToolTableProps extends EntityTableProps {
    pythonTools: ExtendedTool[];
}

export const PythonToolTable: FC<PythonToolTableProps> = ({ pythonTools, openInfoPanel, refresh, lastUpdated, isLoading }) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const [searchText, setSearchText] = useState<string>();
    const [selectedTools, setSelectedTools] = useState<ExtendedTool[]>([]);

    const EMPTY_DISPLAY = useMemo(() => intl.formatMessage(SreAgentResources.none), [intl]);

    const pythonToolItems = useMemo(() => {
        const query = searchText?.trim().toLowerCase();
        let filteredTools = pythonTools;

        if (query) {
            filteredTools = filteredTools.filter(tool => tool.name?.toLowerCase().includes(query));
        }

        return filteredTools.map(tool => {
            const timeoutText = tool.timeoutSeconds ? `${tool.timeoutSeconds}s` : EMPTY_DISPLAY;

            return {
                name: tool.name || EMPTY_DISPLAY,
                description: tool.description || EMPTY_DISPLAY,
                timeout: timeoutText,
                data: tool,
            };
        });
    }, [searchText, pythonTools, EMPTY_DISPLAY]);

    const renderTableHeaders = useCallback(() => {
        return (
            <>
                <TableHeaderCell className={styles.tableHeader} style={{ width: '75px' }}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolName)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader} style={{ width: '200px' }}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.description)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader} style={{ width: '130px' }}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.timeout)}
                </TableHeaderCell>
            </>
        );
    }, [intl, styles.tableHeader]);

    const renderTableCells = useCallback(
        (item: BaseTableItem) => {
            const toolItem = item as PythonToolItem;
            return (
                <>
                    <TableCell tabIndex={0} role="gridcell">
                        <Button
                            appearance="transparent"
                            onClick={() => openInfoPanel?.(toolItem.name, ExtendedAgentNodeType.Tool)}
                            className={styles.transparentButton}
                        >
                            <Text className={styles.clickableText}>{toolItem.name}</Text>
                        </Button>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{toolItem.description}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{toolItem.timeout}</Text>
                    </TableCell>
                </>
            );
        },
        [styles, openInfoPanel]
    );

    return (
        <div className={styles.entityTable}>
            <ToolTableToolbar
                toolType="python"
                selectedTools={selectedTools}
                searchText={searchText}
                setSearchText={setSearchText}
                searchPlaceholder={ExtendedAgentsGraphResources.searchByPythonTool}
                refresh={refresh}
                lastUpdated={lastUpdated}
            />
            <EntityTable
                activeTab="pythonTools"
                searchText={searchText}
                items={pythonToolItems}
                setSelectedItems={(items: BaseTableItem[]) => setSelectedTools(items.map(item => (item as PythonToolItem).data))}
                renderTableHeaders={renderTableHeaders}
                renderTableCells={renderTableCells}
                isLoading={isLoading}
            />
        </div>
    );
};
