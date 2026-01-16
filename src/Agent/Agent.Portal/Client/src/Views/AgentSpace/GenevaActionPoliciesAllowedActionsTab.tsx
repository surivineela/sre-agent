import {
    Button,
    Checkbox,
    makeStyles,
    Table,
    TableBody,
    TableCell,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AgentSpaceClient } from '../../Common/Clients/AgentSpaceClient';
import { AddAllowedActionsDialog } from '../../Common/Components/GenevaActions/AddAllowedActionsDialog';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { AgentSpace, AgentSpaceAllowedAction } from '../../Common/Contracts/AgentSpace';
import { ArmObj } from '../../Common/Contracts/Arm';
import { PortalResources, RolesAndPermissions } from '../../Strings/Resources';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    description: {
        color: tokens.colorNeutralForeground2,
        marginBottom: tokens.spacingVerticalS,
    },
    toolbar: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        marginBottom: tokens.spacingVerticalS,
    },
    divider: {
        width: '1px',
        height: '16px',
        backgroundColor: tokens.colorNeutralStroke2,
        marginLeft: tokens.spacingHorizontalS,
        marginRight: tokens.spacingHorizontalS,
    },
    button: {
        fontWeight: 400,
    },
    tableWrapper: {
        overflowX: 'auto',
    },
    tableHeaderCell: {
        fontWeight: tokens.fontWeightSemibold,
    },
    emptyState: {
        textAlign: 'center',
        padding: tokens.spacingVerticalXXL,
        color: tokens.colorNeutralForeground2,
    },
    checkboxCell: {
        width: '40px',
    },
});

interface GenevaActionPoliciesAllowedActionsTabProps {
    agentSpace: ArmObj<AgentSpace> | null;
    refresh: () => Promise<void>;
    disabled: boolean;
}

