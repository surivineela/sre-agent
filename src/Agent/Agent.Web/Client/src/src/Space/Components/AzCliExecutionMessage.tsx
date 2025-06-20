import axios from 'axios';
import { FC, useEffect, useState } from 'react';
import {
    AiOutlineCheckCircle,
    AiOutlineClockCircle,
    AiOutlineClose,
    AiOutlineCloseCircle,
    AiOutlineCopy,
    AiOutlinePlayCircle,
} from 'react-icons/ai';
import { AzCliExecution } from '../../Common/Contracts/Azure/SreAgent';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { getRiskColor, getRiskLevel } from './Utility';

// Azure CLI Execution Component
const AzCliExecutionMessage: FC<{
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
        setCurrentExecution({ ...execution });
    }, [execution]);

    useEffect(() => {
        if (currentExecution.status !== 'Running') {
            return;
        }

        let isPolling = true;
        const pollInterval: NodeJS.Timeout = setInterval(async () => {
            if (!isPolling) return;

            try {
                const response = await fetch(`/api/v1/azCliExecution/${threadId}/${execution.id}/output`, {
                    headers: getAgentHeaders(),
                    cache: 'no-cache',
                });

                if (!response.ok) {
                    console.error('Failed to fetch execution output:', response.status, response.statusText);
                    return;
                }

                const data = await response.json();

                if (isPolling) {
                    setCurrentExecution(prev => ({
                        ...prev,
                        output: data.output || prev.output,
                        status: data.status || prev.status,
                        error: data.error || prev.error,
                        completedTimestamp: data.completedTimestamp || prev.completedTimestamp,
                    }));

                    // Stop polling if execution is complete
                    if (data.completed) {
                        isPolling = false;
                        clearInterval(pollInterval);
                    }
                }
            } catch (error) {
                console.error('Error polling for execution output:', error);
            }
        }, 2000);

        return () => {
            isPolling = false;
            clearInterval(pollInterval);
        };
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
                    marginTop: '8px',
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

                    {/* Status badge moved to right */}
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
                            color: copied ? '#16a34a' : '#888',
                            fontSize: '14px',
                            padding: '4px',
                            borderRadius: '4px',
                            transition: 'color 0.2s',
                        }}
                        title={copied ? 'Copied!' : 'Copy command'}
                        onMouseEnter={e => !copied && (e.currentTarget.style.color = '#fff')}
                        onMouseLeave={e => !copied && (e.currentTarget.style.color = '#888')}
                    >
                        {copied ? <AiOutlineCheckCircle size={16} /> : <AiOutlineCopy size={16} />}
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
                marginTop: '8px',
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

export default AzCliExecutionMessage;
