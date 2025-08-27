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
import axios from 'axios';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Approval, ApprovalDecision, AzCliExecution, KubectlExecution } from '../../Common/Contracts/DataPlane/Message';
import { getAgentHeaders } from '../../Common/Helpers/headers';
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

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    useEffect(() => {
        setApproval(approvalInput);
    }, [approvalInput]);

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

    const sendApprovalDecision = async (threadId: string, approvalId: string, decision: ApprovalDecision, scope?: string) => {
        const url = `${sreAgentEndpoint}/api/v1/approvals/${threadId}/${approvalId}/decision`;

        const response = await axios.post(
            url,
            {
                Status: decision,
                User: userIdAndDisplayName.userId,
                Scope: scope,
            },
            {
                headers: getAgentHeaders(scope),
            }
        );

        return response.data;
    };

    const handleApprovalDecision = async (approved: boolean) => {
        try {
            if (approval) {
                // Check if already approved/rejected/canceled/authorized
                if (approval.status !== ApprovalDecision.Pending && approval.status !== ApprovalDecision.PendingAuthorization) {
                    console.warn(`Approval ${approval.id} is already ${approval.status}`);
                    return;
                }

                setIsApprovalLoading(true);
                setLoadingButton(approved ? 'approve' : 'deny');

                let decision: ApprovalDecision;
                if (approval.status === ApprovalDecision.Pending) {
                    decision = approved ? ApprovalDecision.Approved : ApprovalDecision.Cancelled;
                } else {
                    decision = approved ? ApprovalDecision.Authorized : ApprovalDecision.Cancelled;
                }

                const approvalData = await sendApprovalDecision(threadId, approval.id, decision, approval.oboTokenScope);

                console.log(`Approval decision sent for message ID: ${messageId}, approved: ${approved}`);

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
            }
        } catch (error: any) {
            console.error(`Failed to send approval decision for message ID: ${messageId}`, error);

            if (error.response?.status === 409) {
                // Conflict - already approved/rejected/canceled/authorized
                const errorData = error.response?.data;

                if (approval && errorData) {
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

                const formattedDate = errorData.decisionTimestamp ? new Date(errorData.decisionTimestamp).toLocaleString() : 'unknown date';
                console.error(
                    `This operation was already ${errorData.status?.toLowerCase()} by ${errorData.decisionMakerName || 'Unknown User'} on ${formattedDate}`
                );
            } else {
                console.error('Failed to process approval decision. Please try again.');
            }
        } finally {
            setIsApprovalLoading(false);
            setLoadingButton(null);
        }
    };

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
