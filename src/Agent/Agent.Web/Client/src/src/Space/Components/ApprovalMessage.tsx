import { InfoLabel } from '@fluentui/react-infolabel';
import axios from 'axios';
import { useContext, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Approval, ApprovalDecision } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';

const ApprovalMessage = ({ approval, messageId, threadId }: { approval?: Approval; messageId: string; threadId: string }) => {
    const [approvalStatus, setApprovalStatus] = useState<ApprovalDecision | null>(approval ? approval.status : null);
    const [isApprovalLoading, setIsApprovalLoading] = useState(false);
    const [loadingButton, setLoadingButton] = useState<'approve' | 'deny' | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    if (!approval) return null;

    // Use the local state for status to ensure UI updates immediately after user action
    const status = approvalStatus || approval.status;
    const { title, description, oboTokenScope } = approval;

    // Log approval information to help with debugging
    console.log('Rendering approval with status:', status, 'and title:', title);

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
                // Check if already approved/rejected
                if (approval.status !== ApprovalDecision.Pending) {
                    console.warn(`Approval ${approval.id} is already ${approval.status}`);
                    return; // Exit early if already decided
                }

                setIsApprovalLoading(true);
                setLoadingButton(approved ? 'approve' : 'deny');
                const approvalData = await sendApprovalDecision(
                    threadId,
                    approval.id,
                    approved ? ApprovalDecision.Approved : ApprovalDecision.Rejected,
                    approval.oboTokenScope
                );

                console.log(`Approval decision sent for message ID: ${messageId}, approved: ${approved}`);

                setApprovalStatus(approvalData.status as ApprovalDecision);
                approval = {
                    ...approval,
                    status: approvalData.status as ApprovalDecision,
                    decisionUser: {
                        displayName: approvalData.decisionMakerName || approvalData.decisionMaker || 'Web Client User',
                        userId: approvalData.decisionMakerId || approvalData.decisionMaker,
                        role: 'User',
                    },
                    decisionTimestamp: approvalData.decisionTimestamp,
                };
            }
        } catch (error: any) {
            console.error(`Failed to send approval decision for message ID: ${messageId}`, error);

            // Handle specific error cases
            if (error.response?.status === 409) {
                // Conflict - already approved/rejected
                const errorData = error.response?.data;

                if (approval && errorData) {
                    approval = {
                        ...approval,
                        status: errorData.status as ApprovalDecision,
                        decisionUser: {
                            displayName: errorData.decisionMakerName || 'Unknown User',
                            userId: errorData.decisionMakerId || '',
                            role: 'User',
                        },
                        decisionTimestamp: errorData.decisionTimestamp,
                    };

                    setApprovalStatus(errorData.status as ApprovalDecision);
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

    if (status === ApprovalDecision.Pending) {
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
                                An on-behalf-of token will be used with the following scope: <b>{oboTokenScope}</b>
                            </>
                        }
                    >
                        {description}
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
                        <FormattedMessage {...SreAgentResources.approve} />
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
                        <FormattedMessage {...SreAgentResources.deny} />
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
                <p
                    style={{
                        fontSize: '11px',
                        color: '#666',
                        marginTop: '16px',
                        marginBottom: '0',
                    }}
                >
                    <FormattedMessage {...SreAgentResources.approveUsingCreds} />
                </p>
            </div>
        );
    } else {
        // For Approved or Denied status
        const statusColor = status === ApprovalDecision.Approved ? '#107C10' : '#A4262C';

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
                                    An on-behalf-of token will be used with the following scope: <b>{oboTokenScope}</b>
                                </>
                            }
                        >
                            {description}
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
                        {status === ApprovalDecision.Approved ? (
                            <FormattedMessage {...SreAgentResources.approved} />
                        ) : (
                            <FormattedMessage {...SreAgentResources.denied} />
                        )}
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
                                {status === ApprovalDecision.Approved ? (
                                    <FormattedMessage {...SreAgentResources.approvedBy} />
                                ) : (
                                    <FormattedMessage {...SreAgentResources.deniedBy} />
                                )}
                                :
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

                {status === ApprovalDecision.Approved && (
                    <p
                        style={{
                            fontSize: '11px',
                            color: '#666',
                            marginTop: '16px',
                            marginBottom: '0',
                        }}
                    >
                        <FormattedMessage {...SreAgentResources.beingExecutedUsingCreds} />
                    </p>
                )}
            </div>
        );
    }
};

export default ApprovalMessage;
