import { FeedbackButtons } from '@fluentui-copilot/react-copilot';
import {
    CopilotMessageV2 as CopilotMessage,
    CopilotMessageV2Props as CopilotMessageProps,
    UserMessageV2 as UserMessage,
} from '@fluentui-copilot/react-copilot-chat';
import { mergeStyleSets } from '@fluentui/react';
import { Body1Strong, Button, Image, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { SquareDismissRegular } from '@fluentui/react-icons';
import axios from 'axios';
import mermaid from 'mermaid';
import { memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import {
    AiOutlineCheckCircle,
    AiOutlineClockCircle,
    AiOutlineClose,
    AiOutlineCloseCircle,
    AiOutlineCopy,
    AiOutlinePlayCircle,
} from 'react-icons/ai';
import { FormattedMessage, useIntl } from 'react-intl';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import DailyReport from '../../Common/Components/DailyReport';
import IncidentAlert from '../../Common/Components/IncidentAlert';
import InvestigationSummary from '../../Common/Components/InvestigationSummary';
import InvestigationSummaryPanel from '../../Common/Components/InvestigationSummaryPanel';
import { ApprovalDecision, AzCliExecution, KubectlExecution } from '../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { shouldGroupWithPreviousMessage } from '../Activities/Utility';
import { IChatMessageProps } from '../Contracts/Activities';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { ChatBoxStyles, nameAndTimestampContainerStyle, useChatBoxStyles } from '../Styles/Activities.styles';
import AgentChart from './Charts';
import { FeedbackDialog } from './FeedbackDialog';
import MermaidChart from './Mermaid';

const chatMessageStyles = mergeStyleSets({
    regularMessageContent: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '0px 16px',
        borderRadius: tokens.borderRadiusXLarge,
    },
    codeBlock: {
        backgroundColor: tokens.colorNeutralBackground6,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'inline-block',
        padding: '2px 4px',
        borderRadius: tokens.borderRadiusSmall,
    },
    codeBlockInPre: {
        backgroundColor: tokens.colorTransparentBackground,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'block',
    },
    preBlock: {
        overflowX: 'auto',
        overflowY: 'hidden',
        backgroundColor: tokens.colorNeutralBackground6,
        borderRadius: tokens.borderRadiusSmall,
        padding: '15px',
    },
});

// Initialize mermaid with default configuration
mermaid.initialize({
    startOnLoad: false,
    theme: 'neutral',
    flowchart: { useMaxWidth: false },
    securityLevel: 'loose',
});

// Add table styling for markdown tables
const tableStyles = `
  table {
    border-spacing: 0;
    border-collapse: collapse;
    display: block;
    padding: 1px;
    margin-top: 0;
    margin-bottom: 16px;
    width: max-content;
    max-width: 100%;
    overflow: auto;
    border-radius: 8px;
  }

  tr {
    background-color: var(--color-canvas-default, #ffffff);
    border-top: 1px solid var(--color-border-muted, #d0d7de);
  }

  tr:nth-child(2n) {
    background-color: var(--color-canvas-subtle, #f6f8fa);
  }

  td,
  th {
    padding: 6px 13px;
    border: 1px solid var(--color-border-default, #d0d7de);
  }

  th {
    font-weight: 600;
  }

  /* Round corners for first and last cells in first and last rows */
  tr:first-child th:first-child {
    border-top-left-radius: 8px;
  }
  tr:first-child th:last-child {
    border-top-right-radius: 8px;
  }
  tr:last-child td:first-child {
    border-bottom-left-radius: 8px;
  }
  tr:last-child td:last-child {
    border-bottom-right-radius: 8px;
  }

  table img {
    background-color: transparent;
  }

  @media (prefers-color-scheme: dark) {
  tr {
    background-color: #161b22;
    border-top: 1px solid #30363d;
  }

  tr:nth-child(2n) {
    background-color: #21262d;
  }

  td,
  th {
    border: 1px solid #444c56;
    color: #c9d1d9;
  }

  th {
    background-color: #21262d;
    font-weight: bold;
  }
}`;

const getRiskLevel = (command: string): 'Safe' | 'Low' | 'Medium' | 'High' => {
    const cmd = command.toLowerCase();

    // High risk operations
    if (cmd.includes('delete') || cmd.includes('remove') || cmd.includes('purge')) return 'High';

    // Medium risk operations
    if (cmd.includes('create') || cmd.includes('update') || cmd.includes('set') || cmd.includes('scale') || cmd.includes('restart'))
        return 'Medium';

    // Low risk operations
    if (cmd.includes('start') || cmd.includes('stop') || cmd.includes('enable') || cmd.includes('disable')) return 'Low';

    // Safe operations (read-only)
    if (cmd.includes('list') || cmd.includes('show') || cmd.includes('get')) return 'Safe';

    return 'Medium'; // Default
};

const getRiskColor = (risk: string) => {
    switch (risk) {
        case 'Safe':
            return '#16a34a';
        case 'Low':
            return '#3b82f6';
        case 'Medium':
            return '#f59e0b';
        case 'High':
            return '#dc2626';
        default:
            return '#6b7280';
    }
};

// Azure CLI Execution Component
const AzCliExecutionComponent: React.FC<{
    execution: AzCliExecution;
    threadId: string;
}> = ({ execution, threadId }) => {
    const [currentExecution, setCurrentExecution] = useState<AzCliExecution>(execution);
    const [isActionLoading, setIsActionLoading] = useState(false);
    const [loadingAction, setLoadingAction] = useState<'run' | 'cancel' | null>(null);
    const [copied, setCopied] = useState(false);
    const [outputCopied, setOutputCopied] = useState(false);
    const [isOutputCollapsed, setIsOutputCollapsed] = useState(false);
    const [isExecutionCollapsed, setIsExecutionCollapsed] = useState(execution.status !== 'Pending');

    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const riskLevel = getRiskLevel(currentExecution.command);
    const riskColor = getRiskColor(riskLevel);

    const copyCommand = async () => {
        try {
            await navigator.clipboard.writeText(currentExecution.command);
            setCopied(true);
            setTimeout(() => setCopied(false), 2000);
        } catch (err) {
            console.error('Failed to copy command:', err);
        }
    };

    const copyOutput = async () => {
        try {
            let outputText = '';
            if (currentExecution.output) {
                outputText += currentExecution.output;
            }
            if (currentExecution.error) {
                if (outputText) outputText += '\n\n';
                outputText += `Error: ${currentExecution.error}`;
            }

            await navigator.clipboard.writeText(outputText);
            setOutputCopied(true);
            setTimeout(() => setOutputCopied(false), 2000);
        } catch (err) {
            console.error('Failed to copy output:', err);
        }
    };

    const getStatusIcon = () => {
        switch (currentExecution.status) {
            case 'Completed':
                return <AiOutlineCheckCircle size={16} color="#16a34a" />;
            case 'Failed':
                return <AiOutlineCloseCircle size={16} color="#dc2626" />;
            case 'Running':
                return (
                    <div
                        style={{
                            width: '16px',
                            height: '16px',
                            border: '2px solid #3b82f6',
                            borderTop: '2px solid transparent',
                            borderRadius: '50%',
                            animation: 'spin 1s linear infinite',
                        }}
                    />
                );
            case 'Pending':
                return <AiOutlineClockCircle size={16} color="#f59e0b" />;
            case 'Cancelled':
                return <AiOutlineClose size={16} color="#6b7280" />;
            default:
                return <AiOutlineClockCircle size={16} color="#6b7280" />;
        }
    };

    useEffect(() => {
        if (currentExecution.status === 'Running') {
            // Poll for updates using EventSource
            const eventSource = new EventSource(`/api/v1/azCliExecution/${threadId}/${execution.id}/output`);

            eventSource.onmessage = (event: MessageEvent) => {
                const data = JSON.parse(event.data);
                setCurrentExecution(prev => ({
                    ...prev,
                    output: data.output,
                    status: data.status,
                    error: data.error,
                }));

                if (data.completed) {
                    eventSource.close();
                }
            };

            eventSource.onerror = () => {
                eventSource.close();
            };

            return () => eventSource.close();
        }
    }, [currentExecution.status, execution.id, threadId]);

    // Auto-collapse output when execution completes
    useEffect(() => {
        if (currentExecution.status === 'Completed' || currentExecution.status === 'Failed') {
            setIsOutputCollapsed(true);
        }
    }, [currentExecution.status]);

    const handleAction = async (action: 'run' | 'cancel') => {
        setIsActionLoading(true);
        setLoadingAction(action);

        const maxRetries = 3;
        const baseDelay = 1000;

        for (let attempt = 0; attempt <= maxRetries; attempt++) {
            try {
                const response = await axios.post(
                    `/api/v1/azCliExecution/${threadId}/${execution.id}/action`,
                    {
                        action,
                        user: userIdAndDisplayName?.userId || 'sreagent-client',
                    },
                    { headers: getAgentHeaders() }
                );

                if (response.data) {
                    setCurrentExecution(prev => ({
                        ...prev,
                        status: response.data.status,
                        startedTimestamp: response.data.startedTimestamp || prev.startedTimestamp,
                        executedBy: response.data.executedBy
                            ? {
                                  displayName: response.data.executedBy,
                                  userId: response.data.executedById,
                                  role: 'User',
                              }
                            : prev.executedBy,
                    }));
                }

                // Success - break out of retry loop
                break;
            } catch (error: any) {
                console.error(`Failed to ${action} execution (attempt ${attempt + 1}/${maxRetries + 1}):`, error);

                // If this was the last attempt, handle the error
                if (attempt === maxRetries) {
                    if (error.response?.status === 409) {
                        alert(`Cannot ${action} - execution is already ${error.response.data.currentStatus}`);
                    } else {
                        alert(`Failed to ${action} execution after ${maxRetries + 1} attempts. Please try again.`);
                    }
                } else {
                    // Wait before retrying (exponential backoff)
                    const delay = baseDelay * Math.pow(2, attempt);
                    console.log(`Retrying in ${delay}ms...`);
                    await new Promise(resolve => setTimeout(resolve, delay));
                }
            }
        }

        setIsActionLoading(false);
        setLoadingAction(null);
    };

    // Render collapsed view for non-pending executions
    if (isExecutionCollapsed && currentExecution.status !== 'Pending') {
        return (
            <div
                style={{
                    border: '1px solid #ececec',
                    borderRadius: '8px',
                    padding: '12px',
                    marginTop: '16px',
                    backgroundColor: '#f9f9f9',
                    cursor: 'pointer',
                    transition: 'background-color 0.2s',
                }}
                onClick={() => setIsExecutionCollapsed(false)}
                onMouseEnter={e => (e.currentTarget.style.backgroundColor = '#f0f0f0')}
                onMouseLeave={e => (e.currentTarget.style.backgroundColor = '#f9f9f9')}
            >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <span style={{ fontSize: '16px' }}>🖥️</span>
                            <span style={{ fontWeight: '600', fontSize: '14px' }}>Azure CLI</span>
                        </div>

                        {/* Risk indicator */}
                        <span
                            style={{
                                padding: '2px 8px',
                                fontSize: '11px',
                                fontWeight: '500',
                                borderRadius: '12px',
                                backgroundColor: `${riskColor}15`,
                                color: riskColor,
                                border: `1px solid ${riskColor}25`,
                            }}
                        >
                            {riskLevel}
                        </span>

                        {/* Status badge - icon only */}
                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                padding: '4px 6px',
                                borderRadius: '12px',
                                backgroundColor: '#e8e8e8',
                                border: '1px solid #d0d0d0',
                            }}
                        >
                            {getStatusIcon()}
                        </div>

                        {/* Show executor info only if it's not SRE Agent */}
                        {currentExecution.executedBy &&
                            currentExecution.executedBy.displayName !== 'SRE Agent' &&
                            currentExecution.status !== 'Cancelled' && (
                                <span
                                    style={{
                                        fontSize: '11px',
                                        color: '#666',
                                        fontStyle: 'italic',
                                    }}
                                >
                                    Executed by {currentExecution.executedBy.displayName}
                                </span>
                            )}
                    </div>

                    {/* Expand indicator */}
                    <div
                        style={{
                            color: '#666',
                            fontSize: '12px',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '4px',
                        }}
                    >
                        <span>Click to expand</span>
                        <span>▶</span>
                    </div>
                </div>

                {/* Command preview with dark background and copy button */}
                <div
                    style={{
                        marginTop: '8px',
                        position: 'relative',
                        backgroundColor: '#1e1e1e',
                        borderRadius: '6px',
                        padding: '8px 12px',
                        border: '1px solid #333',
                    }}
                >
                    <div
                        style={{
                            fontSize: '13px',
                            color: '#e0e0e0',
                            fontFamily: 'Consolas, Monaco, monospace',
                            fontWeight: '500',
                            lineHeight: '1.4',
                            paddingRight: '40px',
                        }}
                    >
                        {currentExecution.command.length > 80
                            ? `${currentExecution.command.substring(0, 80)}...`
                            : currentExecution.command}
                    </div>

                    {/* Copy button */}
                    <button
                        onClick={async e => {
                            e.stopPropagation();
                            await copyCommand();
                        }}
                        style={{
                            position: 'absolute',
                            top: '6px',
                            right: '8px',
                            background: 'transparent',
                            border: 'none',
                            cursor: 'pointer',
                            color: copied ? '#4CAF50' : '#888',
                            fontSize: '14px',
                            padding: '4px',
                            borderRadius: '4px',
                            transition: 'color 0.2s',
                        }}
                        title={copied ? 'Copied!' : 'Copy command'}
                        onMouseEnter={e => !copied && (e.currentTarget.style.color = '#fff')}
                        onMouseLeave={e => !copied && (e.currentTarget.style.color = '#888')}
                    >
                        <AiOutlineCopy size={16} />
                    </button>
                </div>

                {/* CSS for animations in collapsed view */}
                <style>
                    {`
                        @keyframes spin {
                            0% { transform: rotate(0deg); }
                            100% { transform: rotate(360deg); }
                        }
                    `}
                </style>
            </div>
        );
    }

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
            {/* Header with title, risk, status and collapse button */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span style={{ fontSize: '16px' }}>🖥️</span>
                        <h4 style={{ margin: '0', fontWeight: '600' }}>Azure CLI Command</h4>
                    </div>

                    {/* Subtle risk indicator */}
                    <span
                        style={{
                            padding: '2px 8px',
                            fontSize: '11px',
                            fontWeight: '500',
                            borderRadius: '12px',
                            backgroundColor: `${riskColor}15`,
                            color: riskColor,
                            border: `1px solid ${riskColor}25`,
                        }}
                    >
                        {riskLevel}
                    </span>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    {/* Status badge */}
                    <div
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            padding: '6px 12px',
                            borderRadius: '16px',
                            backgroundColor: '#f0f0f0',
                            border: '1px solid #e0e0e0',
                        }}
                    >
                        {getStatusIcon()}
                        <span style={{ fontSize: '13px', fontWeight: '500', color: '#333' }}>{currentExecution.status}</span>
                    </div>

                    {/* Collapse button for non-pending executions */}
                    {currentExecution.status !== 'Pending' && (
                        <button
                            onClick={() => setIsExecutionCollapsed(true)}
                            style={{
                                background: 'transparent',
                                border: 'none',
                                cursor: 'pointer',
                                color: '#666',
                                fontSize: '12px',
                                padding: '4px',
                                borderRadius: '4px',
                            }}
                            title="Collapse"
                            onMouseEnter={e => (e.currentTarget.style.backgroundColor = '#e0e0e0')}
                            onMouseLeave={e => (e.currentTarget.style.backgroundColor = 'transparent')}
                        >
                            ▲
                        </button>
                    )}
                </div>
            </div>

            <p style={{ fontSize: '14px', color: '#666', marginBottom: '12px', margin: '0 0 12px 0' }}>{currentExecution.description}</p>

            {/* Command block with copy button */}
            <div style={{ position: 'relative', backgroundColor: '#1e1e1e', borderRadius: '6px', padding: '12px', marginBottom: '12px' }}>
                <button
                    onClick={copyCommand}
                    style={{
                        position: 'absolute',
                        top: '8px',
                        right: '8px',
                        padding: '6px',
                        backgroundColor: 'transparent',
                        border: 'none',
                        borderRadius: '4px',
                        cursor: 'pointer',
                        color: copied ? '#16a34a' : '#9ca3af',
                        transition: 'color 0.2s',
                    }}
                    title="Copy command"
                    onMouseEnter={e => (e.currentTarget.style.color = '#ffffff')}
                    onMouseLeave={e => (e.currentTarget.style.color = copied ? '#16a34a' : '#9ca3af')}
                >
                    {copied ? <AiOutlineCheckCircle size={16} /> : <AiOutlineCopy size={16} />}
                </button>

                <pre
                    style={{
                        margin: '0',
                        paddingRight: '40px',
                        overflow: 'auto',
                        whiteSpace: 'pre-wrap',
                        wordBreak: 'break-all',
                        fontFamily: 'Consolas, Monaco, monospace',
                        fontSize: '13px',
                        color: '#c9d1d9',
                    }}
                >
                    <code>{currentExecution.command}</code>
                </pre>
            </div>

            {/* Action buttons for pending state */}
            {currentExecution.status === 'Pending' && (
                <div style={{ display: 'flex', gap: '8px', marginBottom: '12px' }}>
                    <button
                        onClick={() => handleAction('run')}
                        disabled={isActionLoading}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            padding: '8px 16px',
                            backgroundColor: '#0078D4',
                            color: 'white',
                            border: 'none',
                            borderRadius: '6px',
                            cursor: isActionLoading ? 'not-allowed' : 'pointer',
                            fontWeight: '500',
                            fontSize: '14px',
                            opacity: isActionLoading ? 0.7 : 1,
                            transition: 'all 0.2s',
                        }}
                    >
                        {loadingAction === 'run' ? (
                            <div
                                style={{
                                    width: '14px',
                                    height: '14px',
                                    border: '2px solid #ffffff',
                                    borderTop: '2px solid transparent',
                                    borderRadius: '50%',
                                    animation: 'spin 1s linear infinite',
                                }}
                            />
                        ) : (
                            <AiOutlinePlayCircle size={14} />
                        )}
                        Run
                    </button>

                    <button
                        onClick={() => handleAction('cancel')}
                        disabled={isActionLoading}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            padding: '8px 16px',
                            backgroundColor: '#f5f5f5',
                            color: '#333',
                            border: '1px solid #d0d0d0',
                            borderRadius: '6px',
                            cursor: isActionLoading ? 'not-allowed' : 'pointer',
                            fontWeight: '500',
                            fontSize: '14px',
                            opacity: isActionLoading ? 0.7 : 1,
                            transition: 'all 0.2s',
                        }}
                    >
                        {loadingAction === 'cancel' ? (
                            <div
                                style={{
                                    width: '14px',
                                    height: '14px',
                                    border: '2px solid #333333',
                                    borderTop: '2px solid transparent',
                                    borderRadius: '50%',
                                    animation: 'spin 1s linear infinite',
                                }}
                            />
                        ) : (
                            <AiOutlineClose size={14} />
                        )}
                        Cancel
                    </button>
                </div>
            )}

            {/* Output section */}
            {(currentExecution.output || currentExecution.error) && currentExecution.status !== 'Pending' && (
                <div style={{ marginBottom: '12px' }}>
                    {/* Output header with collapse toggle */}
                    <div
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            padding: '8px 12px',
                            backgroundColor: '#2d3748',
                            borderRadius: isOutputCollapsed ? '6px' : '6px 6px 0 0',
                            cursor: 'pointer',
                            borderBottom: isOutputCollapsed ? 'none' : '1px solid #4a5568',
                        }}
                        onClick={() => setIsOutputCollapsed(!isOutputCollapsed)}
                    >
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <span style={{ color: '#e2e8f0', fontSize: '13px', fontWeight: '500' }}>
                                Output {currentExecution.status === 'Running' ? '(Running...)' : ''}
                            </span>
                            {isOutputCollapsed && (
                                <span style={{ color: '#a0aec0', fontSize: '11px' }}>
                                    {currentExecution.output && currentExecution.error
                                        ? 'Output and error available'
                                        : currentExecution.output
                                          ? 'Output available'
                                          : 'Error available'}
                                </span>
                            )}
                        </div>
                        <div
                            style={{
                                transform: isOutputCollapsed ? 'rotate(-90deg)' : 'rotate(0deg)',
                                transition: 'transform 0.2s',
                                color: '#a0aec0',
                            }}
                        >
                            ▼
                        </div>
                    </div>

                    {/* Collapsible output content */}
                    {!isOutputCollapsed && (
                        <div
                            style={{
                                position: 'relative',
                                backgroundColor: '#1e1e1e',
                                borderRadius: '0 0 6px 6px',
                                padding: '12px',
                                maxHeight: '300px',
                                overflowY: 'auto',
                                // Custom scrollbar styling
                                scrollbarWidth: 'thin',
                                scrollbarColor: '#4a5568 #2d3748',
                            }}
                            className="custom-scrollbar"
                        >
                            <button
                                onClick={copyOutput}
                                style={{
                                    position: 'absolute',
                                    top: '8px',
                                    right: '8px',
                                    padding: '6px',
                                    backgroundColor: 'transparent',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    color: outputCopied ? '#16a34a' : '#6b7280',
                                    transition: 'color 0.2s',
                                    zIndex: 1,
                                    opacity: 0.7,
                                }}
                                title="Copy output"
                                onMouseEnter={e => {
                                    e.currentTarget.style.color = '#ffffff';
                                    e.currentTarget.style.opacity = '1';
                                }}
                                onMouseLeave={e => {
                                    e.currentTarget.style.color = outputCopied ? '#16a34a' : '#6b7280';
                                    e.currentTarget.style.opacity = '0.7';
                                }}
                            >
                                {outputCopied ? <AiOutlineCheckCircle size={16} /> : <AiOutlineCopy size={16} />}
                            </button>

                            <div style={{ paddingRight: '40px' }}>
                                {currentExecution.output && (
                                    <pre
                                        style={{
                                            margin: 0,
                                            whiteSpace: 'pre-wrap',
                                            wordBreak: 'break-word',
                                            color: '#4ade80',
                                            fontSize: '12px',
                                            fontFamily: 'Consolas, Monaco, monospace',
                                        }}
                                    >
                                        {currentExecution.output}
                                    </pre>
                                )}
                                {currentExecution.error && (
                                    <pre
                                        style={{
                                            margin: currentExecution.output ? '8px 0 0 0' : 0,
                                            color: '#f87171',
                                            whiteSpace: 'pre-wrap',
                                            wordBreak: 'break-word',
                                            fontSize: '12px',
                                            fontFamily: 'Consolas, Monaco, monospace',
                                        }}
                                    >
                                        Error: {currentExecution.error}
                                    </pre>
                                )}

                                {currentExecution.status === 'Running' && (
                                    <div
                                        style={{
                                            marginTop: '8px',
                                            display: 'flex',
                                            alignItems: 'center',
                                            gap: '8px',
                                            color: '#60a5fa',
                                            fontSize: '12px',
                                        }}
                                    >
                                        <div
                                            style={{
                                                width: '12px',
                                                height: '12px',
                                                border: '2px solid #60a5fa',
                                                borderTop: '2px solid transparent',
                                                borderRadius: '50%',
                                                animation: 'spin 1s linear infinite',
                                            }}
                                        />
                                        <span>Executing...</span>
                                    </div>
                                )}
                            </div>
                        </div>
                    )}
                </div>
            )}

            {/* Execution metadata */}
            {currentExecution.executedBy && (
                <div style={{ fontSize: '14px', color: '#666', marginBottom: '8px' }}>
                    <strong>{currentExecution.status === 'Cancelled' ? 'Cancelled by' : 'Executed by'}:</strong>{' '}
                    {currentExecution.executedBy.displayName}
                </div>
            )}

            {/* Timestamps */}
            <div style={{ fontSize: '12px', color: '#888', display: 'flex', flexDirection: 'column', gap: '2px' }}>
                <div>
                    <strong>Created:</strong> {new Date(currentExecution.createdTimestamp).toLocaleString()}
                </div>
                {currentExecution.startedTimestamp && (
                    <div>
                        <strong>Started:</strong> {new Date(currentExecution.startedTimestamp).toLocaleString()}
                    </div>
                )}
                {currentExecution.completedTimestamp && (
                    <div>
                        <strong>Completed:</strong> {new Date(currentExecution.completedTimestamp).toLocaleString()}
                    </div>
                )}
                {currentExecution.startedTimestamp && currentExecution.completedTimestamp && (
                    <div>
                        <strong>Duration:</strong>{' '}
                        {Math.round(
                            (new Date(currentExecution.completedTimestamp).getTime() -
                                new Date(currentExecution.startedTimestamp).getTime()) /
                                1000
                        )}
                        s
                    </div>
                )}
            </div>

            {currentExecution.status === 'Pending' && (
                <p
                    style={{
                        fontSize: '11px',
                        color: '#888',
                        marginTop: '12px',
                        marginBottom: '0',
                        fontStyle: 'italic',
                    }}
                >
                    This operation will be executed using your Azure credentials
                </p>
            )}

            {/* CSS for animations */}
            <style>
                {`
                    @keyframes spin {
                        0% { transform: rotate(0deg); }
                        100% { transform: rotate(360deg); }
                    }

                    .custom-scrollbar::-webkit-scrollbar {
                        width: 8px;
                    }

                    .custom-scrollbar::-webkit-scrollbar-track {
                        background: #2d3748;
                        border-radius: 4px;
                    }

                    .custom-scrollbar::-webkit-scrollbar-thumb {
                        background: #4a5568;
                        border-radius: 4px;
                        border: 1px solid #2d3748;
                    }

                    .custom-scrollbar::-webkit-scrollbar-thumb:hover {
                        background: #718096;
                    }
                `}
            </style>
        </div>
    );
};

