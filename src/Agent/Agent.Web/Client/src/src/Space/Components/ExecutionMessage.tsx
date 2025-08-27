import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Badge,
    Button,
    Caption1,
    Card,
    Divider,
    Spinner,
    Text,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { CheckmarkCircle16Filled, Dismiss16Regular, DismissCircle16Filled } from '@fluentui/react-icons';
import axios from 'axios';
import { useEffect, useMemo, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import CopyButton from '../../Common/Components/CopyButton';
import { Approval, AzCliExecution, ExecutionStatus, KubectlExecution } from '../../Common/Contracts/DataPlane/Message';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { ApprovalTimestamps } from './ApprovalTimestamps';
import { getRiskColor, getRiskLevel } from './Utility';

// TODO: Show collapsed view by default for non-pending executions

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
    updateSpecialMessageInStreamingMessage?: (specialMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
    }) => void;
};

const useStyles = makeStyles({
    card: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: '12px',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    codeBlock: {
        position: 'relative',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusSmall,
        backgroundColor: tokens.colorNeutralBackground1,
        padding: '12px',
        fontFamily: 'Consolas, Monaco, monospace',
        color: tokens.colorNeutralForeground1,
    },
    copyButton: {
        position: 'absolute',
        top: '8px',
        right: '8px',
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
    },
});

