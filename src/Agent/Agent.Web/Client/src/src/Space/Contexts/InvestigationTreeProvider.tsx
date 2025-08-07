import React, { Component, ReactNode, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentTaskDevClient } from '../../Common/Clients/AgentTaskDevClient';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import {
    AgentTask,
    FormingHypothesisStatus,
    HypothesisStatus,
    HypothesisTreeItem,
    InitialInvestigationStatus,
    TaskProgressUpdate,
} from '../../Common/Contracts/Azure/AgentTaskDevTypes';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { StreamingContext } from '../Contracts/Context';
import { InvestigationTreeContext, InvestigationTreeNode, InvestigationTreeState } from './InvestigationTreeContext';

export { InvestigationTreeContext };

interface InvestigationTreeProviderProps {
    children: ReactNode;
    threadId?: string;
}

interface ErrorBoundaryState {
    hasError: boolean;
    error?: Error;
}

class InvestigationTreeErrorBoundary extends Component<{ children: ReactNode }, ErrorBoundaryState> {
    constructor(props: { children: ReactNode }) {
        super(props);
        this.state = { hasError: false };
    }

    static getDerivedStateFromError(error: Error): ErrorBoundaryState {
        return { hasError: true, error };
    }

    render() {
        if (this.state.hasError) {
            return null;
        }

        return this.props.children;
    }
}

export interface InvestigationTreeContextProps {
    treeState: InvestigationTreeState;
    updateFromTaskProgress: (update: TaskProgressUpdate) => void;
    updateFromTaskUpdate: (task: AgentTask) => void;
    loadSavedAgentTasks: (threadId: string) => Promise<void>;
    toggleNodeExpanded: (nodeId: string) => void;
    clearTree: () => void;
    showTree: () => void;
    hideTree: () => void;
    resetTree: () => void;
    forceRefresh: () => void;
}

