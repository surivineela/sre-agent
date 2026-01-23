import { Button, Skeleton, SkeletonItem, TableCell, TableHeaderCell, Text } from '@fluentui/react-components';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PillFilter } from '../../../../Common/Components/PillFilter/PillFilter';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentNodeType, ExtendedConnector, ExtendedTool, ExtendedTrigger } from '../../../Contracts/ExtendedAgentGraph';
import { McpConnectorStatus } from '../../../Settings/Connectors/Connectors';
import { getStatusIcon } from '../../../Settings/Connectors/ConnectorStatusUtils';
import { EntityTable } from '../Common/EntityTable';
import { ToolTableToolbar } from '../Common/ToolTableToolbar';
import { ALL_FILTER_KEY, BaseTableItem, EntityTableProps, KustoToolItem } from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';
import { useKustoToolConnectorStatus } from '../Hooks/useKustoToolConnectorStatus';

interface KustoToolTableProps extends EntityTableProps {
    kustoTools: ExtendedTool[];
    connectors: ExtendedConnector[];
}

export const KustoToolTable: FC<KustoToolTableProps> = ({ kustoTools, openInfoPanel, refresh, lastUpdated, isLoading }) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const [searchText, setSearchText] = useState<string>();
    const [connectorFilter, setConnectorFilter] = useState<string>(ALL_FILTER_KEY);
    const [connectorStatusFilter, setConnectorStatusFilter] = useState<string>(ALL_FILTER_KEY);
    const [selectedTools, setSelectedTools] = useState<ExtendedTool[]>([]);

    const { connectionMap, loadingStatusMap } = useKustoToolConnectorStatus(kustoTools);

    const EMPTY_DISPLAY = useMemo(() => intl.formatMessage(SreAgentResources.none), [intl]);

    const kustoToolItems = useMemo(() => {
        const query = searchText?.trim().toLowerCase();
        let filteredTools = kustoTools;

        if (query) {
            filteredTools = filteredTools.filter(tool => tool.name?.toLowerCase().includes(query));
        }

        if (connectorFilter !== ALL_FILTER_KEY) {
            filteredTools = filteredTools.filter(tool => tool.connector?.toLowerCase().includes(connectorFilter.toLowerCase()));
        }

        if (connectorStatusFilter !== ALL_FILTER_KEY) {
            filteredTools = filteredTools.filter(tool => {
                const connectorName = tool.connector;
                if (!connectorName) return false;
                const connectorStatus = connectionMap[connectorName]?.status || '';
                return connectorStatus === connectorStatusFilter;
            });
        }

        return filteredTools.map(tool => {
            const parameterCount = tool.parameters?.length || 0;
            const parametersText = parameterCount > 0 ? `${parameterCount}` : EMPTY_DISPLAY;
            const connectorName = tool.connector;
            const connectorStatus = connectorName ? connectionMap[connectorName]?.status || EMPTY_DISPLAY : EMPTY_DISPLAY;

            return {
                name: tool.name || EMPTY_DISPLAY,
                connector: tool.connector || EMPTY_DISPLAY,
                database: tool.database || EMPTY_DISPLAY,
                parameters: parametersText,
                connectorStatus,
                data: tool,
            };
        });
    }, [searchText, kustoTools, connectorFilter, connectorStatusFilter, EMPTY_DISPLAY, connectionMap]);

    const renderTableHeaders = useCallback(() => {
        return (
            <>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoToolName)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.connector)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoDatabaseLabel)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.connectorStatus)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}
                </TableHeaderCell>
            </>
        );
    }, [intl, styles.tableHeader]);

    const renderStatusCell = useCallback(
        (connectorName: string, status: string) => {
            const isLoadingThisStatus = loadingStatusMap[connectorName] ?? true;
            if (isLoadingThisStatus) {
                return (
                    <Skeleton>
                        <SkeletonItem style={{ width: '120px', height: '20px' }} />
                    </Skeleton>
                );
            }

            if (status === EMPTY_DISPLAY) {
                return <Text>{status}</Text>;
            }

            const { icon } = getStatusIcon(status);
            return (
                <div className={styles.flexRowMedium}>
                    {icon}
                    <Text>{status}</Text>
                </div>
            );
        },
        [loadingStatusMap, styles.flexRowMedium, EMPTY_DISPLAY]
    );

    const renderTableCells = useCallback(
        (item: BaseTableItem) => {
            const toolItem = item as KustoToolItem;
            const connectorName = toolItem.data?.connector || '';
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
                        <Text>{toolItem.connector}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{toolItem.database}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        {renderStatusCell(connectorName, toolItem.connectorStatus)}
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{toolItem.parameters}</Text>
                    </TableCell>
                </>
            );
        },
        [styles, openInfoPanel, renderStatusCell]
    );

    const connectorFilterOptions = useMemo(() => {
        const uniqueConnectorNames = Array.from(new Set(kustoTools.map(tool => tool.connector)));

        return [
            {
                key: ALL_FILTER_KEY,
                label: intl.formatMessage(SreAgentResources.all),
            },
            ...uniqueConnectorNames.map(connector => ({
                key: connector ?? '',
                label: connector ?? '',
            })),
        ];
    }, [kustoTools, intl]);

    const connectorStatusFilterOptions = useMemo(
        () => [
            { key: ALL_FILTER_KEY, label: intl.formatMessage(SreAgentResources.all) },
            { key: McpConnectorStatus.Connected, label: McpConnectorStatus.Connected },
            { key: McpConnectorStatus.Disconnected, label: McpConnectorStatus.Disconnected },
            { key: McpConnectorStatus.Error, label: McpConnectorStatus.Error },
            { key: McpConnectorStatus.Failed, label: McpConnectorStatus.Failed },
            { key: McpConnectorStatus.Initializing, label: McpConnectorStatus.Initializing },
        ],
        [intl]
    );

    const connectorFilters = useMemo(
        () => (
            <>
                <PillFilter
                    label={`${intl.formatMessage(ExtendedAgentsGraphResources.connector)}`}
                    filterType="combobox"
                    options={connectorFilterOptions}
                    selectedKeys={[connectorFilter]}
                    onApply={keys => {
                        setConnectorFilter(keys[0]);
                    }}
                />
                <PillFilter
                    label={`${intl.formatMessage(ExtendedAgentsGraphResources.connectorStatus)}`}
                    filterType="combobox"
                    options={connectorStatusFilterOptions}
                    selectedKeys={[connectorStatusFilter]}
                    onApply={keys => {
                        setConnectorStatusFilter(keys[0]);
                    }}
                />
            </>
        ),
        [intl, connectorFilterOptions, connectorStatusFilterOptions, connectorFilter, connectorStatusFilter]
    );

    return (
        <div className={styles.entityTable}>
            <ToolTableToolbar
                toolType="kusto"
                selectedTools={selectedTools}
                searchText={searchText}
                setSearchText={setSearchText}
                searchPlaceholder={ExtendedAgentsGraphResources.searchByTool}
                refresh={refresh}
                lastUpdated={lastUpdated}
                additionalFilters={connectorFilters}
            />
            <EntityTable
                activeTab="scheduledTasks"
                searchText={searchText}
                items={kustoToolItems}
                setSelectedItems={(items: BaseTableItem[]) => setSelectedTools(items as ExtendedTrigger[])}
                renderTableHeaders={renderTableHeaders}
                renderTableCells={renderTableCells}
                isLoading={isLoading}
            />
        </div>
    );
};