const ExecutionMessage = ({ execution, threadId, type, updateSpecialMessageInStreamingMessage }: ExecutionMessageProps) => {
    const [currentExecution, setCurrentExecution] = useState<ExecutionLike>(execution);
    const [isActionLoading, setIsActionLoading] = useState(false);
    const [loadingAction, setLoadingAction] = useState<'run' | 'cancel' | null>(null);

    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const riskLevel = getRiskLevel(currentExecution.command);
    const riskColor = getRiskColor(riskLevel);
    const classes = useStyles();

    const executedByName = currentExecution.executedBy?.displayName;
    const isExecutedWithUserPerms = executedByName && executedByName !== SreAgentDisplayName;

    const { basePath, executionTypeLabel } = useMemo(() => {
        switch (type) {
            case ExecutionMessageType.Kubectl:
                return {
                    basePath: 'kubectlExecution',
                    executionTypeLabel: 'Kubernetes',
                };
            case ExecutionMessageType.AzCli:
            default:
                return {
                    basePath: 'azCliExecution',
                    executionTypeLabel: 'Azure CLI',
                };
        }
    }, [type]);

    const statusTag = useMemo(() => {
        switch (currentExecution.status) {
            case ExecutionStatus.Completed:
                return (
                    <Badge color="success" icon={<CheckmarkCircle16Filled />}>
                        <FormattedMessage {...SreAgentResources.completed} />
                    </Badge>
                );
            case ExecutionStatus.Failed:
                return (
                    <Badge color="danger" size="large" icon={<DismissCircle16Filled />}>
                        <FormattedMessage {...SreAgentResources.failed} />
                    </Badge>
                );
            case ExecutionStatus.Running:
                return (
                    <Badge appearance="outline" color="informative" icon={<Spinner size="extra-tiny" />}>
                        <FormattedMessage {...SreAgentResources.running} />
                    </Badge>
                );
            case ExecutionStatus.Cancelled:
                return (
                    <Badge color="informative" icon={<Dismiss16Regular />}>
                        <FormattedMessage {...SreAgentResources.canceled} />
                    </Badge>
                );
            default:
                return null;
        }
    }, [currentExecution.status]);

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
                const response = await fetch(`/api/v1/${basePath}/${threadId}/${execution.id}/status`, {
                    headers: getAgentHeaders(),
                    cache: 'no-cache',
                });

                if (!response.ok) {
                    console.error('Failed to fetch execution status:', response.status, response.statusText);
                    return;
                }

                const data = await response.json();

                if (isPolling) {
                    const updatedExecution: ExecutionLike = {
                        ...currentExecution,
                        output: data.output ?? currentExecution.output,
                        status: data.status ?? currentExecution.status,
                        error: data.error ?? currentExecution.error,
                        completedTimestamp: data.completedTimestamp ?? currentExecution.completedTimestamp,
                        executedBy: data.executedBy ?? currentExecution.executedBy,
                    };

                    if (updateSpecialMessageInStreamingMessage) {
                        if (type === ExecutionMessageType.AzCli) {
                            updateSpecialMessageInStreamingMessage({ azCliExecution: updatedExecution as AzCliExecution });
                        } else {
                            updateSpecialMessageInStreamingMessage({ kubectlExecution: updatedExecution as KubectlExecution });
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
                console.error('Error polling for execution status:', error);
            }
        }, 2000);

        return () => {
            isPolling = false;
            clearInterval(pollInterval);
        };
    }, [currentExecution, execution.id, threadId, basePath, updateSpecialMessageInStreamingMessage, type]);

    const showOutputAccordion = currentExecution.status === ExecutionStatus.Completed || currentExecution.status === ExecutionStatus.Failed;

    const isKubectlExecution = (e: ExecutionLike): e is KubectlExecution => 'stdin' in e;

    const handleAction = async (action: 'run' | 'cancel') => {
        setIsActionLoading(true);
        setLoadingAction(action);

        const maxRetries = 3;
        const baseDelay = 1000;

        for (let attempt = 0; attempt <= maxRetries; attempt++) {
            try {
                const response = await axios.post(
                    `/api/v1/${basePath}/${threadId}/${execution.id}/action`,
                    {
                        action,
                        user: userIdAndDisplayName?.userId || 'sreagent-client',
                    },
                    { headers: getAgentHeaders() }
                );

                if (response.data) {
                    const updatedExecution: ExecutionLike = {
                        ...currentExecution,
                        status: response.data.status,
                        startedTimestamp: response.data.startedTimestamp || currentExecution.startedTimestamp,
                        // Don't set executedBy here unless 'cancel' as it will always be the user you sent even if using agent creds
                        executedBy:
                            action === 'cancel' && response.data.executedBy
                                ? {
                                      displayName: response.data.executedBy,
                                      userId: response.data.executedById,
                                      role: 'User',
                                  }
                                : currentExecution.executedBy,
                    };

                    if (updateSpecialMessageInStreamingMessage) {
                        if (type === ExecutionMessageType.AzCli) {
                            updateSpecialMessageInStreamingMessage({ azCliExecution: updatedExecution as AzCliExecution });
                        } else {
                            updateSpecialMessageInStreamingMessage({ kubectlExecution: updatedExecution as KubectlExecution });
                        }
                    } else {
                        setCurrentExecution(updatedExecution);
                    }
                }

                break;
            } catch (error: any) {
                console.error(`Failed to ${action} execution (attempt ${attempt + 1}/${maxRetries + 1}):`, error);
                if (attempt === maxRetries) {
                    if (error.response?.status === 409) {
                        console.error(`Cannot ${action} - execution is already ${error.response.data.currentStatus}`);
                    } else {
                        console.error(`Failed to ${action} execution after ${maxRetries + 1} attempts. Please try again.`);
                    }
                } else {
                    const delay = baseDelay * Math.pow(2, attempt);
                    await new Promise(resolve => setTimeout(resolve, delay));
                }
            }
        }

        setIsActionLoading(false);
        setLoadingAction(null);
    };

    return (
        <Card className={classes.card}>
            <div className={classes.headerRow}>
                <div className={classes.summaryLeft}>
                    <Text weight="semibold">{currentExecution.description}</Text>
                    <Badge appearance="tint" size="large" color={riskColor}>
                        {riskLevel}
                    </Badge>
                </div>
            </div>

            <div className={classes.codeBlock} style={{ marginTop: 8 }}>
                <Caption1 style={{ color: tokens.colorNeutralForeground3 }}>{executionTypeLabel}</Caption1>
                <div className={classes.copyButton}>
                    <CopyButton textToCopy={currentExecution.command} />
                </div>
                <pre className={classes.outputPre}>
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
                        <pre className={classes.outputPre} style={{ color: '#b58900' }}>
                            <code>{currentExecution.stdin}</code>
                        </pre>
                    </div>
                </div>
            )}

            <Divider style={{ marginTop: 8 }} />

            {(currentExecution.status === ExecutionStatus.Pending || currentExecution.status === ExecutionStatus.PendingAuthorization) && (
                <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
                    <Button
                        appearance="primary"
                        onClick={() => handleAction('run')}
                        icon={loadingAction === 'run' ? <Spinner size="tiny" /> : undefined}
                        disabled={isActionLoading}
                    >
                        {currentExecution.status === ExecutionStatus.Pending ? (
                            <FormattedMessage {...SreAgentResources.approveAction} />
                        ) : (
                            <FormattedMessage {...SreAgentResources.grantPermissions} />
                        )}
                    </Button>
                    <Button
                        appearance="secondary"
                        onClick={() => handleAction('cancel')}
                        icon={loadingAction === 'cancel' ? <Spinner size="tiny" /> : undefined}
                        disabled={isActionLoading}
                    >
                        <FormattedMessage {...SreAgentResources.cancel} />
                    </Button>
                </div>
            )}

            {(currentExecution.status === ExecutionStatus.Pending || currentExecution.status === ExecutionStatus.PendingAuthorization) && (
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

            {currentExecution.status !== ExecutionStatus.Pending && currentExecution.status !== ExecutionStatus.PendingAuthorization && (
                <div className={classes.infoLine} style={{ marginTop: 12 }}>
                    {statusTag}

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

            <Accordion multiple collapsible style={{ marginTop: 12 }}>
                {showOutputAccordion && (currentExecution.output || currentExecution.error) && (
                    <AccordionItem value="output">
                        <AccordionHeader>
                            <FormattedMessage {...SreAgentResources.outputLogs} />
                        </AccordionHeader>
                        <AccordionPanel>
                            <div className={classes.codeBlock}>
                                <div className={classes.copyButton}>
                                    <CopyButton textToCopy={getCombinedOutput()} />
                                </div>
                                {currentExecution.output && (
                                    <pre className={classes.outputPre} style={{ color: '#198754' }}>
                                        {currentExecution.output}
                                    </pre>
                                )}
                                {currentExecution.error && (
                                    <pre className={classes.outputPre} style={{ color: '#d13438', marginTop: 8 }}>
                                        <FormattedMessage {...SreAgentResources.error} />: {currentExecution.error}
                                    </pre>
                                )}
                            </div>
                        </AccordionPanel>
                    </AccordionItem>
                )}
                <AccordionItem value="timestamps">
                    <AccordionHeader>
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
        </Card>
    );
};

export default ExecutionMessage;
