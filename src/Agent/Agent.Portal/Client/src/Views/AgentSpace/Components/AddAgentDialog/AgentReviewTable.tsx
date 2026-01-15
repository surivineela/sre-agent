import {
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    makeStyles,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    tokens,
} from '@fluentui/react-components';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentArgItem } from '../../../../Common/Contracts/SreAgent';
import { getUserFriendlyLocation } from '../../../../Common/Utilities/Location';
import { safeCompare } from '../../../../Common/Utilities/String';
import { PortalResources } from '../../../../Strings/Resources';

const useStyles = makeStyles({
    dataGrid: {
        maxHeight: '300px',
        overflowY: 'auto',
    },
    selectionCount: {
        color: tokens.colorNeutralForeground3,
        paddingTop: tokens.spacingVerticalS,
    },
});

interface AgentReviewTableProps {
    selectedAgents: SreAgentArgItem[];
    subscriptions: Array<{ subscriptionId: string; displayName: string }>;
}

export const AgentReviewTable = ({ selectedAgents, subscriptions }: AgentReviewTableProps) => {
    const intl = useIntl();
    const styles = useStyles();

    const subscriptionDisplayNameMap = useMemo(() => {
        return new Map(subscriptions.map(sub => [sub.subscriptionId, sub.displayName]));
    }, [subscriptions]);

    const columns: TableColumnDefinition<SreAgentArgItem>[] = useMemo(
        () => [
            createTableColumn<SreAgentArgItem>({
                columnId: 'name',
                compare: (a, b) => safeCompare(a.name, b.name),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.name)}</Text>,
                renderCell: item => <TableCellLayout>{item.name}</TableCellLayout>,
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'subscription',
                compare: (a, b) => safeCompare(a.subscriptionId, b.subscriptionId),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.subscription)}</Text>,
                renderCell: item => (
                    <TableCellLayout>{subscriptionDisplayNameMap.get(item.subscriptionId) || item.subscriptionId}</TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'resourceGroup',
                compare: (a, b) => safeCompare(a.resourceGroup, b.resourceGroup),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.resourceGroup)}</Text>,
                renderCell: item => <TableCellLayout>{item.resourceGroup}</TableCellLayout>,
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'location',
                compare: (a, b) => safeCompare(a.location, b.location),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.region)}</Text>,
                renderCell: item => <TableCellLayout>{getUserFriendlyLocation(item.location)}</TableCellLayout>,
            }),
        ],
        [intl, subscriptionDisplayNameMap]
    );

    return (
        <div>
            <DataGrid items={selectedAgents} columns={columns} sortable getRowId={item => item.id} className={styles.dataGrid}>
                <DataGridHeader>
                    <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
                </DataGridHeader>
                <DataGridBody<SreAgentArgItem>>
                    {({ item, rowId }) => (
                        <DataGridRow<SreAgentArgItem> key={rowId}>
                            {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                        </DataGridRow>
                    )}
                </DataGridBody>
            </DataGrid>
            <Text size={200} className={styles.selectionCount}>
                {intl.formatMessage(PortalResources.nSelected, { count: selectedAgents.length })}
            </Text>
        </div>
    );
};
