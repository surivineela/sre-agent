import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Badge,
    Button,
    Card,
    Divider,
    Spinner,
    Text,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import { CheckmarkCircle16Filled, Dismiss16Regular } from '@fluentui/react-icons';
import { InfoLabel } from '@fluentui/react-infolabel';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Approval, ApprovalDecision, AzCliExecution, KubectlExecution } from '../../Common/Contracts/DataPlane/Message';
// headers handled by ThreadClient
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { ApprovalTimestamps } from './ApprovalTimestamps';

const useStyles = makeStyles({
    card: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusMedium,
        padding: '12px',
        backgroundColor: tokens.colorNeutralBackground2,
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
});

const ApprovalMessage = ({
    approval: approvalInput,
    messageId,
    threadId,
    updateSpecialMessageInStreamingMessage,
}: {
    approval?: Approval;
    messageId: string;
    threadId: string;
    updateSpecialMessageInStreamingMessage?: (specialMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
    }) => void;
}) => {
    const [approval, setApproval] = useState<Approval | undefined>(approvalInput);
    const [isApprovalLoading, setIsApprovalLoading] = useState(false);
    const [loadingButton, setLoadingButton] = useState<'approve' | 'deny' | null>(null);
    const classes = useStyles();

    const { sreAgentEndpoint, resourceId } = useContext(EnvironmentContext);
    const azPortalProxy = useAzPortalContext();
    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const threadClient = useMemo(() => ThreadClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

    const isPending = approval?.status === ApprovalDecision.Pending || approval?.status === ApprovalDecision.PendingAuthorization;

    const getStatusText = useCallback((status: ApprovalDecision) => {
        switch (status) {
            case ApprovalDecision.Approved:
                return SreAgentResources.approved;
            case ApprovalDecision.Authorized:
                return SreAgentResources.authorized;
            case ApprovalDecision.Cancelled:
            default:
                return SreAgentResources.canceled;
        }
    }, []);

    const statusBadge = useMemo(() => {
        if (!approval || isPending) return null;

        switch (approval.status) {
            case ApprovalDecision.Approved:
            case ApprovalDecision.Authorized:
                return (
                    <Badge color="success" icon={<CheckmarkCircle16Filled />}>
                        <FormattedMessage {...getStatusText(approval.status)} />
                    </Badge>
                );
            case ApprovalDecision.Cancelled:
            default:
                return (
                    <Badge appearance="outline" color="informative" icon={<Dismiss16Regular />}>
                        <FormattedMessage {...getStatusText(approval.status)} />
                    </Badge>
                );
        }
    }, [approval, isPending, getStatusText]);

    const primaryButtonText = approval?.status === ApprovalDecision.Pending ? SreAgentResources.continue : SreAgentResources.authorize;

    const handleApprovalDecision = async (approved: boolean) => {
        if (!approval) return;

        // Check if already approved/rejected/canceled/authorized
        if (approval.status !== ApprovalDecision.Pending && approval.status !== ApprovalDecision.PendingAuthorization) {
            return;
        }

        setIsApprovalLoading(true);
        setLoadingButton(approved ? 'approve' : 'deny');

        azPortalProxy.logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: `${approved ? 'approved' : 'denied'}Action`,
            targetFriendlyName: `${approved ? 'Approved' : 'Denied'} action`,
            valueObjectName: approval?.description,
            valueObjectFriendlyName: approval?.description,
            metadata: {
                permissions: approval.status === ApprovalDecision.Pending ? 'agent' : 'obo',
                threadId,
            },
        });

        let decision: ApprovalDecision;
        if (approval.status === ApprovalDecision.Pending) {
            decision = approved ? ApprovalDecision.Approved : ApprovalDecision.Cancelled;
        } else {
            decision = approved ? ApprovalDecision.Authorized : ApprovalDecision.Cancelled;
        }

        const approvalDecisionResult = await threadClient.postApprovalDecision(
            threadId,
            approval.id,
            decision,
            userIdAndDisplayName.userId,
            approval.oboTokenScope
        );

        if (approvalDecisionResult.isSuccessful && approvalDecisionResult.content) {
            const approvalData = approvalDecisionResult.content;
            const updatedApproval: Approval = {
                ...approval,
                status: approvalData.status as ApprovalDecision,
                decisionUser: {
                    displayName: approvalData.decisionMakerName || approvalData.decisionMaker || 'Web Client User',
                    userId: approvalData.decisionMakerId || approvalData.decisionMaker,
                    role: 'User',
                },
                decisionTimestamp: approvalData.decisionTimestamp,
            };

            setApproval(updatedApproval);
            updateSpecialMessageInStreamingMessage?.({
                approval: updatedApproval,
            });
        } else {
            azPortalProxy.log({
                action: 'approvalDecision',
                actionModifier: 'failed',
                logLevel: 'error',
                resourceId,
                data: {
                    error: approvalDecisionResult.error,
                    messageId,
                    approvalId: approval?.id,
                },
            });

            const errorData = approvalDecisionResult.error.response?.data;
            // Conflict - already approved/rejected/canceled/authorized
            if (approval && errorData && approvalDecisionResult.error.response?.status === 409) {
                const updatedApproval: Approval = {
                    ...approval,
                    status: errorData.status as ApprovalDecision,
                    decisionUser: {
                        displayName: errorData.decisionMakerName || 'Unknown User',
                        userId: errorData.decisionMakerId || '',
                        role: 'User',
                    },
                    decisionTimestamp: errorData.decisionTimestamp,
                };

                setApproval(updatedApproval);
                updateSpecialMessageInStreamingMessage?.({
                    approval: updatedApproval,
                });
            }
        }

        setIsApprovalLoading(false);
        setLoadingButton(null);
    };

    useEffect(() => {
        setApproval(approvalInput);
    }, [approvalInput]);

    if (!approval) return null;

    return (
        <Card className={classes.card}>
            <div className={classes.headerRow}>
                <div className={classes.summaryLeft}>
                    <InfoLabel
                        info={
                            <>
                                <FormattedMessage {...SreAgentResources.oboTokenUsed} />: <b>{approval?.oboTokenScope}</b>
                            </>
                        }
                    >
                        <Text weight="semibold">{approval?.description}</Text>
                    </InfoLabel>
                </div>
            </div>
            <Divider style={{ marginTop: 8 }} />

            {isPending ? (
                <>
                    <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
                        <Button
                            appearance="primary"
                            onClick={() => handleApprovalDecision(true)}
                            icon={loadingButton === 'approve' ? <Spinner size="tiny" /> : undefined}
                            disabled={isApprovalLoading}
                        >
                            <FormattedMessage {...primaryButtonText} />
                        </Button>
                        <Button
                            appearance="secondary"
                            onClick={() => handleApprovalDecision(false)}
                            icon={loadingButton === 'deny' ? <Spinner size="tiny" /> : undefined}
                            disabled={isApprovalLoading}
                        >
                            <FormattedMessage {...SreAgentResources.cancel} />
                        </Button>
                    </div>

                    <div style={{ marginTop: 8 }}>
                        <Text>
                            {approval?.status === ApprovalDecision.Pending ? (
                                <FormattedMessage {...SreAgentResources.agentPermsPending} />
                            ) : (
                                <FormattedMessage {...SreAgentResources.userPermsPending} />
                            )}
                        </Text>
                    </div>
                </>
            ) : (
                <div style={{ display: 'flex', gap: '4px', marginTop: 12 }}>
                    {statusBadge}

                    <Text>
                        {approval.status === ApprovalDecision.Cancelled ? (
                            <FormattedMessage {...SreAgentResources.canceledByUser} values={{ name: approval.decisionUser?.displayName }} />
                        ) : approval.status === ApprovalDecision.Approved ? (
                            <FormattedMessage {...SreAgentResources.agentPermsCompleted} />
                        ) : approval.status === ApprovalDecision.Authorized ? (
                            <FormattedMessage
                                {...SreAgentResources.userPermsCompleted}
                                values={{ name: approval.decisionUser?.displayName }}
                            />
                        ) : null}
                    </Text>
                </div>
            )}

            <Accordion multiple collapsible style={{ marginTop: 12 }}>
                <AccordionItem value="timestamps">
                    <AccordionHeader>
                        <FormattedMessage {...SreAgentResources.timestamps} />
                    </AccordionHeader>
                    <AccordionPanel>
                        <ApprovalTimestamps created={approval.createdTimestamp} ended={approval.decisionTimestamp} />
                    </AccordionPanel>
                </AccordionItem>
            </Accordion>
        </Card>
    );
};

export default ApprovalMessage;
