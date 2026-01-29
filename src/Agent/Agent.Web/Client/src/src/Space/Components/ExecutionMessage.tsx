import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Badge,
    Button,
    Caption1,
    Divider,
    Spinner,
    Text,
    Tooltip,
    makeStyles,
    mergeClasses,
    tokens,
} from '@fluentui/react-components';
import {
    CheckmarkCircle16Regular,
    ChevronDownUp16Regular,
    ChevronUpDown16Regular,
    Dismiss16Regular,
    DismissCircle16Filled,
} from '@fluentui/react-icons';
import { useContext, useEffect, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getDataPlaneErrorMessage } from '../../Common/Clients/DataPlaneClient';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import CopyButton from '../../Common/Components/CopyButton';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { Approval, AzCliExecution, ExecutionStatus, KubectlExecution, PsqlExecution } from '../../Common/Contracts/DataPlane/Message';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { usePermissionContext } from '../Contracts/PermissionContext';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { ApprovalTimestamps } from './ApprovalTimestamps';
import { getRiskLevel } from './Utility';

/* Current API notes:
    - Pending vs PendingAuthorization -> agent vs user creds
    - Canceled -> cancelling user returned but not stored
    - Running/Completed/Failed ->
        - executedBy -> SREAgent vs user identity
        - Doesn't store who approved it if agent creds
        - The initial POST action returns the user id that was sent
        regardless of trying agent creds first, so don't trust
*/

const SreAgentDisplayName = 'SRE Agent Client';

export enum ExecutionMessageType {
    AzCli = 'azCli',
    Kubectl = 'kubectl',
}

type ExecutionLike = AzCliExecution | KubectlExecution;

type ExecutionMessageProps = {
    execution: ExecutionLike;
    threadId: string;
    type: ExecutionMessageType;
    updateApprovalOrCliMessageInStreamingMessage?: (approvalOrCliMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
        psqlExecution?: PsqlExecution;
    }) => void;
};

const useStyles = makeStyles({
    card: {
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: '16px',
        transitionProperty: 'background-color, border-color',
        transitionDuration: '0.15s',
        transitionTimingFunction: 'ease',
    },
    codeBlock: {
        position: 'relative',
        border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStrokeDisabled}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground2,
        padding: '10px',
        fontFamily: 'Consolas, Monaco, monospace',
        color: tokens.colorNeutralForeground2,
    },
    copyButton: {
        position: 'absolute',
        top: '6px',
        right: '6px',
    },
    headerRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        columnGap: '8px',
        rowGap: '8px',
        flexWrap: 'wrap',
    },
    summaryLeft: {
        display: 'flex',
        alignItems: 'center',
        columnGap: '8px',
        rowGap: '8px',
        flexWrap: 'wrap',
    },
    infoLine: {
        display: 'flex',
        alignItems: 'center',
        columnGap: '8px',
        color: tokens.colorNeutralForeground2,
    },
    outputPre: {
        margin: 0,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        width: 'calc(100% - 32px)',
    },
    outputPreCollapsed: {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: 'block',
    },
    statusBadge: {
        minWidth: '24px',
        radius: tokens.borderRadiusLarge,
        height: '24px',
        overflowWrap: 'normal',
    },
});