// Kubernetes kubectl Execution Component
const KubectlExecutionComponent: React.FC<{
    execution: KubectlExecution;
    threadId: string;
}> = ({ execution, threadId }) => {
    const [currentExecution, setCurrentExecution] = useState<KubectlExecution>(execution);
    const [isActionLoading, setIsActionLoading] = useState(false);
    const [loadingAction, setLoadingAction] = useState<'run' | 'cancel' | null>(null);
    const [copied, setCopied] = useState(false);
    const [outputCopied, setOutputCopied] = useState(false);
    const [isOutputCollapsed, setIsOutputCollapsed] = useState(false);
    const [isExecutionCollapsed, setIsExecutionCollapsed] = useState(execution.status !== 'Pending');

    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const riskLevel = getRiskLevel(currentExecution.command);
    const riskColor = getRiskColor(riskLevel);

    const copyCommand = async () => {
        try {
            await navigator.clipboard.writeText(currentExecution.command);
            setCopied(true);
            setTimeout(() => setCopied(false), 2000);
        } catch (err) {
            console.error('Failed to copy command:', err);
        }
    };

    const copyOutput = async () => {
        try {
            let outputText = '';
            if (currentExecution.output) {
                outputText += currentExecution.output;
            }
            if (currentExecution.error) {
                if (outputText) outputText += '\n\n';
                outputText += `Error: ${currentExecution.error}`;
            }

            await navigator.clipboard.writeText(outputText);
            setOutputCopied(true);
            setTimeout(() => setOutputCopied(false), 2000);
        } catch (err) {
            console.error('Failed to copy output:', err);
        }
    };

    const getStatusIcon = () => {
        switch (currentExecution.status) {
            case 'Completed':
                return <AiOutlineCheckCircle size={16} color="#16a34a" />;
            case 'Failed':
                return <AiOutlineCloseCircle size={16} color="#dc2626" />;
            case 'Running':
                return (
                    <div
                        style={{
                            width: '16px',
                            height: '16px',
                            border: '2px solid #3b82f6',
                            borderTop: '2px solid transparent',
                            borderRadius: '50%',
                            animation: 'spin 1s linear infinite',
                        }}
                    />
                );
            case 'Pending':
                return <AiOutlineClockCircle size={16} color="#f59e0b" />;
            case 'Cancelled':
                return <AiOutlineClose size={16} color="#6b7280" />;
            default:
                return <AiOutlineClockCircle size={16} color="#6b7280" />;
        }
    };

    useEffect(() => {
        if (currentExecution.status === 'Running') {
            // Poll for updates using EventSource
            const eventSource = new EventSource(`/api/v1/kubectlExecution/${threadId}/${execution.id}/output`);

            eventSource.onmessage = (event: MessageEvent) => {
                const data = JSON.parse(event.data);
                setCurrentExecution(prev => ({
                    ...prev,
                    output: data.output,
                    status: data.status,
                    error: data.error,
                }));

                if (data.completed) {
                    eventSource.close();
                }
            };

            eventSource.onerror = () => {
                eventSource.close();
            };

            return () => eventSource.close();
        }
    }, [currentExecution.status, execution.id, threadId]);

    // Auto-collapse output when execution completes
    useEffect(() => {
        if (currentExecution.status === 'Completed' || currentExecution.status === 'Failed') {
            setIsOutputCollapsed(true);
        }
    }, [currentExecution.status]);

    const handleAction = async (action: 'run' | 'cancel') => {
        setIsActionLoading(true);
        setLoadingAction(action);

        const maxRetries = 3;
        const baseDelay = 1000;

        for (let attempt = 0; attempt <= maxRetries; attempt++) {
            try {
                const response = await axios.post(
                    `/api/v1/kubectlExecution/${threadId}/${execution.id}/action`,
                    {
                        action,
                        user: userIdAndDisplayName?.userId || 'sreagent-client',
                    },
                    { headers: getAgentHeaders() }
                );

                if (response.data) {
                    setCurrentExecution(prev => ({
                        ...prev,
                        status: response.data.status,
                        startedTimestamp: response.data.startedTimestamp || prev.startedTimestamp,
                        executedBy: response.data.executedBy
                            ? {
                                  displayName: response.data.executedBy,
                                  userId: response.data.executedById,
                                  role: 'User',
                              }
                            : prev.executedBy,
                    }));
                }

                // Success - break out of retry loop
                break;
            } catch (error: any) {
                console.error(`Failed to ${action} execution (attempt ${attempt + 1}/${maxRetries + 1}):`, error);

                // If this was the last attempt, handle the error
                if (attempt === maxRetries) {
                    if (error.response?.status === 409) {
                        alert(`Cannot ${action} - execution is already ${error.response.data.currentStatus}`);
                    } else {
                        alert(`Failed to ${action} execution after ${maxRetries + 1} attempts. Please try again.`);
                    }
                } else {
                    // Wait before retrying (exponential backoff)
                    const delay = baseDelay * Math.pow(2, attempt);
                    console.log(`Retrying in ${delay}ms...`);
                    await new Promise(resolve => setTimeout(resolve, delay));
                }
            }
        }

        setIsActionLoading(false);
        setLoadingAction(null);
    };

    // Render collapsed view for non-pending executions
    if (isExecutionCollapsed && currentExecution.status !== 'Pending') {
        return (
            <div
                style={{
                    border: '1px solid #ececec',
                    borderRadius: '8px',
                    padding: '12px',
                    marginTop: '16px',
                    backgroundColor: '#f9f9f9',
                    cursor: 'pointer',
                    transition: 'background-color 0.2s',
                }}
                onClick={() => setIsExecutionCollapsed(false)}
                onMouseEnter={e => (e.currentTarget.style.backgroundColor = '#f0f0f0')}
                onMouseLeave={e => (e.currentTarget.style.backgroundColor = '#f9f9f9')}
            >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <span style={{ fontSize: '16px' }}>⎈</span>
                            <span style={{ fontWeight: '600', fontSize: '14px' }}>Kubernetes</span>
                        </div>

                        {/* Risk indicator */}
                        <span
                            style={{
                                padding: '2px 8px',
                                fontSize: '11px',
                                fontWeight: '500',
                                borderRadius: '12px',
                                backgroundColor: `${riskColor}15`,
                                color: riskColor,
                                border: `1px solid ${riskColor}25`,
                            }}
                        >
                            {riskLevel}
                        </span>

                        {/* Status badge - icon only */}
                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                padding: '4px 6px',
                                borderRadius: '12px',
                                backgroundColor: '#e8e8e8',
                                border: '1px solid #d0d0d0',
                            }}
                        >
                            {getStatusIcon()}
                        </div>

                        {/* Show executor info only if it's not SRE Agent */}
                        {currentExecution.executedBy &&
                            currentExecution.executedBy.displayName !== 'SRE Agent' &&
                            currentExecution.status !== 'Cancelled' && (
                                <span
                                    style={{
                                        fontSize: '11px',
                                        color: '#666',
                                        fontStyle: 'italic',
                                    }}
                                >
                                    Executed by {currentExecution.executedBy.displayName}
                                </span>
                            )}
                    </div>

                    {/* Expand indicator */}
                    <div
                        style={{
                            color: '#666',
                            fontSize: '12px',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '4px',
                        }}
                    >
                        <span>Click to expand</span>
                        <span>▶</span>
                    </div>
                </div>

                {/* Command preview with dark background and copy button */}
                <div
                    style={{
                        marginTop: '8px',
                        position: 'relative',
                        backgroundColor: '#1e1e1e',
                        borderRadius: '6px',
                        padding: '8px 12px',
                        border: '1px solid #333',
                    }}
                >
                    <div
                        style={{
                            fontSize: '13px',
                            color: '#e0e0e0',
                            fontFamily: 'Consolas, Monaco, monospace',
                            fontWeight: '500',
                            lineHeight: '1.4',
                            paddingRight: '40px',
                        }}
                    >
                        {currentExecution.command.length > 80
                            ? `${currentExecution.command.substring(0, 80)}...`
                            : currentExecution.command}
                    </div>

                    {/* Copy button */}
                    <button
                        onClick={async e => {
                            e.stopPropagation();
                            await copyCommand();
                        }}
                        style={{
                            position: 'absolute',
                            top: '6px',
                            right: '8px',
                            background: 'transparent',
                            border: 'none',
                            cursor: 'pointer',
                            color: copied ? '#4CAF50' : '#888',
                            fontSize: '14px',
                            padding: '4px',
                            borderRadius: '4px',
                            transition: 'color 0.2s',
                        }}
                        title={copied ? 'Copied!' : 'Copy command'}
                        onMouseEnter={e => !copied && (e.currentTarget.style.color = '#fff')}
                        onMouseLeave={e => !copied && (e.currentTarget.style.color = '#888')}
                    >
                        <AiOutlineCopy size={16} />
                    </button>
                </div>

                {/* CSS for animations in collapsed view */}
                <style>
                    {`
                        @keyframes spin {
                            0% { transform: rotate(0deg); }
                            100% { transform: rotate(360deg); }
                        }
                    `}
                </style>
            </div>
        );
    }

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
            {/* Header with title, risk, status and collapse button */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span style={{ fontSize: '16px' }}>⎈</span>
                        <h4 style={{ margin: '0', fontWeight: '600' }}>Kubernetes Command</h4>
                    </div>

                    {/* Subtle risk indicator */}
                    <span
                        style={{
                            padding: '2px 8px',
                            fontSize: '11px',
                            fontWeight: '500',
                            borderRadius: '12px',
                            backgroundColor: `${riskColor}15`,
                            color: riskColor,
                            border: `1px solid ${riskColor}25`,
                        }}
                    >
                        {riskLevel}
                    </span>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    {/* Status badge */}
                    <div
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            padding: '6px 12px',
                            borderRadius: '16px',
                            backgroundColor: '#f0f0f0',
                            border: '1px solid #e0e0e0',
                        }}
                    >
                        {getStatusIcon()}
                        <span style={{ fontSize: '13px', fontWeight: '500', color: '#333' }}>{currentExecution.status}</span>
                    </div>

                    {/* Collapse button for non-pending executions */}
                    {currentExecution.status !== 'Pending' && (
                        <button
                            onClick={() => setIsExecutionCollapsed(true)}
                            style={{
                                background: 'transparent',
                                border: 'none',
                                cursor: 'pointer',
                                color: '#666',
                                fontSize: '12px',
                                padding: '4px',
                                borderRadius: '4px',
                            }}
                            title="Collapse"
                            onMouseEnter={e => (e.currentTarget.style.backgroundColor = '#e0e0e0')}
                            onMouseLeave={e => (e.currentTarget.style.backgroundColor = 'transparent')}
                        >
                            ▲
                        </button>
                    )}
                </div>
            </div>

            <p style={{ fontSize: '14px', color: '#666', marginBottom: '12px', margin: '0 0 12px 0' }}>{currentExecution.description}</p>

            {/* Command block with copy button */}
            <div style={{ position: 'relative', backgroundColor: '#1e1e1e', borderRadius: '6px', padding: '12px', marginBottom: '12px' }}>
                <button
                    onClick={copyCommand}
                    style={{
                        position: 'absolute',
                        top: '8px',
                        right: '8px',
                        padding: '6px',
                        backgroundColor: 'transparent',
                        border: 'none',
                        borderRadius: '4px',
                        cursor: 'pointer',
                        color: copied ? '#16a34a' : '#9ca3af',
                        transition: 'color 0.2s',
                    }}
                    title="Copy command"
                    onMouseEnter={e => (e.currentTarget.style.color = '#ffffff')}
                    onMouseLeave={e => (e.currentTarget.style.color = copied ? '#16a34a' : '#9ca3af')}
                >
                    {copied ? <AiOutlineCheckCircle size={16} /> : <AiOutlineCopy size={16} />}
                </button>

                <pre
                    style={{
                        margin: '0',
                        paddingRight: '40px',
                        overflow: 'auto',
                        whiteSpace: 'pre-wrap',
                        wordBreak: 'break-all',
                        fontFamily: 'Consolas, Monaco, monospace',
                        fontSize: '13px',
                        color: '#c9d1d9',
                    }}
                >
                    <code>{currentExecution.command}</code>
                </pre>
            </div>

            {/* Stdin content block */}
            {currentExecution.stdin && currentExecution.stdin.trim() && (
                <div style={{ marginBottom: '12px' }}>
                    <div style={{ fontSize: '13px', fontWeight: '500', color: '#666', marginBottom: '6px' }}>Standard Input:</div>
                    <div style={{ position: 'relative', backgroundColor: '#1e1e1e', borderRadius: '6px', padding: '12px' }}>
                        <button
                            onClick={async () => {
                                try {
                                    await navigator.clipboard.writeText(currentExecution.stdin || '');
                                } catch (err) {
                                    console.error('Failed to copy stdin:', err);
                                }
                            }}
                            style={{
                                position: 'absolute',
                                top: '8px',
                                right: '8px',
                                padding: '6px',
                                backgroundColor: 'transparent',
                                border: 'none',
                                borderRadius: '4px',
                                cursor: 'pointer',
                                color: '#9ca3af',
                                transition: 'color 0.2s',
                            }}
                            title="Copy stdin content"
                            onMouseEnter={e => (e.currentTarget.style.color = '#ffffff')}
                            onMouseLeave={e => (e.currentTarget.style.color = '#9ca3af')}
                        >
                            <AiOutlineCopy size={16} />
                        </button>

                        <pre
                            style={{
                                margin: '0',
                                paddingRight: '40px',
                                overflow: 'auto',
                                whiteSpace: 'pre-wrap',
                                wordBreak: 'break-all',
                                fontFamily: 'Consolas, Monaco, monospace',
                                fontSize: '13px',
                                color: '#fbbf24',
                                maxHeight: '200px',
                                scrollbarWidth: 'thin',
                                scrollbarColor: '#4a5568 #2d3748',
                            }}
                        >
                            <code>{currentExecution.stdin}</code>
                        </pre>
                    </div>
                </div>
            )}

            {/* Action buttons for pending state */}
            {currentExecution.status === 'Pending' && (
                <div style={{ display: 'flex', gap: '8px', marginBottom: '12px' }}>
                    <button
                        onClick={() => handleAction('run')}
                        disabled={isActionLoading}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            padding: '8px 16px',
                            backgroundColor: '#0078D4',
                            color: 'white',
                            border: 'none',
                            borderRadius: '6px',
                            cursor: isActionLoading ? 'not-allowed' : 'pointer',
                            fontWeight: '500',
                            fontSize: '14px',
                            opacity: isActionLoading ? 0.7 : 1,
                            transition: 'all 0.2s',
                        }}
                    >
                        {loadingAction === 'run' ? (
                            <div
                                style={{
                                    width: '14px',
                                    height: '14px',
                                    border: '2px solid #ffffff',
                                    borderTop: '2px solid transparent',
                                    borderRadius: '50%',
                                    animation: 'spin 1s linear infinite',
                                }}
                            />
                        ) : (
                            <AiOutlinePlayCircle size={14} />
                        )}
                        Run
                    </button>

                    <button
                        onClick={() => handleAction('cancel')}
                        disabled={isActionLoading}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '6px',
                            padding: '8px 16px',
                            backgroundColor: '#f5f5f5',
                            color: '#333',
                            border: '1px solid #d0d0d0',
                            borderRadius: '6px',
                            cursor: isActionLoading ? 'not-allowed' : 'pointer',
                            fontWeight: '500',
                            fontSize: '14px',
                            opacity: isActionLoading ? 0.7 : 1,
                            transition: 'all 0.2s',
                        }}
                    >
                        {loadingAction === 'cancel' ? (
                            <div
                                style={{
                                    width: '14px',
                                    height: '14px',
                                    border: '2px solid #333333',
                                    borderTop: '2px solid transparent',
                                    borderRadius: '50%',
                                    animation: 'spin 1s linear infinite',
                                }}
                            />
                        ) : (
                            <AiOutlineClose size={14} />
                        )}
                        Cancel
                    </button>
                </div>
            )}

            {/* Output section */}
            {(currentExecution.output || currentExecution.error) && currentExecution.status !== 'Pending' && (
                <div style={{ marginBottom: '12px' }}>
                    {/* Output header with collapse toggle */}
                    <div
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            padding: '8px 12px',
                            backgroundColor: '#2d3748',
                            borderRadius: isOutputCollapsed ? '6px' : '6px 6px 0 0',
                            cursor: 'pointer',
                            borderBottom: isOutputCollapsed ? 'none' : '1px solid #4a5568',
                        }}
                        onClick={() => setIsOutputCollapsed(!isOutputCollapsed)}
                    >
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <span style={{ color: '#e2e8f0', fontSize: '13px', fontWeight: '500' }}>
                                Output {currentExecution.status === 'Running' ? '(Running...)' : ''}
                            </span>
                            {isOutputCollapsed && (
                                <span style={{ color: '#a0aec0', fontSize: '11px' }}>
                                    {currentExecution.output && currentExecution.error
                                        ? 'Output and error available'
                                        : currentExecution.output
                                          ? 'Output available'
                                          : 'Error available'}
                                </span>
                            )}
                        </div>
                        <div
                            style={{
                                transform: isOutputCollapsed ? 'rotate(-90deg)' : 'rotate(0deg)',
                                transition: 'transform 0.2s',
                                color: '#a0aec0',
                            }}
                        >
                            ▼
                        </div>
                    </div>

                    {/* Collapsible output content */}
                    {!isOutputCollapsed && (
                        <div
                            style={{
                                position: 'relative',
                                backgroundColor: '#1e1e1e',
                                borderRadius: '0 0 6px 6px',
                                padding: '12px',
                                maxHeight: '300px',
                                overflowY: 'auto',
                                // Custom scrollbar styling
                                scrollbarWidth: 'thin',
                                scrollbarColor: '#4a5568 #2d3748',
                            }}
                            className="custom-scrollbar"
                        >
                            <button
                                onClick={copyOutput}
                                style={{
                                    position: 'absolute',
                                    top: '8px',
                                    right: '8px',
                                    padding: '6px',
                                    backgroundColor: 'transparent',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    color: outputCopied ? '#16a34a' : '#6b7280',
                                    transition: 'color 0.2s',
                                    zIndex: 1,
                                    opacity: 0.7,
                                }}
                                title="Copy output"
                                onMouseEnter={e => {
                                    e.currentTarget.style.color = '#ffffff';
                                    e.currentTarget.style.opacity = '1';
                                }}
                                onMouseLeave={e => {
                                    e.currentTarget.style.color = outputCopied ? '#16a34a' : '#6b7280';
                                    e.currentTarget.style.opacity = '0.7';
                                }}
                            >
                                {outputCopied ? <AiOutlineCheckCircle size={16} /> : <AiOutlineCopy size={16} />}
                            </button>

                            <div style={{ paddingRight: '40px' }}>
                                {currentExecution.output && (
                                    <pre
                                        style={{
                                            margin: 0,
                                            whiteSpace: 'pre-wrap',
                                            wordBreak: 'break-word',
                                            color: '#4ade80',
                                            fontSize: '12px',
                                            fontFamily: 'Consolas, Monaco, monospace',
                                        }}
                                    >
                                        {currentExecution.output}
                                    </pre>
                                )}
                                {currentExecution.error && (
                                    <pre
                                        style={{
                                            margin: currentExecution.output ? '8px 0 0 0' : 0,
                                            color: '#f87171',
                                            whiteSpace: 'pre-wrap',
                                            wordBreak: 'break-word',
                                            fontSize: '12px',
                                            fontFamily: 'Consolas, Monaco, monospace',
                                        }}
                                    >
                                        Error: {currentExecution.error}
                                    </pre>
                                )}

                                {currentExecution.status === 'Running' && (
                                    <div
                                        style={{
                                            marginTop: '8px',
                                            display: 'flex',
                                            alignItems: 'center',
                                            gap: '8px',
                                            color: '#60a5fa',
                                            fontSize: '12px',
                                        }}
                                    >
                                        <div
                                            style={{
                                                width: '12px',
                                                height: '12px',
                                                border: '2px solid #60a5fa',
                                                borderTop: '2px solid transparent',
                                                borderRadius: '50%',
                                                animation: 'spin 1s linear infinite',
                                            }}
                                        />
                                        <span>Executing...</span>
                                    </div>
                                )}
                            </div>
                        </div>
                    )}
                </div>
            )}

            {/* Execution metadata */}
            {currentExecution.executedBy && (
                <div style={{ fontSize: '14px', color: '#666', marginBottom: '8px' }}>
                    <strong>{currentExecution.status === 'Cancelled' ? 'Cancelled by' : 'Executed by'}:</strong>{' '}
                    {currentExecution.executedBy.displayName}
                </div>
            )}

            {/* Timestamps */}
            <div style={{ fontSize: '12px', color: '#888', display: 'flex', flexDirection: 'column', gap: '2px' }}>
                <div>
                    <strong>Created:</strong> {new Date(currentExecution.createdTimestamp).toLocaleString()}
                </div>
                {currentExecution.startedTimestamp && (
                    <div>
                        <strong>Started:</strong> {new Date(currentExecution.startedTimestamp).toLocaleString()}
                    </div>
                )}
                {currentExecution.completedTimestamp && (
                    <div>
                        <strong>Completed:</strong> {new Date(currentExecution.completedTimestamp).toLocaleString()}
                    </div>
                )}
                {currentExecution.startedTimestamp && currentExecution.completedTimestamp && (
                    <div>
                        <strong>Duration:</strong>{' '}
                        {Math.round(
                            (new Date(currentExecution.completedTimestamp).getTime() -
                                new Date(currentExecution.startedTimestamp).getTime()) /
                                1000
                        )}
                        s
                    </div>
                )}
            </div>

            {currentExecution.status === 'Pending' && (
                <p
                    style={{
                        fontSize: '11px',
                        color: '#888',
                        marginTop: '12px',
                        marginBottom: '0',
                        fontStyle: 'italic',
                    }}
                >
                    This operation will be executed using your Kubernetes credentials
                </p>
            )}

            {/* CSS for animations */}
            <style>
                {`
                    @keyframes spin {
                        0% { transform: rotate(0deg); }
                        100% { transform: rotate(360deg); }
                    }

                    .custom-scrollbar::-webkit-scrollbar {
                        width: 8px;
                    }

                    .custom-scrollbar::-webkit-scrollbar-track {
                        background: #2d3748;
                        border-radius: 4px;
                    }

                    .custom-scrollbar::-webkit-scrollbar-thumb {
                        background: #4a5568;
                        border-radius: 4px;
                        border: 1px solid #2d3748;
                    }

                    .custom-scrollbar::-webkit-scrollbar-thumb:hover {
                        background: #718096;
                    }
                `}
            </style>
        </div>
    );
};

