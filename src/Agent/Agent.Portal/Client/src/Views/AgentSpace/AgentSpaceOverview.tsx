import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Divider,
    Image,
    Link,
    makeStyles,
    Spinner,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    tokens,
} from '@fluentui/react-components';
import {
    Add16Regular,
    ArrowClockwise16Regular,
    Code16Regular,
    Delete16Regular,
    Open16Regular,
    Play16Regular,
    Stop16Regular,
} from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useSubscriptions } from '../../Common/Contexts/SubscriptionsContext';
import { AgentSpace } from '../../Common/Contracts/AgentSpace';
import { ArmObj } from '../../Common/Contracts/Arm';
import { SreAgentArgItem } from '../../Common/Contracts/SreAgent';
import { usePersistentNavigate } from '../../Common/Hooks/usePersistentNavigate';
import { parseArmId } from '../../Common/Utilities/ArmId';
import { getUserFriendlyLocation } from '../../Common/Utilities/Location';
import { safeCompare } from '../../Common/Utilities/String';
import { openResourceGroupOverviewInNewTab, openSubscriptionOverviewInNewTab } from '../../Common/Utilities/Url';
import { PortalResources } from '../../Strings/Resources';
import { RemoveAgentDialog } from './Components/RemoveAgentDialog';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        padding: '20px',
    },
    actionBar: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        paddingBottom: tokens.spacingVerticalM,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        marginBottom: tokens.spacingVerticalM,
    },
    twoColumnLayout: {
        display: 'flex',
        flexDirection: 'row',
        gap: '24px',
        '@media (max-width: 1050px)': {
            flexDirection: 'column-reverse',
        },
    },
    leftColumn: {
        flex: 2,
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        minWidth: 0,
    },
    rightColumn: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        minWidth: '280px',
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    sectionHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
    },
    detailsGrid: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        '@media (max-width: 1050px)': {
            flexDirection: 'row',
            flexWrap: 'wrap',
        },
    },
    essentialItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    label: {
        color: tokens.colorNeutralForeground3,
    },
    linkWithIcon: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '48px',
        gap: '16px',
        color: tokens.colorNeutralForeground3,
    },
    dataGrid: {
        maxHeight: '400px',
        overflowY: 'auto',
    },
});

interface AgentSpaceOverviewProps {
    agentSpace: ArmObj<AgentSpace> | null;
    memberAgents: SreAgentArgItem[];
    isLoadingAgents: boolean;
    refreshAgents: () => Promise<void>;
    onRefresh: () => void;
    onViewJson: () => void;
    onDelete: () => void;
    onAddAgent: () => void;
    onStartAgents: (agentIds: string[]) => Promise<void>;
    onStopAgents: (agentIds: string[]) => Promise<void>;
    onRemoveAgents: (agentIds: string[]) => Promise<void>;
}