export const GenevaActionPoliciesAllowedActionsTab = ({ agentSpace, refresh, disabled }: GenevaActionPoliciesAllowedActionsTabProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { start, succeed, fail } = useNotifications();

    const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
    const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [isRefreshing, setIsRefreshing] = useState(false);

    const client = useMemo(() => AgentSpaceClient.getInstance(TelemetrySource.AgentSpaceView), []);

    // Create rows with unique keys
    const rows = useMemo(() => {
        const allowedActions = agentSpace?.properties?.policies?.genevaActionsConfiguration?.allowedActions ?? [];
        return allowedActions.map((action, index) => ({
            key: `${action.extension}:${action.actionName}:${index}`,
            action,
        }));
    }, [agentSpace?.properties?.policies?.genevaActionsConfiguration?.allowedActions]);

    // Selection state
    const allSelected = useMemo(() => rows.length > 0 && rows.every(r => selectedKeys.has(r.key)), [rows, selectedKeys]);
    const someSelected = useMemo(() => rows.some(r => selectedKeys.has(r.key)) && !allSelected, [rows, selectedKeys, allSelected]);
    const hasSelection = selectedKeys.size > 0;

    const toggleAll = useCallback(() => {
        setSelectedKeys(prev => {
            const next = new Set(prev);
            if (allSelected) {
                rows.forEach(r => next.delete(r.key));
            } else {
                rows.forEach(r => next.add(r.key));
            }
            return next;
        });
    }, [allSelected, rows]);

    const toggleRow = useCallback((key: string) => {
        setSelectedKeys(prev => {
            const next = new Set(prev);
            if (next.has(key)) {
                next.delete(key);
            } else {
                next.add(key);
            }
            return next;
        });
    }, []);

    // Add actions handler
    const handleAddActions = useCallback(
        async (newActions: AgentSpaceAllowedAction[]) => {
            if (!agentSpace) return;

            setIsSubmitting(true);

            const notificationId = start(
                intl.formatMessage(PortalResources.updateAgentSpace),
                intl.formatMessage(PortalResources.updatingAgentSpace)
            );

            const existingConfig = agentSpace.properties?.policies?.genevaActionsConfiguration;
            const updatedActions = [...(existingConfig?.allowedActions ?? []), ...newActions];

            const response = await client.updateAgentSpace(agentSpace.id, {
                policies: {
                    genevaActionsConfiguration: {
                        ...existingConfig,
                        allowedActions: updatedActions,
                    },
                },
            });

            if (response.isSuccessful) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.updateAgentSpace),
                    intl.formatMessage(PortalResources.updateAgentSpaceSuccess)
                );
                setIsAddDialogOpen(false);
                await refresh();
            } else {
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.updateAgentSpace),
                    intl.formatMessage(PortalResources.updateAgentSpaceError)
                );
            }

            setIsSubmitting(false);
        },
        [agentSpace, client, intl, start, succeed, fail, refresh]
    );

    // Delete actions handler
    const handleDeleteActions = useCallback(async () => {
        if (!agentSpace || selectedKeys.size === 0) return;

        setIsSubmitting(true);

        const notificationId = start(
            intl.formatMessage(PortalResources.updateAgentSpace),
            intl.formatMessage(PortalResources.updatingAgentSpace)
        );

        const existingConfig = agentSpace.properties?.policies?.genevaActionsConfiguration;
        const keysToDelete = new Set(selectedKeys);

        // Filter out the selected actions
        const updatedActions = (existingConfig?.allowedActions ?? []).filter((action, index) => {
            const key = `${action.extension}:${action.actionName}:${index}`;
            return !keysToDelete.has(key);
        });

        const response = await client.updateAgentSpace(agentSpace.id, {
            policies: {
                genevaActionsConfiguration: {
                    ...existingConfig,
                    allowedActions: updatedActions,
                },
            },
        });

        if (response.isSuccessful) {
            succeed(
                notificationId,
                intl.formatMessage(PortalResources.updateAgentSpace),
                intl.formatMessage(PortalResources.updateAgentSpaceSuccess)
            );
            setSelectedKeys(new Set());
            await refresh();
        } else {
            fail(
                notificationId,
                intl.formatMessage(PortalResources.updateAgentSpace),
                intl.formatMessage(PortalResources.updateAgentSpaceError)
            );
        }

        setIsSubmitting(false);
    }, [agentSpace, client, selectedKeys, intl, start, succeed, fail, refresh]);

    // Refresh handler
    const handleRefresh = useCallback(async () => {
        setIsRefreshing(true);
        await refresh();
        setIsRefreshing(false);
    }, [refresh]);

    if (!agentSpace) {
        return null;
    }

    return (
        <div className={styles.container}>
            <Text className={styles.description}>{intl.formatMessage(PortalResources.allowedActionsDescription)}</Text>

            <div className={styles.toolbar} role="toolbar" aria-label={intl.formatMessage(RolesAndPermissions.allowedActions)}>
                <Tooltip content={intl.formatMessage(PortalResources.addAllowedActions)} relationship="label">
                    <Button
                        appearance="transparent"
                        icon={<Add16Regular />}
                        onClick={() => setIsAddDialogOpen(true)}
                        disabled={disabled || isSubmitting}
                        className={styles.button}
                    >
                        {intl.formatMessage(PortalResources.addAllowedActions)}
                    </Button>
                </Tooltip>

                <Tooltip content={intl.formatMessage(PortalResources.refresh)} relationship="label">
                    <Button
                        appearance="transparent"
                        icon={<ArrowClockwise16Regular />}
                        onClick={handleRefresh}
                        disabled={disabled || isRefreshing || isSubmitting}
                        className={styles.button}
                    >
                        {intl.formatMessage(PortalResources.refresh)}
                    </Button>
                </Tooltip>

                <div className={styles.divider} />

                <Tooltip content={intl.formatMessage(PortalResources.delete)} relationship="label">
                    <Button
                        appearance="transparent"
                        icon={<Delete16Regular />}
                        onClick={handleDeleteActions}
                        disabled={disabled || !hasSelection || isSubmitting}
                        className={styles.button}
                    >
                        {intl.formatMessage(PortalResources.delete)}
                    </Button>
                </Tooltip>
            </div>

            {rows.length > 0 ? (
                <div className={styles.tableWrapper}>
                    <Table aria-label={intl.formatMessage(RolesAndPermissions.allowedActions)} size="medium">
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell className={styles.checkboxCell}>
                                    <Checkbox
                                        aria-label={intl.formatMessage(PortalResources.selectAll)}
                                        checked={allSelected ? true : someSelected ? 'mixed' : false}
                                        onChange={toggleAll}
                                        disabled={disabled || isSubmitting}
                                    />
                                </TableHeaderCell>
                                <TableHeaderCell className={styles.tableHeaderCell}>
                                    {intl.formatMessage(RolesAndPermissions.extensionName)}
                                </TableHeaderCell>
                                <TableHeaderCell className={styles.tableHeaderCell}>
                                    {intl.formatMessage(PortalResources.actionName)}
                                </TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {rows.map(r => {
                                const selected = selectedKeys.has(r.key);
                                return (
                                    <TableRow
                                        key={r.key}
                                        aria-selected={selected}
                                        onClick={() => !disabled && !isSubmitting && toggleRow(r.key)}
                                        style={{ cursor: disabled || isSubmitting ? 'default' : 'pointer' }}
                                    >
                                        <TableCell className={styles.checkboxCell}>
                                            <Checkbox
                                                aria-label={`${r.action.extension}:${r.action.actionName}`}
                                                checked={selected}
                                                onChange={() => toggleRow(r.key)}
                                                onClick={e => e.stopPropagation()}
                                                disabled={disabled || isSubmitting}
                                            />
                                        </TableCell>
                                        <TableCell>{r.action.extension}</TableCell>
                                        <TableCell>{r.action.actionName}</TableCell>
                                    </TableRow>
                                );
                            })}
                        </TableBody>
                    </Table>
                </div>
            ) : (
                <div className={styles.emptyState} role="status" aria-live="polite">
                    <Text>{intl.formatMessage(PortalResources.noAllowedActionsConfigured)}</Text>
                </div>
            )}

            <AddAllowedActionsDialog
                open={isAddDialogOpen}
                onOpenChange={setIsAddDialogOpen}
                onAdd={handleAddActions}
                isSubmitting={isSubmitting}
            />
        </div>
    );
};