// Helper function to parse and render markdown with images and mermaid diagrams
const renderMarkdownWithImagesAndMermaid = (text: string) => {
    if (!text) return text;

    // Check for markdown image syntax with base64 data
    const imageRegex = /!\[(.*?)\]\((data:image\/[a-z]+;base64,[A-Za-z0-9+/=]+)\)/g;
    // Check for mermaid code blocks
    const mermaidRegex = /```mermaid\n([\s\S]*?)\n```/g;
    // Check for chart data blocks
    const chartRegex = /```chart-data\n([\s\S]*?)\n```/g;

    if (!imageRegex.test(text) && !mermaidRegex.test(text) && !chartRegex.test(text)) {
        return text; // No special content, return original text
    }

    // Reset regex lastIndex properties to ensure we start from the beginning
    imageRegex.lastIndex = 0;
    mermaidRegex.lastIndex = 0;
    chartRegex.lastIndex = 0;

    // Split images, mermaid blocks, and text
    const parts: (string | { type: string; [key: string]: any })[] = [];
    let lastIndex = 0;

    // Function to process a match and add it to the parts array
    const processMatch = (match: RegExpExecArray, type: string) => {
        if (match.index > lastIndex) {
            parts.push(text.substring(lastIndex, match.index));
        }

        if (type === 'image') {
            parts.push({
                type: 'image',
                alt: match[1],
                src: match[2],
            });
        } else if (type === 'mermaid') {
            parts.push({
                type: 'mermaid',
                content: match[1],
            });
        } else if (type === 'chart-data') {
            parts.push({
                type: 'chart-data',
                content: match[0], // Include the entire match with the markers
            });
        }

        lastIndex = match.index + match[0].length;
    };

    // Find all matches and process them in order of appearance
    let imageMatch: RegExpExecArray | null;
    let mermaidMatch: RegExpExecArray | null;
    let chartMatch: RegExpExecArray | null;

    // Initialize the first matches
    imageMatch = imageRegex.exec(text);
    mermaidMatch = mermaidRegex.exec(text);
    chartMatch = chartRegex.exec(text);

    while (imageMatch || mermaidMatch || chartMatch) {
        // Find the match that appears first in the text
        let firstMatch: RegExpExecArray | null = null;
        let matchType = '';

        if (
            imageMatch &&
            (!mermaidMatch || imageMatch.index < mermaidMatch.index) &&
            (!chartMatch || imageMatch.index < chartMatch.index)
        ) {
            firstMatch = imageMatch;
            matchType = 'image';
            imageMatch = imageRegex.exec(text);
        } else if (mermaidMatch && (!chartMatch || mermaidMatch.index < chartMatch.index)) {
            firstMatch = mermaidMatch;
            matchType = 'mermaid';
            mermaidMatch = mermaidRegex.exec(text);
        } else if (chartMatch) {
            firstMatch = chartMatch;
            matchType = 'chart-data';
            chartMatch = chartRegex.exec(text);
        }

        if (firstMatch) {
            processMatch(firstMatch, matchType);
        }
    }

    // Add any remaining text
    if (lastIndex < text.length) {
        parts.push(text.substring(lastIndex));
    }

    return parts;
};