export const InvestigationTreeProvider: React.FC<InvestigationTreeProviderProps> = ({ children, threadId }) => {
    const [treeState, setTreeState] = useState<InvestigationTreeState>({
        nodes: new Map(),
        rootNodes: [],
        currentTask: undefined,
        isVisible: false,
        isLoading: false,
    });

    // Track the highest status reached for each phase to prevent backwards progression
    const phaseProgressRef = useRef<Map<string, string>>(new Map());

    // Track the highest status reached for each hypothesis to prevent backwards progression
    const hypothesisProgressRef = useRef<Map<string, string>>(new Map());

    // Get streaming context to subscribe to task updates
    const streamingContext = useContext(StreamingContext);

    // Get environment context for API clients
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const agentTaskDevClient = AgentTaskDevClient.getInstance(sreAgentEndpoint);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    // Check if agent task feature is enabled
    const showAgentTaskDev = useConfigSetting(SettingNames.ShowAgentTaskDev);

    // Load saved agent tasks when threadId changes
    useEffect(() => {
        if (threadId) {
            console.log('🔄 Thread changed, resetting tree for threadId:', threadId);
            // Reset the tree state when thread changes - don't show until there's actual activity
            setTreeState(prevState => ({
                ...prevState,
                nodes: new Map(),
                rootNodes: [],
                currentTask: undefined,
                isVisible: false, // Hide by default, show only when there's activity
                isLoading: false,
            }));

            // Clear progress trackers for new thread
            phaseProgressRef.current.clear();
            hypothesisProgressRef.current.clear();
        } else {
            console.log('🚫 No threadId, clearing tree');
            setTreeState(prevState => ({
                ...prevState,
                nodes: new Map(),
                rootNodes: [],
                currentTask: undefined,
                isVisible: false,
                isLoading: false,
            }));

            // Clear progress trackers
            phaseProgressRef.current.clear();
            hypothesisProgressRef.current.clear();
        }
    }, [threadId]);

    // Status progression order: started -> in_progress -> completed
    const getStatusLevel = (status: string): number => {
        switch (status) {
            case 'started':
                return 1;
            case 'in_progress':
                return 2;
            case 'completed':
                return 3;
            case 'failed':
                return 3; // Treat failed as final like completed
            default:
                return 0;
        }
    };

    // Hypothesis status progression order: pending -> validating -> validated/invalidated/inconclusive
    const getHypothesisStatusLevel = (status: string): number => {
        switch (status.toLowerCase()) {
            case 'pending':
                return 1;
            case 'validating':
                return 2;
            case 'validated':
                return 3;
            case 'invalidated':
                return 3;
            case 'inconclusive':
                return 3;
            default:
                return 0;
        }
    };

    // Check if this status change is allowed (forward progression only)
    const isStatusProgressionAllowed = (taskId: string, phase: string, newStatus: string): boolean => {
        const phaseKey = `${taskId}-${phase}`;
        const currentStatus = phaseProgressRef.current.get(phaseKey);

        if (!currentStatus) {
            // First time seeing this phase, allow any status
            return true;
        }

        const currentLevel = getStatusLevel(currentStatus);
        const newLevel = getStatusLevel(newStatus);

        // Only allow forward progression or same level
        return newLevel >= currentLevel;
    };

    // Check if this hypothesis status change is allowed (forward progression only)
    const isHypothesisStatusProgressionAllowed = (hypothesisId: string, newStatus: string): boolean => {
        const currentStatus = hypothesisProgressRef.current.get(hypothesisId);

        if (!currentStatus) {
            // First time seeing this hypothesis, allow any status
            return true;
        }

        const currentLevel = getHypothesisStatusLevel(currentStatus);
        const newLevel = getHypothesisStatusLevel(newStatus);

        // Only allow forward progression or same level
        return newLevel >= currentLevel;
    };

    // Helper function to get existing expanded state for a node
    const getExistingExpandedState = (nodeId: string, prevState: InvestigationTreeState): boolean => {
        const existingNode = prevState.nodes.get(nodeId);
        return existingNode?.expanded ?? true; // Default to true if no existing node
    };

    const updateFromTaskProgress = useCallback(
        (update: TaskProgressUpdate) => {
            try {
                // Handle both lowercase and uppercase property names
                const taskId = update.taskId || (update as any).TaskId || 'unknown';
                const phase = update.phase || (update as any).Phase || 'unknown';
                const status = update.status || (update as any).Status || 'unknown';
                const message = update.message || (update as any).Message || 'No message';
                //const timestamp = update.timestamp || (update as any).Timestamp || Date.now().toString();

                // Check if this status progression is allowed (prevent backwards progression)
                if (!isStatusProgressionAllowed(taskId, phase, status)) {
                    return; // Skip this update completely
                }

                // Update the phase progress tracker
                const phaseKey = `${taskId}-${phase}`;
                phaseProgressRef.current.set(phaseKey, status);

                setTreeState(prevState => {
                    if (!prevState) {
                        return prevState;
                    }

                    // Skip test tasks
                    if (taskId.startsWith('debug-')) {
                        return prevState;
                    }

                    const newState = { ...prevState };

                    // If this is a new task, clear previous phase progress
                    if (prevState.currentTask?.taskId !== taskId) {
                        phaseProgressRef.current.clear();
                        hypothesisProgressRef.current.clear();
                    }

                    // Update current task info
                    newState.currentTask = {
                        taskId,
                        phase,
                        status,
                        message,
                    };

                    const newNodes = new Map(prevState.nodes || new Map());
                    // Create a copy of root nodes to avoid mutation
                    let newRootNodes = [...(prevState.rootNodes || [])];

                    // Create a stable ID for this phase (without timestamp to avoid duplicates)
                    const phaseNodeId = `${taskId}-${phase}`;

                    // Check if we already have a node for this main phase type
                    const existingPhaseNodes = newRootNodes.filter(
                        node => node.nodeType === 'phase' && node.title.toLowerCase().includes(phase.replace(/_/g, ' ').toLowerCase())
                    );

                    // Skip conclusion and validating_hypothesis phases - they're handled separately
                    if (phase !== 'conclusion' && phase !== 'validating_hypothesis') {
                        if (existingPhaseNodes.length === 0) {
                            // Create new phase node
                            const phaseNode: InvestigationTreeNode = {
                                id: phaseNodeId,
                                title: phase.replace(/_/g, ' ').replace(/\b\w/g, (l: string) => l.toUpperCase()),
                                description: message,
                                children: [],
                                expanded: getExistingExpandedState(phaseNodeId, prevState),
                                isValidating: false,
                                isLoading: status === 'in_progress' || status === 'started',
                                parentId: undefined,
                                nodeType: 'phase',
                                status,
                            };

                            newNodes.set(phaseNodeId, phaseNode);
                            newRootNodes.push(phaseNode);
                        } else {
                            // Update existing phase node
                            const existingPhaseNode = existingPhaseNodes[0];
                            const updatedPhaseNode = {
                                ...existingPhaseNode,
                                description: message,
                                status,
                                isLoading: status === 'in_progress' || status === 'started',
                            };

                            newNodes.set(existingPhaseNode.id, updatedPhaseNode);

                            // Update in root nodes
                            const rootIndex = newRootNodes.findIndex(node => node.id === existingPhaseNode.id);
                            if (rootIndex !== -1) {
                                newRootNodes[rootIndex] = updatedPhaseNode;
                            }
                        }
                    }

                    // Handle different status types
                    if (status === 'completed') {
                        // Mark all nodes as completed
                        newRootNodes.forEach(node => {
                            if (node.nodeType === 'phase') {
                                const updatedNode = {
                                    ...node,
                                    status: 'completed',
                                    isValidating: false,
                                    isLoading: false,
                                };
                                newNodes.set(node.id, updatedNode);
                            }
                        });
                    }

                    // Handle hypothesis updates - during forming phase, add as root nodes; during validation, add as children
                    const hypothesisUpdate = update.hypothesisUpdate || (update as any).HypothesisUpdate;
                    const hypothesisAction = update.hypothesisAction || (update as any).HypothesisAction;

                    if (hypothesisAction) {
                        let action = hypothesisAction;

                        // If no explicit action, infer from the phase and message
                        if (!action) {
                            if (phase === 'forming_hypothesis' && message.toLowerCase().includes('generated')) {
                                action = 'add';
                            } else if (phase === 'validating_hypothesis' && message.toLowerCase().includes('validating')) {
                                action = 'validate';
                            } else if (message.toLowerCase().includes('validated') || message.toLowerCase().includes('invalidated')) {
                                action = 'update';
                            }
                        }

                        if (hypothesisUpdate) {
                            // Handle normal hypothesis updates with data
                            const hypothesis = hypothesisUpdate;
                            let hypothesisNodeId =
                                hypothesis.id || hypothesis.Id || `hypothesis-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

                            // Check if hypothesis already exists to prevent duplicates
                            let existingHypothesis = newNodes.get(hypothesisNodeId);

                            // If not found by ID, try to find by title and description (for updates)
                            if (!existingHypothesis && (action === 'update' || action === 'validate')) {
                                existingHypothesis = Array.from(newNodes.values()).find(
                                    node =>
                                        node.nodeType === 'hypothesis' &&
                                        node.title === (hypothesis.title || hypothesis.Title) &&
                                        node.description === (hypothesis.description || hypothesis.Description)
                                );

                                // If found by title/description, use its ID for the update
                                if (existingHypothesis) {
                                    hypothesisNodeId = existingHypothesis.id;
                                }

                                // If still not found, try to find by title only (more flexible)
                                if (!existingHypothesis) {
                                    existingHypothesis = Array.from(newNodes.values()).find(
                                        node => node.nodeType === 'hypothesis' && node.title === (hypothesis.title || hypothesis.Title)
                                    );

                                    if (existingHypothesis) {
                                        hypothesisNodeId = existingHypothesis.id;
                                    }
                                }
                            }

                            if (action === 'add') {
                                if (!existingHypothesis) {
                                    // For new hypotheses, always start with 'pending' status
                                    // The backend sets them as 'Inconclusive' but we want to show 'pending' initially
                                    const statusString = 'pending';

                                    const newHypothesisNode: InvestigationTreeNode = {
                                        id: hypothesisNodeId,
                                        title: hypothesis.title || hypothesis.Title,
                                        description: hypothesis.description || hypothesis.Description,
                                        status: statusString,
                                        parentHypothesisDescription:
                                            hypothesis.parentHypothesisDescription || hypothesis.ParentHypothesisDescription || undefined,
                                        children: [],
                                        expanded: getExistingExpandedState(hypothesisNodeId, prevState), // Preserve existing expanded state
                                        isValidating: false,
                                        isLoading: false,
                                        parentId: undefined,
                                        nodeType: 'hypothesis',
                                        // Include detailed validation steps
                                        steps: hypothesis.steps || [],
                                    };

                                    newNodes.set(hypothesisNodeId, newHypothesisNode);

                                    // Check if this is a child hypothesis or initial hypothesis
                                    const parentDesc = hypothesis.parentHypothesisDescription || hypothesis.ParentHypothesisDescription;
                                    if (!parentDesc || parentDesc === '') {
                                        // This is an initial hypothesis - add directly to the Initial Investigation phase
                                        const initialInvestigationPhase = newRootNodes.find(
                                            node => node.nodeType === 'phase' && node.title.toLowerCase().includes('initial investigation')
                                        );

                                        if (initialInvestigationPhase) {
                                            newHypothesisNode.parentId = initialInvestigationPhase.id;
                                            initialInvestigationPhase.children = [...initialInvestigationPhase.children, newHypothesisNode];
                                            newNodes.set(initialInvestigationPhase.id, initialInvestigationPhase);

                                            // Update the phase node in root nodes
                                            const rootIndex = newRootNodes.findIndex(node => node.id === initialInvestigationPhase.id);
                                            if (rootIndex !== -1) {
                                                newRootNodes[rootIndex] = initialInvestigationPhase;
                                            }
                                        } else {
                                            // If no Initial Investigation phase exists, add as root node (fallback)
                                            newRootNodes.push(newHypothesisNode);
                                        }
                                    } else {
                                        // This is a child hypothesis - find parent and add as child
                                        const parentHypothesis = Array.from(newNodes.values()).find(
                                            node => node.nodeType === 'hypothesis' && node.description === parentDesc
                                        );

                                        if (parentHypothesis) {
                                            newHypothesisNode.parentId = parentHypothesis.id;
                                            parentHypothesis.children = [...parentHypothesis.children, newHypothesisNode];
                                            newNodes.set(parentHypothesis.id, parentHypothesis);

                                            // Update the parent in its parent's children array
                                            newRootNodes = updateNodeInParent(parentHypothesis, newNodes, newRootNodes);
                                        } else {
                                            // If parent not found, add to Initial Investigation as fallback
                                            const initialInvestigationPhase = newRootNodes.find(
                                                node =>
                                                    node.nodeType === 'phase' && node.title.toLowerCase().includes('initial investigation')
                                            );

                                            if (initialInvestigationPhase) {
                                                newHypothesisNode.parentId = initialInvestigationPhase.id;
                                                initialInvestigationPhase.children = [
                                                    ...initialInvestigationPhase.children,
                                                    newHypothesisNode,
                                                ];
                                                newNodes.set(initialInvestigationPhase.id, initialInvestigationPhase);

                                                // Update the phase node in root nodes
                                                const rootIndex = newRootNodes.findIndex(node => node.id === initialInvestigationPhase.id);
                                                if (rootIndex !== -1) {
                                                    newRootNodes[rootIndex] = initialInvestigationPhase;
                                                }
                                            } else {
                                                // Last resort: add as root node
                                                newRootNodes.push(newHypothesisNode);
                                            }
                                        }
                                    }
                                } else {
                                    // Check if this add action contains a final status update
                                    const hypothesisStatus = hypothesis.status || hypothesis.Status;
                                    let statusString = hypothesisStatus;

                                    // Convert numeric status to string if needed
                                    if (typeof hypothesisStatus === 'number') {
                                        switch (hypothesisStatus) {
                                            case 0:
                                                statusString = 'pending';
                                                break;
                                            case 1:
                                                statusString = 'validated';
                                                break;
                                            case 2:
                                                statusString = 'invalidated';
                                                break;
                                            case 3:
                                                statusString = 'inconclusive';
                                                break;
                                            default:
                                                statusString = 'pending';
                                                break;
                                        }
                                    } else if (typeof hypothesisStatus === 'string') {
                                        statusString = hypothesisStatus.toLowerCase();
                                    }

                                    // Only ignore if it would overwrite a validating status with pending/inconclusive
                                    const isFinalState =
                                        statusString === 'validated' || statusString === 'invalidated' || statusString === 'inconclusive';
                                    const wouldOverwriteValidating = existingHypothesis.status === 'validating' && !isFinalState;

                                    if (wouldOverwriteValidating) {
                                        // Ignore the update to preserve validating status
                                    } else {
                                        // Allow the update if it's a final state or not overwriting validating

                                        const updatedHypothesis = {
                                            ...existingHypothesis,
                                            title: hypothesis.title || hypothesis.Title,
                                            description: hypothesis.description || hypothesis.Description,
                                            status: statusString,
                                            isValidating: false,
                                            isLoading: false,
                                            // Update steps if available
                                            steps: hypothesis.steps || existingHypothesis.steps,
                                        };

                                        newNodes.set(hypothesisNodeId, updatedHypothesis);
                                        newRootNodes = updateNodeInParent(updatedHypothesis, newNodes, newRootNodes);
                                    }
                                }
                            } else if (action === 'update' && existingHypothesis) {
                                // Update existing hypothesis
                                const hypothesisStatus = hypothesis.status || hypothesis.Status;

                                // Convert numeric status to string if needed
                                let statusString = hypothesisStatus;
                                if (typeof hypothesisStatus === 'number') {
                                    switch (hypothesisStatus) {
                                        case 0:
                                            statusString = 'pending';
                                            break;
                                        case 1:
                                            statusString = 'validated';
                                            break;
                                        case 2:
                                            statusString = 'invalidated';
                                            break;
                                        case 3:
                                            statusString = 'inconclusive';
                                            break; // Inconclusive for actual inconclusive results
                                        default:
                                            statusString = 'pending';
                                            break;
                                    }
                                } else if (typeof hypothesisStatus === 'string') {
                                    // Handle string status values
                                    statusString = hypothesisStatus.toLowerCase();
                                }

                                // Check if status progression is allowed
                                if (!isHypothesisStatusProgressionAllowed(hypothesisNodeId, statusString)) {
                                    // Skip this update - don't modify the hypothesis
                                } else {
                                    // Update progress tracker
                                    hypothesisProgressRef.current.set(hypothesisNodeId, statusString);
                                    const updatedHypothesis = {
                                        ...existingHypothesis,
                                        title: hypothesis.title || hypothesis.Title,
                                        description: hypothesis.description || hypothesis.Description,
                                        status: statusString,
                                        isValidating: false,
                                        isLoading: false,
                                        // Update steps if available
                                        steps: hypothesis.steps || existingHypothesis.steps,
                                    };

                                    newNodes.set(hypothesisNodeId, updatedHypothesis);

                                    // Update in parent's children array
                                    newRootNodes = updateNodeInParent(updatedHypothesis, newNodes, newRootNodes);
                                }
                            } else if (action === 'validate' && existingHypothesis) {
                                // Check if status progression is allowed
                                if (!isHypothesisStatusProgressionAllowed(hypothesisNodeId, 'validating')) {
                                    // Skip this update - don't modify the hypothesis
                                } else {
                                    // Update progress tracker
                                    hypothesisProgressRef.current.set(hypothesisNodeId, 'validating');
                                    // Mark hypothesis as being validated
                                    const updatedHypothesis = {
                                        ...existingHypothesis,
                                        status: 'validating',
                                        isValidating: true,
                                        isLoading: true,
                                    };
                                    newNodes.set(hypothesisNodeId, updatedHypothesis);

                                    // Update in parent's children array
                                    newRootNodes = updateNodeInParent(updatedHypothesis, newNodes, newRootNodes);
                                }
                            } else if (action === 'validate' && !existingHypothesis) {
                                // Validation action received but hypothesis not found - this can happen with timing issues
                            }
                        }
                    }

                    // Handle hypothesis updates without explicit action (inferred from phase and message)
                    if (hypothesisUpdate && !hypothesisAction) {
                        const hypothesis = hypothesisUpdate;
                        const hypothesisNodeId =
                            hypothesis.id || hypothesis.Id || `hypothesis-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

                        // Check if hypothesis already exists to prevent duplicates
                        const existingHypothesis = newNodes.get(hypothesisNodeId);

                        // During forming phase, treat as add
                        if (phase === 'forming_hypothesis' && message.toLowerCase().includes('generated') && !existingHypothesis) {
                            // For new hypotheses, always start with 'pending' status
                            const statusString = 'pending';

                            const newHypothesisNode: InvestigationTreeNode = {
                                id: hypothesisNodeId,
                                title: hypothesis.title || hypothesis.Title,
                                description: hypothesis.description || hypothesis.Description,
                                status: statusString,
                                parentHypothesisDescription:
                                    hypothesis.parentHypothesisDescription || hypothesis.ParentHypothesisDescription || undefined,
                                children: [],
                                expanded: getExistingExpandedState(hypothesisNodeId, prevState),
                                isValidating: false,
                                isLoading: false,
                                parentId: undefined,
                                nodeType: 'hypothesis',
                            };

                            newNodes.set(hypothesisNodeId, newHypothesisNode);

                            // Add directly to the Initial Investigation phase node
                            const initialInvestigationPhase = newRootNodes.find(
                                node => node.nodeType === 'phase' && node.title.toLowerCase().includes('initial investigation')
                            );

                            if (initialInvestigationPhase) {
                                newHypothesisNode.parentId = initialInvestigationPhase.id;
                                initialInvestigationPhase.children = [...initialInvestigationPhase.children, newHypothesisNode];
                                newNodes.set(initialInvestigationPhase.id, initialInvestigationPhase);

                                // Update the phase node in root nodes
                                const rootIndex = newRootNodes.findIndex(node => node.id === initialInvestigationPhase.id);
                                if (rootIndex !== -1) {
                                    newRootNodes[rootIndex] = initialInvestigationPhase;
                                }
                            } else {
                                // If no Initial Investigation phase exists, add as root node (fallback)
                                newRootNodes.push(newHypothesisNode);
                            }
                        } else if (existingHypothesis) {
                            // Hypothesis already exists, skipping inferred add
                        }
                    }

                    // Handle investigation summary streaming
                    if (phase === 'initial_investigation' && status === 'completed' && (update.summary || (update as any).Summary)) {
                        const summary = update.summary || (update as any).Summary;
                        const initialInvestigationPhase = newRootNodes.find(
                            node => node.nodeType === 'phase' && node.title.toLowerCase().includes('initial investigation')
                        );

                        if (initialInvestigationPhase) {
                            const updatedPhaseNode = {
                                ...initialInvestigationPhase,
                                description: summary,
                                status: 'completed',
                                isLoading: false,
                            };

                            newNodes.set(initialInvestigationPhase.id, updatedPhaseNode);

                            // Update in root nodes
                            const rootIndex = newRootNodes.findIndex(node => node.id === initialInvestigationPhase.id);
                            if (rootIndex !== -1) {
                                newRootNodes[rootIndex] = updatedPhaseNode;
                            }
                        }
                    }

                    // Handle conclusion streaming (both in progress and completed)
                    if (phase === 'conclusion') {
                        // Use a stable ID for the conclusion node
                        const conclusionNodeId = `${taskId}-conclusion`;
                        // Find existing conclusion phase node by ID
                        const conclusionPhase = newNodes.get(conclusionNodeId);

                        let conclusionTitle = 'Conclusion';
                        let conclusionSummary = '';
                        // Check if we have a conclusion object
                        if (update.conclusion || (update as any).Conclusion) {
                            const conclusion = update.conclusion || (update as any).Conclusion;
                            conclusionTitle = conclusion.title || conclusion.Title || 'Conclusion';
                            conclusionSummary = conclusion.summary || conclusion.Summary || '';
                        }
                        // Get top-level summary
                        const topLevelSummary = update.summary || (update as any).Summary || '';
                        // Combine summaries
                        let combinedSummary = '';
                        if (status === 'completed' && topLevelSummary) {
                            combinedSummary = topLevelSummary;
                        } else if (conclusionSummary) {
                            combinedSummary = conclusionSummary;
                        } else if (topLevelSummary) {
                            combinedSummary = topLevelSummary;
                        }

                        if (conclusionPhase) {
                            // Update existing conclusion node
                            const updatedPhaseNode = {
                                ...conclusionPhase,
                                title: conclusionTitle,
                                description: combinedSummary,
                                status: status,
                                isLoading: status === 'in_progress' || status === 'started',
                            };
                            newNodes.set(conclusionNodeId, updatedPhaseNode);
                            // Update in root nodes
                            const rootIndex = newRootNodes.findIndex(node => node.id === conclusionNodeId);
                            if (rootIndex !== -1) {
                                newRootNodes[rootIndex] = updatedPhaseNode;
                            }
                        } else {
                            // Create new conclusion phase node only if it doesn't exist
                            const newConclusionNode: InvestigationTreeNode = {
                                id: conclusionNodeId,
                                title: conclusionTitle,
                                description: combinedSummary,
                                status: status,
                                children: [],
                                expanded: getExistingExpandedState(conclusionNodeId, prevState),
                                isValidating: false,
                                isLoading: status === 'in_progress' || status === 'started',
                                parentId: undefined,
                                nodeType: 'phase',
                            };
                            newNodes.set(conclusionNodeId, newConclusionNode);
                            newRootNodes.push(newConclusionNode);
                        }
                    }

                    // Update the state with the new nodes and root nodes
                    newState.nodes = newNodes;
                    newState.rootNodes = newRootNodes;

                    console.log('📊 Tree state update:', {
                        nodeCount: newNodes.size,
                        rootNodeCount: newRootNodes.length,
                        rootNodeTitles: newRootNodes.map(n => n.title),
                        currentTask: newState.currentTask,
                    });

                    // Only make tree visible when we actually have investigation content (root nodes)
                    if (!newState.isVisible && newRootNodes.length > 0) {
                        console.log('🌳 Making tree visible - root nodes count:', newRootNodes.length);
                        newState.isVisible = true;
                    }
                    return newState;
                });
            } catch (error) {
                // Still return to prevent further errors from propagating
                return;
            }
        },
        [isStatusProgressionAllowed, isHypothesisStatusProgressionAllowed]
    );

    const updateFromTaskUpdate = useCallback((task: AgentTask) => {
        console.log('🚀 updateFromTaskUpdate called with task:', task);
        try {
            console.log('✅ Task update received:', task);

            setTreeState(prevState => {
                if (!prevState) {
                    console.warn('Previous state is null or undefined');
                    return prevState;
                }

                const newState = { ...prevState };

                // Show tree automatically when any task update is received
                if (!newState.isVisible) {
                    console.log('Making tree visible due to task update');
                    newState.isVisible = true;
                }

                // Update current task info
                // newState.currentTask = {
                //     taskId: task.id,
                //     phase: 'task_update',
                //     status: task.status,
                //     message: `Task ${task.title} - ${task.status}`,
                // };

                const newNodes = new Map(prevState.nodes || new Map());
                const newRootNodes: InvestigationTreeNode[] = [];

                console.log('🔍 Task details:', {
                    type: task.type,
                    id: task.id,
                    title: task.title,
                    status: task.status,
                    hasProperties: !!task.properties,
                });

                // Build tree from task properties if it's an incident investigation task
                if (task.type === 'IncidentInvestigation' && task.properties) {
                    const properties = task.properties; // Type assertion for now

                    // Handle Initial Investigation
                    if (properties.initialInvestigation) {
                        const initialInvestigation = properties.initialInvestigation;
                        const initialInvestigationNode: InvestigationTreeNode = {
                            id: `${task.id}-initial-investigation`,
                            title: 'Initial Investigation',
                            description:
                                initialInvestigation.summary ||
                                initialInvestigation.statusMessage ||
                                'Initial investigation in progress...',
                            status: initialInvestigation.status === InitialInvestigationStatus.Complete ? 'completed' : 'in_progress',
                            children: [],
                            expanded: getExistingExpandedState(`${task.id}-initial-investigation`, prevState),
                            isValidating: false,
                            isLoading: initialInvestigation.status === InitialInvestigationStatus.InProgress,
                            parentId: undefined,
                            nodeType: 'phase',
                            // Include detailed gathering context steps
                            gatheringContextSteps: initialInvestigation.gatheringContext?.steps || [],
                        };
                        newNodes.set(initialInvestigationNode.id, initialInvestigationNode);
                        newRootNodes.push(initialInvestigationNode);
                    }

                    // Handle Forming Hypothesis
                    if (properties.formingHypothesis && properties.initialInvestigation?.status === InitialInvestigationStatus.Complete) {
                        const formingHypothesis = properties.formingHypothesis;
                        const formingHypothesisNode: InvestigationTreeNode = {
                            id: `${task.id}-forming-hypothesis`,
                            title: 'Forming Hypothesis',
                            description: `Generated ${formingHypothesis.hypotheses?.length || 0} hypotheses`,
                            status: formingHypothesis.status === FormingHypothesisStatus.Complete ? 'completed' : 'in_progress',
                            children: [],
                            expanded: getExistingExpandedState(`${task.id}-forming-hypothesis`, prevState),
                            isValidating: false,
                            isLoading: formingHypothesis.status === FormingHypothesisStatus.InProgress,
                            parentId: undefined,
                            nodeType: 'phase',
                        };

                        // Add hypotheses as children
                        if (formingHypothesis.hypotheses) {
                            formingHypothesisNode.children = formingHypothesis.hypotheses.map((hypothesis: HypothesisTreeItem) => {
                                const hypothesisNode: InvestigationTreeNode = {
                                    id: hypothesis.id,
                                    title: hypothesis.title,
                                    description: hypothesis.description,
                                    status: hypothesis.status.toLowerCase(),
                                    children: [],
                                    expanded: getExistingExpandedState(hypothesis.id, prevState),
                                    isValidating: hypothesis.status === HypothesisStatus.Validating,
                                    isLoading: false,
                                    parentId: formingHypothesisNode.id,
                                    nodeType: 'hypothesis',
                                    // Include detailed validation steps
                                    steps: hypothesis.steps || [],
                                };
                                newNodes.set(hypothesisNode.id, hypothesisNode);
                                return hypothesisNode;
                            });
                        }

                        // Recursively add hypothesis children to the tree
                        const addHypothesisChildren = (parentNode: InvestigationTreeNode, hypothesis: HypothesisTreeItem) => {
                            if (hypothesis.children && hypothesis.children.length > 0) {
                                parentNode.children = hypothesis.children.map((child: HypothesisTreeItem) => {
                                    const childNode: InvestigationTreeNode = {
                                        id: child.id,
                                        title: child.title,
                                        description: child.description,
                                        status: child.status.toLowerCase(),
                                        children: [],
                                        expanded: getExistingExpandedState(child.id, prevState),
                                        isValidating: child.status === HypothesisStatus.Validating,
                                        isLoading: false,
                                        parentId: parentNode.id,
                                        nodeType: 'hypothesis',
                                        // Include detailed validation steps
                                        steps: child.steps || [],
                                    };
                                    newNodes.set(childNode.id, childNode);
                                    // Recursively add further children
                                    addHypothesisChildren(childNode, child);
                                    return childNode;
                                });
                            }
                        };

                        // After creating the children for the formingHypothesisNode, recursively add their children
                        if (formingHypothesisNode.children && formingHypothesisNode.children.length > 0) {
                            formingHypothesisNode.children.forEach((childNode, idx) => {
                                const hypothesis = formingHypothesis.hypotheses[idx];
                                if (hypothesis) {
                                    addHypothesisChildren(childNode, hypothesis);
                                }
                            });
                        }

                        newNodes.set(formingHypothesisNode.id, formingHypothesisNode);
                        newRootNodes.push(formingHypothesisNode);
                    }

                    // Handle Conclusion
                    if (properties.conclusion && properties.formingHypothesis?.status === FormingHypothesisStatus.Complete) {
                        const conclusion = properties.conclusion;
                        const conclusionNode: InvestigationTreeNode = {
                            id: `${task.id}-conclusion`,
                            title: conclusion.title || 'Conclusion',
                            description: conclusion.summary || 'Conclusion in progress...',
                            status: task.status === 'Complete' ? 'completed' : 'in_progress',
                            children: [],
                            expanded: getExistingExpandedState(`${task.id}-conclusion`, prevState),
                            isValidating: false,
                            isLoading: task.status === 'InProgress',
                            parentId: undefined,
                            nodeType: 'phase',
                        };
                        newNodes.set(conclusionNode.id, conclusionNode);
                        newRootNodes.push(conclusionNode);
                    }
                } else {
                    // Fallback: create a basic tree node for any task type to ensure something shows
                    console.log('📋 Creating basic tree node for task type:', task.type);
                    const basicTaskNode: InvestigationTreeNode = {
                        id: `${task.id}-basic`,
                        title: task.title || `Task ${task.id}`,
                        description: `Task of type ${task.type} - ${task.status || 'Unknown status'}`,
                        status: task.status?.toLowerCase() || 'unknown',
                        children: [],
                        expanded: true,
                        isValidating: false,
                        isLoading: task.status === 'InProgress',
                        parentId: undefined,
                        nodeType: 'phase',
                    };
                    newNodes.set(basicTaskNode.id, basicTaskNode);
                    newRootNodes.push(basicTaskNode);
                }

                // Update the state with the new nodes and root nodes
                newState.nodes = newNodes;
                newState.rootNodes = newRootNodes;

                console.log('Tree updated from task:', { taskId: task.id, status: task.status, nodes: newRootNodes.length });

                return newState;
            });
        } catch (error) {
            console.error('Error in updateFromTaskUpdate:', error, { task });
            return;
        }
    }, []);

    // Force refresh investigation tree from API
    const refreshFromAPI = useCallback(async () => {
        if (!threadId || !showAgentTaskDev) {
            return;
        }

        console.log('🔧 refreshFromAPI called for thread:', threadId);
        try {
            // Get the thread first to access agentTasks
            const threadResponse = await threadClient.getThread(threadId);
            if (!threadResponse.isSuccessful || !threadResponse.content) {
                console.log('🔧 refreshFromAPI: Failed to get thread');
                return;
            }

            const thread = threadResponse.content;
            const agentTasks = (thread as any)?.agentTasks || (thread as any)?.AgentTasks || [];
            if (agentTasks.length > 0) {
                const taskId = agentTasks[agentTasks.length - 1]; // Get the last task ID
                const response = await agentTaskDevClient.getAgentTask(threadId, taskId);

                console.log('🔧 refreshFromAPI response:', response);
                if (response.isSuccessful && response.content) {
                    console.log('✅ Got fresh task data, updating tree from API');
                    updateFromTaskUpdate(response.content);
                } else {
                    console.log('🔧 refreshFromAPI: No successful response from API');
                }
            } else {
                console.log('🔧 refreshFromAPI: No agent tasks in thread');
            }
        } catch (error) {
            console.error('🔧 Failed to refresh from API:', error);
        }
    }, [threadId, showAgentTaskDev, agentTaskDevClient, threadClient, updateFromTaskUpdate]);

    // Subscribe to streaming task updates
    useEffect(() => {
        if (!streamingContext?.subscribeTaskUpdateEvent) {
            return;
        }

        const unsubscribe = streamingContext.subscribeTaskUpdateEvent((message: StreamingMessage) => {
            console.log('🔔 Received streaming message:', message);
            try {
                // Check if this message belongs to the current thread
                const messageThreadId = message.additionalProperties?.threadId;

                if (!threadId || !messageThreadId || messageThreadId !== threadId) {
                    console.log('🚫 Message for different thread:', { messageThreadId, currentThreadId: threadId });
                    return;
                }
                // Check if this is a TaskUpdate or TaskProgress message
                const messageType = message.additionalProperties?.streamMessageType;
                console.log('📨 Processing message type:', messageType);
                if (messageType?.toLowerCase() === 'taskupdate') {
                    const text = message.contents?.[0]?.text;
                    console.log('🔍 Raw text received:', !!text, text?.length);
                    if (text) {
                        console.log('📝 TaskUpdate data:', text);
                        try {
                            const taskData = JSON.parse(text) as AgentTask;
                            console.log('🔍 Parsed taskData successfully:', taskData);

                            // Check if this might be incomplete streaming data
                            const hasInitialInvestigation = taskData.properties?.initialInvestigation;
                            const hypotheses = taskData.properties?.formingHypothesis?.hypotheses;
                            const hasHypotheses = hypotheses && hypotheses.length > 0;
                            const hasConclusion = taskData.properties?.conclusion;

                            console.log('🔍 TaskUpdate completeness check:', {
                                hasInitialInvestigation,
                                hasHypotheses,
                                hasConclusion,
                                hypothesesCount: hypotheses?.length || 0,
                            });

                            // If we previously had more data and this looks like just initial investigation,
                            // it might be incomplete streaming data - refresh from API
                            if (hasInitialInvestigation && !hasHypotheses && !hasConclusion) {
                                console.log('🔧 Detected potentially incomplete TaskUpdate, triggering API refresh');
                                setTimeout(() => refreshFromAPI(), 500); // Small delay to let API catch up
                            }

                            // Call updateFromTaskUpdate with the parsed task data
                            updateFromTaskUpdate(taskData);
                        } catch (parseError) {
                            console.error('❌ Error parsing TaskUpdate JSON:', parseError);
                        }
                    }
                } else if (messageType?.toLowerCase() === 'taskprogress') {
                    const text = message.contents?.[0]?.text;
                    if (text) {
                        console.log('📈 TaskProgress data:', text);
                        const progressData = JSON.parse(text) as TaskProgressUpdate;

                        // TaskProgress often triggers incomplete updates, so refresh from API after processing
                        console.log('🔧 TaskProgress received, scheduling API refresh');
                        setTimeout(() => refreshFromAPI(), 1000); // Longer delay for progress updates

                        // Call updateFromTaskProgress with the parsed progress data
                        updateFromTaskProgress(progressData);
                    }
                }
            } catch (error) {
                // Error parsing TaskUpdate streaming message
            }
        });

        return () => {
            unsubscribe();
        };
    }, [streamingContext, updateFromTaskUpdate, updateFromTaskProgress, threadId, refreshFromAPI]);

    const toggleNodeExpanded = useCallback((nodeId: string) => {
        setTreeState(prevState => {
            const newNodes = new Map(prevState.nodes);
            const node = newNodes.get(nodeId);

            if (node) {
                const updatedNode = {
                    ...node,
                    expanded: !node.expanded,
                };

                newNodes.set(nodeId, updatedNode);

                // Update in parent's children array
                const updatedRootNodes = updateNodeInParent(updatedNode, newNodes, prevState.rootNodes);

                return {
                    ...prevState,
                    nodes: newNodes,
                    rootNodes: updatedRootNodes,
                };
            } else {
                // Node not found for toggle
            }

            return prevState;
        });
    }, []);

    const clearTree = useCallback(() => {
        phaseProgressRef.current.clear(); // Clear phase progress tracker
        hypothesisProgressRef.current.clear(); // Clear hypothesis progress tracker
        setTreeState(prevState => ({
            ...prevState,
            nodes: new Map(),
            rootNodes: [],
            currentTask: undefined,
        }));
    }, []);

    const showTree = useCallback(() => {
        setTreeState(prevState => ({
            ...prevState,
            isVisible: true,
        }));
    }, []);

    const hideTree = useCallback(() => {
        setTreeState(prevState => ({
            ...prevState,
            isVisible: false,
        }));
    }, []);

    const resetTree = useCallback(() => {
        phaseProgressRef.current.clear(); // Clear phase progress tracker
        hypothesisProgressRef.current.clear(); // Clear hypothesis progress tracker
        setTreeState({
            nodes: new Map(),
            rootNodes: [],
            currentTask: undefined,
            isVisible: false,
            isLoading: false,
        });
    }, []);

    const forceRefresh = useCallback(() => {
        phaseProgressRef.current.clear();
        hypothesisProgressRef.current.clear();
        // Also refresh from API to get latest data
        refreshFromAPI();
    }, [refreshFromAPI]);

    const loadSavedAgentTasks = useCallback(async (threadId: string) => {
        if (!threadId) {
            return;
        }

        // Loading saved agent tasks for thread

        try {
            setTreeState(prevState => ({
                ...prevState,
                isLoading: true,
            }));

            // TODO: In the future, we would call an API to get all agent tasks for this thread
            // For now, we'll fetch individual agent task if we know its ID
            // const threadClient = new ThreadClient();
            // const response = await threadClient.getAgentTask(threadId, agentTaskId);
            // if (response.data) {
            //     updateFromTaskUpdate(response.data);
            // }

            // Successfully loaded saved agent tasks for thread
        } catch (error) {
            // Error loading saved agent tasks
        } finally {
            setTreeState(prevState => ({
                ...prevState,
                isLoading: false,
            }));
        }
    }, []);

    const contextValue = {
        treeState,
        updateFromTaskProgress,
        updateFromTaskUpdate,
        loadSavedAgentTasks,
        toggleNodeExpanded,
        clearTree,
        showTree,
        hideTree,
        resetTree,
        forceRefresh,
    };

    // Safety check for children prop - moved after hooks to avoid conditional hook calls
    if (!children) {
        // InvestigationTreeProvider: children prop is undefined or null
        return null;
    }

    return (
        <InvestigationTreeErrorBoundary>
            <InvestigationTreeContext.Provider value={contextValue}>{children || null}</InvestigationTreeContext.Provider>
        </InvestigationTreeErrorBoundary>
    );
};

// Helper function to update a node in its parent's children array
const updateNodeInParent = (
    updatedNode: InvestigationTreeNode,
    nodes: Map<string, InvestigationTreeNode>,
    rootNodes: InvestigationTreeNode[]
): InvestigationTreeNode[] => {
    if (!updatedNode || !nodes || !rootNodes) {
        // Invalid parameters passed to updateNodeInParent
        return rootNodes || [];
    }

    // Recursively update the node and all its ancestors
    const updateNodeRecursively = (node: InvestigationTreeNode): InvestigationTreeNode => {
        if (node.id === updatedNode.id) {
            return updatedNode;
        }

        if (node.children && node.children.length > 0) {
            const updatedChildren = node.children.map(child => updateNodeRecursively(child));
            return {
                ...node,
                children: updatedChildren,
            };
        }

        return node;
    };

    // Update all root nodes recursively
    const updatedRootNodes = rootNodes.map(rootNode => updateNodeRecursively(rootNode));

    // Also update the nodes map to keep it in sync
    updatedRootNodes.forEach(rootNode => {
        const updateNodeInMap = (node: InvestigationTreeNode) => {
            nodes.set(node.id, node);
            node.children.forEach(child => updateNodeInMap(child));
        };
        updateNodeInMap(rootNode);
    });

    return updatedRootNodes;
};