export const AgentSpaceOverview = ({
    agentSpace,
    memberAgents,
    isLoadingAgents,
    refreshAgents,
    onRefresh,
    onViewJson,
    onDelete,
    onAddAgent,
    onStartAgents,
    onStopAgents,
    onRemoveAgents,
}: AgentSpaceOverviewProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const navigate = usePersistentNavigate();
    const { subscriptions } = useSubscriptions();
    const [selectedAgentIds, setSelectedAgentIds] = useState<Set<string>>(new Set());
    const [showRemoveDialog, setShowRemoveDialog] = useState(false);

    const onSelectionChange = useCallback((_: unknown, data: { selectedItems: Set<string | number> }) => {
        setSelectedAgentIds(data.selectedItems as Set<string>);
    }, []);

    const hasSelection = selectedAgentIds.size > 0;

    const selectedAgentNames = useMemo(() => {
        return memberAgents.filter(agent => selectedAgentIds.has(agent.id)).map(agent => agent.name);
    }, [memberAgents, selectedAgentIds]);

    const handleStartAgents = useCallback(async () => {
        await onStartAgents(Array.from(selectedAgentIds));
        setSelectedAgentIds(new Set());
    }, [onStartAgents, selectedAgentIds]);

    const handleStopAgents = useCallback(async () => {
        await onStopAgents(Array.from(selectedAgentIds));
        setSelectedAgentIds(new Set());
    }, [onStopAgents, selectedAgentIds]);

    const handleRemoveAgents = useCallback(async () => {
        await onRemoveAgents(Array.from(selectedAgentIds));
        setSelectedAgentIds(new Set());
    }, [onRemoveAgents, selectedAgentIds]);

    const parsedId = useMemo(() => (agentSpace ? parseArmId(agentSpace.id) : null), [agentSpace]);

    const subscriptionDisplayNameMap = useMemo(() => {
        return new Map(subscriptions.map(sub => [sub.subscriptionId, sub.displayName]));
    }, [subscriptions]);

    const columns: TableColumnDefinition<SreAgentArgItem>[] = useMemo(
        () => [
            createTableColumn<SreAgentArgItem>({
                columnId: 'name',
                compare: (a, b) => safeCompare(a.name, b.name),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.name)}</Text>,
                renderCell: item => (
                    <TableCellLayout
                        media={<Image src="/SreAgent.svg" width={16} height={16} alt={intl.formatMessage(PortalResources.agent)} />}
                    >
                        <Link onClick={() => navigate(`/agents/${encodeURIComponent(item.id)}`)}>{item.name}</Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'subscription',
                compare: (a, b) => safeCompare(a.subscriptionId, b.subscriptionId),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.subscription)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Link onClick={() => openSubscriptionOverviewInNewTab(item.subscriptionId)}>
                            {subscriptionDisplayNameMap.get(item.subscriptionId) || item.subscriptionId}
                        </Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'resourceGroup',
                compare: (a, b) => safeCompare(a.resourceGroup, b.resourceGroup),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.resourceGroup)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Link onClick={() => openResourceGroupOverviewInNewTab(item.id)}>{item.resourceGroup}</Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'powerState',
                compare: (a, b) => safeCompare(a.powerState, b.powerState),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.powerState)}</Text>,
                renderCell: item => <TableCellLayout>{item.powerState || '-'}</TableCellLayout>,
            }),
        ],
        [intl, navigate, subscriptionDisplayNameMap]
    );

    if (!agentSpace) {
        return null;
    }

    return (
        <div className={styles.container}>
            <div className={styles.actionBar}>
                <Button icon={<ArrowClockwise16Regular />} appearance="subtle" onClick={onRefresh}>
                    {intl.formatMessage(PortalResources.refresh)}
                </Button>
                <Button icon={<Code16Regular />} appearance="subtle" onClick={onViewJson}>
                    {intl.formatMessage(PortalResources.viewJson)}
                </Button>
                <Button icon={<Delete16Regular />} appearance="subtle" onClick={onDelete}>
                    {intl.formatMessage(PortalResources.delete)}
                </Button>
            </div>

            <div className={styles.twoColumnLayout}>
                <div className={styles.leftColumn}>
                    <Text size={400} weight="semibold">
                        {intl.formatMessage(PortalResources.agents)}
                    </Text>
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap' }}>
                        <Button icon={<Add16Regular />} appearance="subtle" onClick={onAddAgent}>
                            {intl.formatMessage(PortalResources.addExistingAgent)}
                        </Button>
                        <Button icon={<ArrowClockwise16Regular />} appearance="subtle" onClick={refreshAgents} disabled={isLoadingAgents}>
                            {intl.formatMessage(PortalResources.refresh)}
                        </Button>
                        <Divider vertical style={{ height: '20px', flexGrow: 0, flexShrink: 0 }} />
                        <Button icon={<Play16Regular />} appearance="subtle" disabled={!hasSelection} onClick={handleStartAgents}>
                            {intl.formatMessage(PortalResources.start)}
                        </Button>
                        <Button icon={<Stop16Regular />} appearance="subtle" disabled={!hasSelection} onClick={handleStopAgents}>
                            {intl.formatMessage(PortalResources.stop)}
                        </Button>
                        <Button
                            icon={<Delete16Regular />}
                            appearance="subtle"
                            disabled={!hasSelection}
                            onClick={() => setShowRemoveDialog(true)}
                        >
                            {intl.formatMessage(PortalResources.remove)}
                        </Button>
                    </div>

                    {isLoadingAgents ? (
                        <div className={styles.emptyState}>
                            <Spinner size="medium" />
                        </div>
                    ) : memberAgents.length === 0 ? (
                        <div className={styles.emptyState}>
                            <Text>{intl.formatMessage(PortalResources.noMemberAgents)}</Text>
                        </div>
                    ) : (
                        <DataGrid
                            items={memberAgents}
                            columns={columns}
                            sortable
                            getRowId={item => item.id}
                            className={styles.dataGrid}
                            selectionMode="multiselect"
                            selectedItems={selectedAgentIds}
                            onSelectionChange={onSelectionChange}
                        >
                            <DataGridHeader>
                                <DataGridRow
                                    selectionCell={{
                                        checkboxIndicator: { 'aria-label': intl.formatMessage(PortalResources.selectAll) },
                                    }}
                                >
                                    {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                                </DataGridRow>
                            </DataGridHeader>
                            <DataGridBody<SreAgentArgItem>>
                                {({ item, rowId }) => (
                                    <DataGridRow<SreAgentArgItem>
                                        key={rowId}
                                        selectionCell={{
                                            checkboxIndicator: {
                                                'aria-label': intl.formatMessage(PortalResources.selectAgent, { name: item.name }),
                                            },
                                        }}
                                    >
                                        {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                    </DataGridRow>
                                )}
                            </DataGridBody>
                        </DataGrid>
                    )}
                </div>

                <div className={styles.rightColumn}>
                    <Text size={400} weight="semibold">
                        {intl.formatMessage(PortalResources.details)}
                    </Text>
                    <div className={styles.detailsGrid}>
                        <div className={styles.essentialItem}>
                            <Text size={200} className={styles.label}>
                                {intl.formatMessage(PortalResources.subscription)}
                            </Text>
                            <Link
                                onClick={() => parsedId && openSubscriptionOverviewInNewTab(parsedId.subscription)}
                                className={styles.linkWithIcon}
                            >
                                {subscriptionDisplayNameMap.get(parsedId?.subscription || '') || parsedId?.subscription}
                                <Open16Regular />
                            </Link>
                        </div>
                        <div className={styles.essentialItem}>
                            <Text size={200} className={styles.label}>
                                {intl.formatMessage(PortalResources.resourceGroup)}
                            </Text>
                            <Link
                                onClick={() => agentSpace && openResourceGroupOverviewInNewTab(agentSpace.id)}
                                className={styles.linkWithIcon}
                            >
                                {parsedId?.resourceGroup}
                                <Open16Regular />
                            </Link>
                        </div>
                        <div className={styles.essentialItem}>
                            <Text size={200} className={styles.label}>
                                {intl.formatMessage(PortalResources.region)}
                            </Text>
                            <Text>{getUserFriendlyLocation(agentSpace.location)}</Text>
                        </div>
                        <div className={styles.essentialItem}>
                            <Text size={200} className={styles.label}>
                                {intl.formatMessage(PortalResources.provisioningState)}
                            </Text>
                            <Text>{agentSpace.properties?.provisioningState || '-'}</Text>
                        </div>
                        <div className={styles.essentialItem}>
                            <Text size={200} className={styles.label}>
                                {intl.formatMessage(PortalResources.currentAgentCount)}
                            </Text>
                            <Text>{agentSpace.properties?.currentAgentCount ?? 0}</Text>
                        </div>
                        <div className={styles.essentialItem}>
                            <Text size={200} className={styles.label}>
                                {intl.formatMessage(PortalResources.maxAgentCount)}
                            </Text>
                            <Text>{agentSpace.properties?.maxAgentCount ?? '-'}</Text>
                        </div>
                    </div>
                </div>
            </div>

            <RemoveAgentDialog
                open={showRemoveDialog}
                agentNames={selectedAgentNames}
                onClose={() => setShowRemoveDialog(false)}
                onConfirm={handleRemoveAgents}
            />
        </div>
    );
};