const ChatMessage = ({
    message,
    previousMessage,
    nextMessage,
    isTyping,
    threadId,
    cancelResponse,
    threadOrchestrationReasoningState,
}: IChatMessageProps) => {
    const chatStyles = useChatBoxStyles();
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [showFeedbackDialog, setShowFeedbackDialog] = useState(false);
    const [selectedFeedback, setSelectedFeedback] = useState<'positive' | 'negative'>();
    const [approvalStatus, setApprovalStatus] = useState<ApprovalDecision | null>(message.approval ? message.approval.status : null);
    const [isApprovalLoading, setIsApprovalLoading] = useState(false);
    const [loadingButton, setLoadingButton] = useState<'approve' | 'deny' | null>(null);

    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const messageContent = useMemo(() => {
        // Make sure we have a text property and it's not empty
        // But if there's an azCliExecution, kubectlExecution, approval, or isDailyReport, it's okay to have no text
        if (
            !message.text &&
            !isTyping &&
            !message.azCliExecution &&
            !message.kubectlExecution &&
            !message.approval &&
            !message.isDailyReport
        ) {
            return 'No message content to display';
        }
        const content = renderMarkdownWithImagesAndMermaid(message.text);
        return Array.isArray(content) ? content : message.text;
    }, [message.text, isTyping, message.azCliExecution, message.kubectlExecution, message.approval, message.isDailyReport]);

    const agentMessageProps = useMemo(() => {
        const messageProps: CopilotMessageProps = {
            avatar: <Image src="./SreAgent.svg" width={28} height={28} alt={intl.formatMessage(SreAgentResources.sreAgent)} />,
            loadingState: isTyping ? 'loading' : 'none',
            mode: 'canvas',
            name: (
                <div style={nameAndTimestampContainerStyle}>
                    <span>{intl.formatMessage(SreAgentResources.sreAgent)}</span>
                    {!isTyping && (
                        <Text size={200} color={tokens.colorNeutralForeground3}>
                            {getSafeDateTime(message.timeStamp).toLocaleString()}
                        </Text>
                    )}
                </div>
            ),
            disclaimer: null,
        };

        return messageProps;
    }, [intl, isTyping, message.timeStamp]);

    // Hide message's icon, name and timestamp if the message is grouped with the previous one
    const hideMessageHeader = useMemo(() => shouldGroupWithPreviousMessage(message, previousMessage), [message, previousMessage]);
    // Show feedback buttons if the message is from SREAgent, not typing and it is the last message in the group
    const showFeedbackButtons = useMemo(
        () => message.author.role === 'SREAgent' && !isTyping && !shouldGroupWithPreviousMessage(nextMessage, message),
        [message, nextMessage, isTyping]
    );

    const handleFeedbackClick = (isPositive: boolean) => {
        setSelectedFeedback(isPositive ? 'positive' : 'negative');
        setShowFeedbackDialog(true);
    };

    // Helper function to extract title from mermaid content
    const extractMermaidTitle = (content: string): string => {
        const lines = content.trim().split('\n');
        if (lines.length === 0) return 'Diagram';

        const firstLine = lines[0];

        if (firstLine.startsWith('%%')) {
            return firstLine.substring(2).trim();
        }

        if (firstLine.startsWith('title:')) {
            return firstLine.substring(6).trim();
        }

        if (firstLine.length < 50 && !firstLine.includes('->') && !firstLine.includes('--')) {
            return firstLine.trim();
        }

        return 'Diagram';
    };

    // Render specific content types
    const renderContentPart = (part: any, index: number): React.ReactNode => {
        // Plain text markdown
        if (typeof part === 'string') {
            return <ReactMarkdownComponent key={index} content={part} />;
        }

        // Handle different content types
        switch (part.type) {
            case 'image':
                return (
                    <div key={index} style={{ margin: '10px 0' }}>
                        <img src={part.src} alt={part.alt || 'Embedded image'} style={{ maxWidth: '100%', borderRadius: '4px' }} />
                        {part.alt && <div style={{ textAlign: 'center', fontSize: '12px', color: '#666' }}>{part.alt}</div>}
                    </div>
                );

            case 'mermaid':
                return <MermaidChart key={index} chart={part.content} title={extractMermaidTitle(part.content)} />;

            case 'chart-data':
                return <AgentChart key={index} messageText={part.content} />;

            default:
                return null;
        }
    };

    // Main content rendering function
    const renderContent = (isUserMessage?: boolean): React.ReactNode => {
        // Check if the entire message is just a incident-alert block
        const incidentAlertRegex = /```incident-alert\s+([\s\S]*?)```/;

        // Check for investigation summary formats
        const investigationSummaryRegex = /<investigation-summary>([\s\S]*?)<\/investigation-summary>/;
        const investigationSummariesRegex = /<investigation-summaries>([\s\S]*?)<\/investigation-summaries>/;

        // Special case: if the whole message is an incident alert, render it directly
        if (typeof message.text === 'string') {
            const incidentMatch = message.text.match(incidentAlertRegex);
            if (incidentMatch && incidentMatch[1]) {
                return <IncidentAlert messageText={message.text} />;
            }

            // Special case: Check for investigation-summaries format (multiple summaries in one container)
            const summariesMatch = message.text.match(investigationSummariesRegex);
            if (summariesMatch && summariesMatch[1]) {
                try {
                    const summariesData = JSON.parse(summariesMatch[1].trim());
                    // Always render the panel even if there are no summaries yet
                    if (summariesData) {
                        // Pass the entire message text directly to the panel component
                        return <InvestigationSummaryPanel messageText={message.text} />;
                    }
                } catch (error) {
                    console.error('Failed to parse investigation summaries:', error);
                }
            }

            // Special case: Check for a single investigation-summary block
            const singleMatch = message.text.match(investigationSummaryRegex);
            if (singleMatch) {
                return <InvestigationSummary messageText={message.text} />;
            }
        }

        // Check if the entire message is just a chart-data block
        const chartRegex = /```chart-data\n([\s\S]*?)\n```/;

        // Special case 3: if the whole message is a chart, render it directly
        if (
            typeof message.text === 'string' &&
            chartRegex.test(message.text) &&
            message.text.trim().replace(/\s+/g, ' ').match(chartRegex)?.[0].length === message.text.trim().length
        ) {
            return <AgentChart messageText={message.text} />;
        }

        // Normal markdown content
        if (!Array.isArray(messageContent)) {
            return <ReactMarkdownComponent content={messageContent} isUserMessage={isUserMessage} />;
        }

        // Mixed content with special blocks
        return <>{messageContent.map(renderContentPart)}</>;
    };

    const sendApprovalDecision = async (threadId: string, approvalId: string, decision: ApprovalDecision) => {
        const url = `${sreAgentEndpoint}/api/v1/approvals/${threadId}/${approvalId}/decision`;

        const response = await axios.post(
            url,
            {
                Status: decision,
                User: userIdAndDisplayName.userId,
            },
            {
                headers: getAgentHeaders(),
            }
        );

        return response.data;
    };

    const handleApprovalDecision = async (approved: boolean) => {
        try {
            if (message.approval) {
                // Check if already approved/rejected
                if (message.approval.status !== ApprovalDecision.Pending) {
                    console.warn(`Approval ${message.approval.id} is already ${message.approval.status}`);
                    return; // Exit early if already decided
                }

                setIsApprovalLoading(true);
                setLoadingButton(approved ? 'approve' : 'deny');
                const approvalData = await sendApprovalDecision(
                    threadId,
                    message.approval.id,
                    approved ? ApprovalDecision.Approved : ApprovalDecision.Rejected
                );

                console.log(`Approval decision sent for message ID: ${message.id}, approved: ${approved}`);

                setApprovalStatus(approvalData.status as ApprovalDecision);
                message.approval = {
                    ...message.approval,
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
            console.error(`Failed to send approval decision for message ID: ${message.id}`, error);

            // Handle specific error cases
            if (error.response?.status === 409) {
                // Conflict - already approved/rejected
                const errorData = error.response?.data;

                if (message.approval && errorData) {
                    message.approval = {
                        ...message.approval,
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

    const renderApprovalContent = () => {
        if (!message.approval) return null;

        // Use the local state for status to ensure UI updates immediately after user action
        const status = approvalStatus || message.approval.status;
        const { title, description } = message.approval;

        // Log approval information to help with debugging
        console.log('Rendering approval with status:', status, 'and title:', title);

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
                    <h4 style={{ margin: '0 0 16px 0' }}>{description}</h4>
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
                        <h4 style={{ margin: '0', fontWeight: '600', maxWidth: '75%' }}>{description}</h4>
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
                        {message.approval.createdTimestamp ? new Date(message.approval.createdTimestamp).toLocaleString() : 'N/A'}
                    </p>

                    {message.approval.decisionUser && (
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
                                {message.approval.decisionUser.displayName}
                            </p>
                            {message.approval.decisionTimestamp && (
                                <p style={{ margin: '4px 0' }}>
                                    <strong>
                                        <FormattedMessage {...SreAgentResources.decisionTime} />:
                                    </strong>{' '}
                                    {new Date(message.approval.decisionTimestamp).toLocaleString()}
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

    // Add a new method to render the daily report
    const renderDailyReport = () => {
        try {
            const dailyReportData = JSON.parse(message.text);
            return <DailyReport data={dailyReportData} timestamp={message.timeStamp} />;
        } catch (e) {
            console.error('Failed to parse daily report:', e);
            return (
                <div>
                    <div style={{ color: 'red', marginBottom: '8px' }}>Failed to parse daily report:</div>
                    <pre style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{message.text}</pre>
                </div>
            );
        }
    };

    switch (message.author.role) {
        case 'SREAgent':
            return (
                <div>
                    <style>{tableStyles}</style>
                    <CopilotMessage
                        {...agentMessageProps}
                        key={message.id}
                        style={{ font: 'Segoe UI', lineHeight: '20px', wordBreak: 'unset', maxWidth: '90%' }}
                        className={mergeClasses(
                            ChatBoxStyles.agentMessage,
                            hideMessageHeader ? ChatBoxStyles.hideAgentMessageHeader : undefined
                        )}
                    >
                        {isTyping && threadOrchestrationReasoningState && <Body1Strong>{threadOrchestrationReasoningState}</Body1Strong>}
                        {/* For messages with approval - text content may be empty, so we may only need to render approval UI */}
                        {message.approval ? (
                            <>{renderApprovalContent()}</>
                        ) : message.isDailyReport ? (
                            <>{renderDailyReport()}</>
                        ) : message.azCliExecution ? (
                            <>
                                <AzCliExecutionComponent execution={message.azCliExecution} threadId={threadId} />
                            </>
                        ) : message.kubectlExecution ? (
                            <>
                                <KubectlExecutionComponent execution={message.kubectlExecution} threadId={threadId} />
                            </>
                        ) : message.text || isTyping ? (
                            renderContent()
                        ) : null}

                        {showFeedbackButtons && ( // Only show buttons when the agent is not typing
                            <FeedbackButtons
                                positiveFeedbackButton={{ onClick: () => handleFeedbackClick(true) }}
                                negativeFeedbackButton={{ onClick: () => handleFeedbackClick(false) }}
                                selected={selectedFeedback}
                            />
                        )}
                    </CopilotMessage>

                    {isTyping && (
                        <Button
                            icon={<SquareDismissRegular />}
                            onClick={() => cancelResponse?.()}
                            appearance="transparent"
                            style={{
                                width: '90%',
                                marginTop: '12px',
                                maxWidth: 'none',
                                display: 'flex',
                                flexDirection: 'row',
                                justifyContent: 'flex-end',
                                padding: '0px',
                            }}
                        />
                    )}

                    <FeedbackDialog
                        isOpen={showFeedbackDialog}
                        setIsOpen={setShowFeedbackDialog}
                        threadId={threadId}
                        isPositiveFeedback={selectedFeedback === 'positive'}
                    />
                </div>
            );
        default:
            return (
                <div className={ChatBoxStyles.userMessage} key={message.id}>
                    {hideMessageHeader ? null : (
                        <div style={nameAndTimestampContainerStyle}>
                            {message.author.userId !== userIdAndDisplayName.userId && (
                                <Text block={true} weight={'semibold'} className={chatStyles.userName}>
                                    {message.author.displayName}
                                </Text>
                            )}
                            <Text size={200} color={tokens.colorNeutralForeground3} style={{ lineHeight: '26px' }}>
                                {getSafeDateTime(message.timeStamp).toLocaleString()}
                            </Text>
                        </div>
                    )}
                    <UserMessage className={chatStyles.userBubble} message={{ className: chatStyles.userBubbleMessage }} key={message.id}>
                        {renderContent(true)}
                    </UserMessage>
                </div>
            );
    }
};

const ReactMarkdownComponent = ({
    key,
    content,
    isUserMessage,
}: {
    key?: string | number;
    content?: string | null;
    isUserMessage?: boolean;
}) => {
    const aLinkRenderer = useCallback((props: any) => {
        return (
            <a href={props.href} target="_blank" rel="noopener noreferrer">
                {props.children}
            </a>
        );
    }, []);

    const codeRenderer = useCallback((props: any) => {
        // Check if this code element is inside a pre element (code block)
        const isInPre = props.node?.parent?.tagName === 'pre';
        const className = isInPre ? chatMessageStyles.codeBlockInPre : chatMessageStyles.codeBlock;
        return <code className={className}>{props.children}</code>;
    }, []);

    const preRenderer = useCallback((props: any) => {
        return <pre className={chatMessageStyles.preBlock}>{props.children}</pre>;
    }, []);

    return (
        <div key={key} className={mergeClasses('markdown-content', isUserMessage ? undefined : chatMessageStyles.regularMessageContent)}>
            <ReactMarkdown
                components={{
                    a: aLinkRenderer,
                    code: codeRenderer,
                    pre: preRenderer,
                }}
                remarkPlugins={[remarkGfm]}
                rehypePlugins={[rehypeRaw]}
            >
                {content}
            </ReactMarkdown>
        </div>
    );
};

export default memo(ChatMessage);