const ExecutionMessage = ({ execution, threadId, type, updateApprovalOrCliMessageInStreamingMessage }: ExecutionMessageProps) => {
    const { userIdAndDisplayName } = useAuthenticatedUserInfo();
    const classes = useStyles();
    const intl = useIntl();
    const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalProxy = useAzPortalContext();
    const { canApproveThreads } = usePermissionContext();

    const [currentExecution, setCurrentExecution] = useState<ExecutionLike>(execution);
    const [isActionLoading, setIsActionLoading] = useState(false);
    const [loadingAction, setLoadingAction] = useState<'run' | 'cancel' | null>(null);
    const [isCollapsed, setIsCollapsed] = useState(
        execution.status !== ExecutionStatus.Pending && execution.status !== ExecutionStatus.PendingAuthorization
    );

    const threadClient = useMemo(() => ThreadClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

    const riskLevel = useMemo(() => getRiskLevel(currentExecution.command, intl), [currentExecution.command, intl]);
    const executedByName = useMemo(() => currentExecution.executedBy?.displayName, [currentExecution.executedBy]);
    const isExecutedWithUserPerms = useMemo(() => executedByName && executedByName !== SreAgentDisplayName, [executedByName]);

    const { basePath, executionTypeLabel } = useMemo(() => {
        switch (type) {
            case ExecutionMessageType.Kubectl:
                return {
                    basePath: 'kubectlExecution' as const,
                    executionTypeLabel: 'Kubernetes',
                };
            case ExecutionMessageType.AzCli:
            default:
                return {
                    basePath: 'azCliExecution' as const,
                    executionTypeLabel: 'Azure CLI',
                };
        }
    }, [type]);

    const isCurrentExecutionPending = useMemo(() => {
        return currentExecution.status === ExecutionStatus.Pending || currentExecution.status === ExecutionStatus.PendingAuthorization;
    }, [currentExecution.status]);

    const statusBadge = useMemo(() => {
        switch (currentExecution.status) {
            case ExecutionStatus.Completed:
                return (
                    <Badge color="success" icon={<CheckmarkCircle16Regular />} className={classes.statusBadge}>
                        {!isCollapsed && <FormattedMessage {...SreAgentResources.completed} />}
                    </Badge>
                );
            case ExecutionStatus.Failed:
                return (
                    <Badge color="danger" size="large" icon={<DismissCircle16Filled />} className={classes.statusBadge}>
                        {!isCollapsed && <FormattedMessage {...SreAgentResources.failed} />}
                    </Badge>
                );
            case ExecutionStatus.Running:
                return (
                    <Badge appearance="outline" color="informative" icon={<Spinner size="extra-tiny" />} className={classes.statusBadge}>
                        {!isCollapsed && <FormattedMessage {...SreAgentResources.running} />}
                    </Badge>
                );
            case ExecutionStatus.Cancelled:
                return (
                    <Badge color="informative" icon={<Dismiss16Regular />} className={classes.statusBadge}>
                        {!isCollapsed && <FormattedMessage {...SreAgentResources.canceled} />}
                    </Badge>
                );
            default:
                return null;
        }
    }, [currentExecution.status, classes.statusBadge, isCollapsed]);

    const getCombinedOutput = (): string => {
        let outputText = '';
        if (currentExecution.output) {
            outputText += currentExecution.output;
        }
        if (currentExecution.error) {
            if (outputText) outputText += '\n\n';
            outputText += `Error: ${currentExecution.error}`;
        }
        return outputText;
    };

    useEffect(() => {
        setCurrentExecution(execution);
    }, [execution]);

    useEffect(() => {
        if (currentExecution.status !== ExecutionStatus.Running) {
            return;
        }

        let isPolling = true;
        const pollInterval: NodeJS.Timeout = setInterval(async () => {
            if (!isPolling) return;

            try {
                const client = ThreadClient.getInstance(sreAgentEndpoint);
                const result = await client.getExecutionStatus(basePath as any, threadId, execution.id);
                if (!result.isSuccessful) {
                    azPortalProxy.log({
                        action: 'fetchExecutionStatus',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId,
                        data: {
                            error: getDataPlaneErrorMessage(result.error),
                        },
                    });
                    return;
                }
                const data = result.content || {};

                if (isPolling) {
                    const updatedExecution: ExecutionLike = {
                        ...currentExecution,
                        output: data.output ?? currentExecution.output,
                        status: data.status ?? currentExecution.status,
                        error: data.error ?? currentExecution.error,
                        completedTimestamp: data.completedTimestamp ?? currentExecution.completedTimestamp,
                        executedBy: data.executedBy ?? currentExecution.executedBy,
                    };

                    if (updateApprovalOrCliMessageInStreamingMessage) {
                        if (type === ExecutionMessageType.AzCli) {
                            updateApprovalOrCliMessageInStreamingMessage({ azCliExecution: updatedExecution as AzCliExecution });
                        } else {
                            updateApprovalOrCliMessageInStreamingMessage({ kubectlExecution: updatedExecution as KubectlExecution });
                        }
                    } else {
                        setCurrentExecution(updatedExecution);
                    }

                    if (data.completed) {
                        isPolling = false;
                        clearInterval(pollInterval);
                    }
                }
            } catch (error) {
                azPortalProxy.log({
                    action: 'fetchExecutionStatus',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId,
                    data: {
                        error,
                    },
                });
            }
        }, 2000);

        return () => {
            isPolling = false;
            clearInterval(pollInterval);
        };
    }, [
        currentExecution,
        execution.id,
        threadId,
        basePath,
        updateApprovalOrCliMessageInStreamingMessage,
        type,
        azPortalProxy,
        resourceId,
        sreAgentEndpoint,
    ]);

    const showOutputAccordion = currentExecution.status === ExecutionStatus.Completed || currentExecution.status === ExecutionStatus.Failed;

    const isKubectlExecution = (_e: ExecutionLike): _e is KubectlExecution => type === ExecutionMessageType.Kubectl;

    const isAzCliExecution = (_e: ExecutionLike): _e is AzCliExecution => type === ExecutionMessageType.AzCli;

    const handleAction = async (action: 'run' | 'cancel') => {
        setIsActionLoading(true);
        setLoadingAction(action);

        azPortalProxy.logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: `${action}Execution`,
            targetFriendlyName: `${action} execution`,
            valueObjectName: execution.description,
            valueObjectFriendlyName: execution.description,
            metadata: {
                permissions: execution.status === ExecutionStatus.Pending ? 'agent' : 'obo',
                type,
                threadId,
            },
        });

        const maxRetries = 3;
        const baseDelay = 1000;

        // Get required scopes for OBO flow if this is an AzCli execution in PendingAuthorization status
        const oboScope =
            currentExecution.status === ExecutionStatus.PendingAuthorization &&
            (isAzCliExecution(currentExecution) || isKubectlExecution(currentExecution))
                ? currentExecution.requiredScopes
                : undefined;

        for (let attempt = 0; attempt <= maxRetries; attempt++) {
            const result = await threadClient.postExecutionAction(
                basePath,
                threadId,
                execution.id,
                action,
                userIdAndDisplayName?.userId || 'sreagent-client',
                oboScope
            );

            if (result.isSuccessful && result.content) {
                const updatedExecution: ExecutionLike = {
                    ...currentExecution,
                    status: result.content.status,
                    startedTimestamp: result.content.startedTimestamp || currentExecution.startedTimestamp,
                    // Don't set executedBy here unless 'cancel' as it will always be the user you sent even if using agent creds
                    executedBy:
                        action === 'cancel' && result.content.executedBy
                            ? {
                                  displayName: result.content.executedBy,
                                  userId: result.content.executedById,
                                  role: 'User',
                              }
                            : currentExecution.executedBy,
                };

                if (updateApprovalOrCliMessageInStreamingMessage) {
                    if (type === ExecutionMessageType.AzCli) {
                        updateApprovalOrCliMessageInStreamingMessage({ azCliExecution: updatedExecution as AzCliExecution });
                    } else {
                        updateApprovalOrCliMessageInStreamingMessage({ kubectlExecution: updatedExecution as KubectlExecution });
                    }
                } else {
                    setCurrentExecution(updatedExecution);
                }

                break;
            } else {
                azPortalProxy.log({
                    action: 'runExecution',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId,
                    data: {
                        action: execution.description,
                        error: result.error,
                        attemptNum: attempt + 1,
                    },
                });

                if (attempt !== maxRetries) {
                    const delay = baseDelay * Math.pow(2, attempt);
                    await new Promise(resolve => setTimeout(resolve, delay));
                }
            }
        }

        setIsActionLoading(false);
        setLoadingAction(null);
    };

    return (
        <div className={classes.card}>
            <div className={classes.headerRow}>
                <div className={classes.summaryLeft}>
                    <Text weight="semibold">{currentExecution.description}</Text>
                    <Badge appearance="outline" size="large" color="informative">
                        {riskLevel}
                    </Badge>

                    {isCollapsed && statusBadge}
                    {isCollapsed && executedByName && isExecutedWithUserPerms && <Caption1>{executedByName}</Caption1>}
                </div>

                {!isCurrentExecutionPending && (
                    <Tooltip
                        relationship="label"
                        content={
                            isCollapsed ? (
                                <FormattedMessage {...SreAgentResources.expand} />
                            ) : (
                                <FormattedMessage {...SreAgentResources.collapse} />
                            )
                        }
                    >
                        <Button
                            icon={isCollapsed ? <ChevronUpDown16Regular /> : <ChevronDownUp16Regular />}
                            onClick={() => setIsCollapsed(!isCollapsed)}
                            size="small"
                        />
                    </Tooltip>
                )}
            </div>

            <div className={classes.codeBlock} style={{ marginTop: 12 }}>
                {!isCollapsed && <Caption1 style={{ color: tokens.colorNeutralForeground3 }}>{executionTypeLabel}</Caption1>}
                <div className={classes.copyButton}>
                    <CopyButton textToCopy={currentExecution.command} />
                </div>
                <pre className={mergeClasses(classes.outputPre, isCollapsed ? classes.outputPreCollapsed : undefined)}>
                    <code>{currentExecution.command}</code>
                </pre>
            </div>

            {isKubectlExecution(currentExecution) && currentExecution.stdin && currentExecution.stdin.trim() && (
                <div style={{ marginTop: 8 }}>
                    <Text weight="semibold" size={200}>
                        <FormattedMessage {...SreAgentResources.standardInput} />:
                    </Text>
                    <div className={classes.codeBlock} style={{ marginTop: 4 }}>
                        <div className={classes.copyButton}>
                            <CopyButton textToCopy={currentExecution.stdin || ''} />
                        </div>
                        <pre className={classes.outputPre} style={{ color: tokens.colorStatusWarningForeground1 }}>
                            <code>{currentExecution.stdin}</code>
                        </pre>
                    </div>
                </div>
            )}

            {!isCollapsed && <Divider style={{ marginTop: 8 }} />}

            {isCurrentExecutionPending && (
                <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
                    <PermissionedButton
                        canPerform={canApproveThreads}
                        noPermissionTooltip={intl.formatMessage(ActivitiesResources.approveActionNoPermissionTooltip)}
                        disabledReason={isActionLoading}
                        appearance="primary"
                        onClick={() => handleAction('run')}
                        icon={loadingAction === 'run' ? <Spinner size="tiny" /> : undefined}
                    >
                        {currentExecution.status === ExecutionStatus.Pending ? (
                            <FormattedMessage {...SreAgentResources.approveAction} />
                        ) : (
                            <FormattedMessage {...SreAgentResources.grantPermissions} />
                        )}
                    </PermissionedButton>
                    <PermissionedButton
                        canPerform={canApproveThreads}
                        noPermissionTooltip={intl.formatMessage(ActivitiesResources.approveActionNoPermissionTooltip)}
                        disabledReason={isActionLoading}
                        appearance="secondary"
                        onClick={() => handleAction('cancel')}
                        icon={loadingAction === 'cancel' ? <Spinner size="tiny" /> : undefined}
                    >
                        <FormattedMessage {...SreAgentResources.cancel} />
                    </PermissionedButton>
                </div>
            )}

            {isCurrentExecutionPending && (
                <div style={{ marginTop: 8 }}>
                    <Text>
                        {currentExecution.status === ExecutionStatus.Pending ? (
                            <FormattedMessage {...SreAgentResources.agentPermsPending} />
                        ) : (
                            <FormattedMessage {...SreAgentResources.userPermsPending} />
                        )}
                    </Text>
                </div>
            )}

            {!isCurrentExecutionPending && !isCollapsed && (
                <div className={classes.infoLine} style={{ marginTop: 12 }}>
                    {statusBadge}

                    {currentExecution.status === ExecutionStatus.Cancelled ? (
                        <Text>
                            {isExecutedWithUserPerms ? (
                                <FormattedMessage {...SreAgentResources.canceledByUser} values={{ name: executedByName }} />
                            ) : (
                                <FormattedMessage {...SreAgentResources.canceledAction} />
                            )}
                        </Text>
                    ) : currentExecution.status === ExecutionStatus.Completed ? (
                        <Text>
                            {isExecutedWithUserPerms ? (
                                <FormattedMessage {...SreAgentResources.userPermsCompleted} values={{ name: executedByName }} />
                            ) : (
                                <FormattedMessage {...SreAgentResources.agentPermsCompleted} />
                            )}
                        </Text>
                    ) : currentExecution.status === ExecutionStatus.Failed ? (
                        <Text>
                            {isExecutedWithUserPerms ? (
                                <FormattedMessage {...SreAgentResources.userPermsFailed} values={{ name: executedByName }} />
                            ) : (
                                <FormattedMessage {...SreAgentResources.agentPermsFailed} />
                            )}
                        </Text>
                    ) : (
                        <Text>
                            {isExecutedWithUserPerms ? (
                                <FormattedMessage {...SreAgentResources.userPermsRunning} values={{ name: executedByName }} />
                            ) : (
                                <FormattedMessage {...SreAgentResources.agentPermsRunning} />
                            )}
                        </Text>
                    )}
                </div>
            )}

            <Accordion multiple collapsible style={{ marginTop: 12, display: isCollapsed ? 'none' : undefined }}>
                {showOutputAccordion && (currentExecution.output || currentExecution.error) && (
                    <AccordionItem value="output">
                        <AccordionHeader style={{ color: tokens.colorNeutralForeground3 }}>
                            <FormattedMessage {...SreAgentResources.outputLogs} />
                        </AccordionHeader>
                        <AccordionPanel>
                            <div className={classes.codeBlock}>
                                <div className={classes.copyButton}>
                                    <CopyButton textToCopy={getCombinedOutput()} />
                                </div>
                                {currentExecution.output && (
                                    <pre className={classes.outputPre} style={{ color: tokens.colorStatusSuccessForeground1 }}>
                                        {currentExecution.output}
                                    </pre>
                                )}
                                {currentExecution.error && (
                                    <pre className={classes.outputPre} style={{ color: tokens.colorStatusDangerForeground1, marginTop: 8 }}>
                                        <FormattedMessage {...SreAgentResources.error} />: {currentExecution.error}
                                    </pre>
                                )}
                            </div>
                        </AccordionPanel>
                    </AccordionItem>
                )}
                <AccordionItem value="timestamps">
                    <AccordionHeader style={{ color: tokens.colorNeutralForeground3 }}>
                        <FormattedMessage {...SreAgentResources.timestamps} />
                    </AccordionHeader>
                    <AccordionPanel>
                        <ApprovalTimestamps
                            created={currentExecution.createdTimestamp}
                            started={currentExecution.startedTimestamp}
                            ended={currentExecution.completedTimestamp}
                        />
                    </AccordionPanel>
                </AccordionItem>
            </Accordion>
        </div>
    );
};

export default ExecutionMessage;
