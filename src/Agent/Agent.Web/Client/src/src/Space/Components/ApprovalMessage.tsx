import { InfoLabel } from '@fluentui/react-infolabel';
import axios from 'axios';
import { useContext, useEffect, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Approval, ApprovalDecision } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';

const ApprovalMessage = ({
    approval: approvalInput,
    messageId,
    threadId,
}: {
    approval?: Approval;
    messageId: string;
    threadId: string;
}) => {
    const [approval, setApproval] = useState<Approval | undefined>(approvalInput);
    const [isApprovalLoading, setIsApprovalLoading] = useState(false);
    const [loadingButton, setLoadingButton] = useState<'approve' | 'deny' | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    useEffect(() => {
        setApproval(approvalInput);
    }, [approvalInput]);

    if (!approval) return null;

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
                    return; // Exit early if already decided
                }

                setIsApprovalLoading(true);
                setLoadingButton(approved ? 'approve' : 'deny');

                let decision: ApprovalDecision;
                if (approval.status === ApprovalDecision.Pending) {
                    decision = approved ? ApprovalDecision.Approved : ApprovalDecision.Cancelled;
                } else if (approval.status === ApprovalDecision.PendingAuthorization) {
                    decision = approved ? ApprovalDecision.Authorized : ApprovalDecision.Cancelled;
                } else {
                    // Should not reach here due to check above, but fallback
                    decision = approved ? ApprovalDecision.Approved : ApprovalDecision.Cancelled;
                }

                const approvalData = await sendApprovalDecision(threadId, approval.id, decision, approval.oboTokenScope);

                console.log(`Approval decision sent for message ID: ${messageId}, approved: ${approved}`);

                setApproval({
                    ...approval,
                    status: approvalData.status as ApprovalDecision,
                    decisionUser: {
                        displayName: approvalData.decisionMakerName || approvalData.decisionMaker || 'Web Client User',
                        userId: approvalData.decisionMakerId || approvalData.decisionMaker,
                        role: 'User',
                    },
                    decisionTimestamp: approvalData.decisionTimestamp,
                });
            }
        } catch (error: any) {
            console.error(`Failed to send approval decision for message ID: ${messageId}`, error);

            // Handle specific error cases
            if (error.response?.status === 409) {
                // Conflict - already approved/rejected/canceled/authorized
                const errorData = error.response?.data;

                if (approval && errorData) {
                    setApproval({
                        ...approval,
                        status: errorData.status as ApprovalDecision,
                        decisionUser: {
                            displayName: errorData.decisionMakerName || 'Unknown User',
                            userId: errorData.decisionMakerId || '',
                            role: 'User',
                        },
                        decisionTimestamp: errorData.decisionTimestamp,
                    });
                }

                const formattedDate = errorData.decisionTimestamp ? new Date(errorData.decisionTimestamp).toLocaleString() : 'unknown date';
                alert(
                    `This operation was already ${errorData.status?.toLowerCase()} by ${errorData.decisionMakerName || 'Unknown User'} on ${formattedDate}`
                );
            } else {
                alert('Failed to process approval decision. Please try again.');
            }
        } finally {
            setIsApprovalLoading(false);
            setLoadingButton(null);
        }
    };

    if (approval.status === ApprovalDecision.Pending || approval.status === ApprovalDecision.PendingAuthorization) {
        // Get button text based on status
        const primaryButtonText = approval.status === ApprovalDecision.Pending ? SreAgentResources.continue : SreAgentResources.authorize;

        return (
            <div
                style={{
                    border: '1px solid #ececec',
                    borderRadius: '8px',
                    padding: '16px',
                    marginTop: '16px',
                    backgroundColor: '#f9f9f9',
                }}
            >
                <h4 style={{ margin: '0 0 16px 0' }}>
                    <InfoLabel
                        info={
                            <>
                                An on-behalf-of token will be used with the following scope: <b>{approval.oboTokenScope}</b>
                            </>
                        }
                    >
                        {approval?.description}
                    </InfoLabel>
                </h4>
                <div style={{ display: 'flex', gap: '8px' }}>
                    <button
                        style={{
                            backgroundColor: '#0078D4',
                            color: 'white',
                            border: 'none',
                            padding: '8px 16px',
                            borderRadius: '4px',
                            cursor: isApprovalLoading ? 'not-allowed' : 'pointer',
                            fontWeight: 'bold',
                            opacity: isApprovalLoading ? 0.7 : 1,
                            display: 'flex',
                            alignItems: 'center',
                            gap: '8px',
                        }}
                        onClick={() => handleApprovalDecision(true)}
                        disabled={isApprovalLoading}
                    >
                        {loadingButton === 'approve' && (
                            <div
                                style={{
                                    width: '16px',
                                    height: '16px',
                                    border: '2px solid #ffffff',
                                    borderTop: '2px solid transparent',
                                    borderRadius: '50%',
                                    animation: 'spin 1s linear infinite',
                                }}
                            />
                        )}
                        <FormattedMessage {...primaryButtonText} />
                    </button>
                    <button
                        style={{
                            backgroundColor: '#ffffff',
                            color: '#333',
                            border: '1px solid #ccc',
                            padding: '8px 16px',
                            borderRadius: '4px',
                            cursor: isApprovalLoading ? 'not-allowed' : 'pointer',
                            fontWeight: 'bold',
                            opacity: isApprovalLoading ? 0.7 : 1,
                            display: 'flex',
                            alignItems: 'center',
                            gap: '8px',
                        }}
                        onClick={() => handleApprovalDecision(false)}
                        disabled={isApprovalLoading}
                    >
                        {loadingButton === 'deny' && (
                            <div
                                style={{
                                    width: '16px',
                                    height: '16px',
                                    border: '2px solid #333333',
                                    borderTop: '2px solid transparent',
                                    borderRadius: '50%',
                                    animation: 'spin 1s linear infinite',
                                }}
                            />
                        )}
                        <FormattedMessage {...SreAgentResources.cancel} />
                    </button>
                </div>
                <style>
                    {`
                            @keyframes spin {
                                0% { transform: rotate(0deg); }
                                100% { transform: rotate(360deg); }
                            }
                        `}
                </style>
                {approval.status === ApprovalDecision.PendingAuthorization && (
                    <p
                        style={{
                            fontSize: '11px',
                            color: '#666',
                            marginTop: '16px',
                            marginBottom: '0',
                        }}
                    >
                        <FormattedMessage {...SreAgentResources.authorizeUsingCreds} />
                    </p>
                )}
                {approval.status === ApprovalDecision.Pending && (
                    <p
                        style={{
                            fontSize: '11px',
                            color: '#666',
                            marginTop: '16px',
                            marginBottom: '0',
                        }}
                    >
                        <FormattedMessage {...SreAgentResources.continueUsingCreds} />
                    </p>
                )}
            </div>
        );
    } else {
        // For Approved, Canceled, Authorized, or Rejected status
        const getStatusColor = (status: ApprovalDecision) => {
            switch (status) {
                case ApprovalDecision.Approved:
                case ApprovalDecision.Authorized:
                    return '#107C10';
                case ApprovalDecision.Cancelled:
                    return '#A4262C';
                default:
                    return '#666';
            }
        };

        const getStatusText = (status: ApprovalDecision) => {
            switch (status) {
                case ApprovalDecision.Approved:
                    return SreAgentResources.approved;
                case ApprovalDecision.Authorized:
                    return SreAgentResources.authorized;
                case ApprovalDecision.Cancelled:
                    return SreAgentResources.canceled;
                default:
                    return SreAgentResources.denied;
            }
        };

        const getDecisionByText = (status: ApprovalDecision) => {
            switch (status) {
                case ApprovalDecision.Approved:
                    return SreAgentResources.approvedBy;
                case ApprovalDecision.Authorized:
                    return SreAgentResources.authorizedBy;
                case ApprovalDecision.Cancelled:
                    return SreAgentResources.canceledBy;
                default:
                    return SreAgentResources.deniedBy;
            }
        };

        const statusColor = getStatusColor(approval.status);

        return (
            <div
                style={{
                    border: '1px solid #ececec',
                    borderRadius: '8px',
                    padding: '16px',
                    marginTop: '16px',
                    backgroundColor: '#f9f9f9',
                }}
            >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                    <h4 style={{ margin: '0', fontWeight: '600', maxWidth: '75%' }}>
                        <InfoLabel
                            info={
                                <>
                                    An on-behalf-of token will be used with the following scope: <b>{approval.oboTokenScope}</b>
                                </>
                            }
                        >
                            {approval.description}
                        </InfoLabel>
                    </h4>
                    <span
                        style={{
                            color: statusColor,
                            fontWeight: 'bold',
                            padding: '4px 12px',
                            borderRadius: '4px',
                            backgroundColor: `${statusColor}15`,
                            display: 'inline-block',
                        }}
                    >
                        <FormattedMessage {...getStatusText(approval.status)} />
                    </span>
                </div>
                <p style={{ margin: '0 0 16px 0' }}>
                    {' '}
                    <FormattedMessage {...SreAgentResources.requestedAt} />
                    {': '}
                    {approval.createdTimestamp ? new Date(approval.createdTimestamp).toLocaleString() : 'N/A'}
                </p>

                {approval.decisionUser && (
                    <div style={{ fontSize: '14px', color: '#666' }}>
                        <p style={{ margin: '4px 0' }}>
                            <strong>
                                <FormattedMessage {...getDecisionByText(approval.status)} />:
                            </strong>{' '}
                            {approval.decisionUser.displayName}
                        </p>
                        {approval.decisionTimestamp && (
                            <p style={{ margin: '4px 0' }}>
                                <strong>
                                    <FormattedMessage {...SreAgentResources.decisionTime} />:
                                </strong>{' '}
                                {new Date(approval.decisionTimestamp).toLocaleString()}
                            </p>
                        )}
                    </div>
                )}

                {(approval.status === ApprovalDecision.Approved || approval.status === ApprovalDecision.Authorized) && (
                    <p
                        style={{
                            fontSize: '11px',
                            color: '#666',
                            marginTop: '16px',
                            marginBottom: '0',
                            fontStyle: 'italic',
                        }}
                    >
                        {status === ApprovalDecision.Approved ? (
                            <FormattedMessage {...SreAgentResources.beingExecutedUsingCreds} />
                        ) : (
                            <FormattedMessage {...SreAgentResources.beingExecutedUsingApproverCreds} />
                        )}
                    </p>
                )}
            </div>
        );
    }
};

export default ApprovalMessage;
