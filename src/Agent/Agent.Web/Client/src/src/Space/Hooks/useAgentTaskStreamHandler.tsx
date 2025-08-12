import cloneDeep from 'lodash/cloneDeep';
import { useCallback } from 'react';
import {
    AgentTask,
    AgentTaskStatus,
    AgentTaskType,
    HypothesisAction,
    HypothesisStatus,
    HypothesisTreeItem,
    InvestigationStatusCommon,
    InvestigationTreeNode,
    InvestigationTreeState,
    TaskProgressPhase,
    TaskProgressStatus,
    TaskProgressUpdate,
    TreeNodeType,
} from '../../Common/Contracts/DataPlane/AgentTask';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { AgentTaskPhaseNodeIdSuffix } from '../Contracts/Activities';

export const useAgentTaskStreamHandler = () => {
    const getTaskIdFromTaskProgressUpdate = (update: TaskProgressUpdate): string => {
        return update.taskId || update.TaskId || 'unknown';
    };

    const getStatusFromTaskProgressUpdate = (update: TaskProgressUpdate): TaskProgressStatus => {
        return update.status || update.Status || 'unknown';
    };

    const getPhaseFromTaskProgressUpdate = (update: TaskProgressUpdate): TaskProgressPhase => {
        return update.phase || update.Phase || 'unknown';
    };

    const getMessageFromTaskProgressUpdate = (update: TaskProgressUpdate): string => {
        return update.message || update.Message || 'No message';
    };

    const getHypothesisTreeItemDescription = (hypothesis: HypothesisTreeItem): string => {
        return hypothesis.Description || hypothesis.description;
    };

    const getPhaseId = (taskProgressUpdate: TaskProgressUpdate) => {
        const taskId = getTaskIdFromTaskProgressUpdate(taskProgressUpdate);
        const phase = getStatusFromTaskProgressUpdate(taskProgressUpdate);

        return `${taskId}-${phase}`;
    };

    const getHypothesisNodeId = (hypothesis: HypothesisTreeItem): string => {
        const hypothesisNodeId = hypothesis.id || hypothesis.Id || `hypothesis-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;

        return hypothesisNodeId;
    };

    const getTaskProgressStatusLevel = (status: TaskProgressStatus): number => {
        switch (status.toLowerCase()) {
            case TaskProgressStatus.Started:
                return 1;
            case TaskProgressStatus.InProgress:
                return 2;
            case TaskProgressStatus.Completed:
                return 3;
            case TaskProgressStatus.Failed:
                return 3; // Treat failed as final like completed
            default:
                return 0;
        }
    };

    // Hypothesis status progression order: pending -> validating -> validated/invalidated/inconclusive
    const getHypothesisStatusLevel = (status: HypothesisStatus | string): number => {
        switch (status) {
            case HypothesisStatus.Pending:
                return 1;
            case HypothesisStatus.Validating:
                return 2;
            case HypothesisStatus.Validated:
                return 3;
            case HypothesisStatus.Invalidated:
                return 3;
            case HypothesisStatus.Inconclusive:
                return 3;
            default:
                return 0;
        }
    };

    const processHypothesisTreeItemStatus = (hypothesis: HypothesisTreeItem): HypothesisStatus | string => {
        const hypothesisStatus = hypothesis.status || hypothesis.Status;

        // Convert numeric status to string if needed
        if (typeof hypothesisStatus === 'number') {
            switch (hypothesisStatus) {
                case 0:
                    return HypothesisStatus.Pending;
                case 1:
                    return HypothesisStatus.Validated;
                case 2:
                    return HypothesisStatus.Invalidated;
                case 3:
                    return HypothesisStatus.Inconclusive;
                default:
                    return HypothesisStatus.Pending;
            }
        } else if (typeof hypothesisStatus === 'string') {
            return hypothesisStatus.toLowerCase();
        }

        return '';
    };

    const isStreamOutdated = (currentTimestamp?: string | null, updateTimestamp?: string | null): boolean => {
        if (!currentTimestamp || !updateTimestamp) return false;

        return getSafeDateTime(currentTimestamp).getTime() > getSafeDateTime(updateTimestamp).getTime();
    };

    const isPhaseStatusProgressionAllowed = (currentTreeState: InvestigationTreeState | null, update: TaskProgressUpdate) => {
        const currentStatus = currentTreeState?.phaseNodesStatus.get(getPhaseId(update));

        if (!currentStatus) {
            return true;
        }

        const currentLevel = getTaskProgressStatusLevel(currentStatus);
        const updateLevel = getTaskProgressStatusLevel(getStatusFromTaskProgressUpdate(update));
        const timestamp = update.lastModified || update.LastModified;

        return currentLevel <= updateLevel && !isStreamOutdated(currentTreeState?.lastModified, timestamp);
    };

    const isHypothesisStatusProgressionAllowed = (
        currentTreeState: InvestigationTreeState | null,
        id: string,
        status: HypothesisStatus | string
    ) => {
        const currentStatus = currentTreeState?.hypothesisNodesStatus.get(id);

        if (!currentStatus) {
            // First time seeing this hypothesis, allow any status
            return true;
        }

        const currentLevel = getHypothesisStatusLevel(currentStatus);
        const newLevel = getHypothesisStatusLevel(status);

        return currentLevel <= newLevel;
    };

    const getPhaseNodeByTitle = (nodeMap: Map<string, InvestigationTreeNode>, rootNodeIds: string[], title: string) => {
        for (const nodeId of rootNodeIds) {
            const node = nodeMap.get(nodeId);
            if (node && node.nodeType === TreeNodeType.Phase && node.title.toLowerCase().includes(title.toLowerCase())) {
                return node;
            }
        }
        return null;
    };

    const getHypothesisNodeByDescription = (nodeMap: Map<string, InvestigationTreeNode>, description: string) => {
        return (
            Array.from(nodeMap.values()).find(node => node.nodeType === TreeNodeType.Hypothesis && node.description === description) || null
        );
    };

    const markAllRootNodesAsCompleted = (newNodes: Map<string, InvestigationTreeNode>, rootNodeIds: string[]) => {
        rootNodeIds.forEach(nodeId => {
            const node = newNodes.get(nodeId);
            if (node && node.nodeType === TreeNodeType.Phase) {
                const updatedNode = {
                    ...node,
                    status: TaskProgressStatus.Completed,
                    isValidating: false,
                    isLoading: false,
                };
                newNodes.set(node.id, updatedNode);
            }
        });
    };

    // Helper function to get existing expanded state for a node
    const getExistingExpandedState = (nodeId: string, currentTreeState: InvestigationTreeState | null): boolean => {
        const existingNode = currentTreeState?.nodes.get(nodeId);
        return existingNode?.expanded ?? true; // Default to true if no existing node
    };

    const getDefaultTreeState = () => {
        const defaultTreeState: InvestigationTreeState = {
            nodes: new Map(),
            rootNodeIds: [],
            phaseNodesStatus: new Map(),
            hypothesisNodesStatus: new Map(),
            isVisible: false,
            isLoading: false,
        };

        return defaultTreeState;
    };

    const getHypothesisNodeIdAndObjectFromNodeMap = (hypothesis: HypothesisTreeItem, nodeMap: Map<string, InvestigationTreeNode>) => {
        const hypothesisNodeId = getHypothesisNodeId(hypothesis);

        return {
            hypothesisNodeId,
            existingHypothesis: nodeMap.get(hypothesisNodeId),
        };
    };

    const getHypothesisNodeAndId = (
        hypothesis: HypothesisTreeItem,
        hypothesisAction: HypothesisAction,
        nodeMap: Map<string, InvestigationTreeNode>
    ) => {
        // Check if hypothesis already exists to prevent duplicates
        const { hypothesisNodeId, existingHypothesis } = getHypothesisNodeIdAndObjectFromNodeMap(hypothesis, nodeMap);

        if (existingHypothesis) {
            return {
                existingHypothesis,
                hypothesisNodeId,
            };
        } else if (equals(hypothesisAction, HypothesisAction.Update) || equals(hypothesisAction, HypothesisAction.Validate)) {
            // If not found by ID, try to find by title
            const nodeWithSameTitle = Array.from(nodeMap.values()).filter(
                node => node.nodeType === TreeNodeType.Hypothesis && node.title === (hypothesis.title || hypothesis.Title)
            );

            if (nodeWithSameTitle.length > 0) {
                // If found by title, check if we can find by description as well. If so, return the match, otherwise return the first node with the same title
                const nodeWithSameDescription = nodeWithSameTitle.find(
                    node => node.description === getHypothesisTreeItemDescription(hypothesis)
                );
                if (nodeWithSameDescription) {
                    return {
                        existingHypothesis: nodeWithSameDescription,
                        hypothesisNodeId: nodeWithSameDescription.id,
                    };
                } else {
                    return {
                        existingHypothesis: nodeWithSameTitle[0],
                        hypothesisNodeId: nodeWithSameTitle[0].id,
                    };
                }
            }
        }

        return {
            existingHypothesis: null,
            hypothesisNodeId,
        };
    };

    const createNewHypothesisNode = (
        hypothesis: HypothesisTreeItem,
        nodeId: string,
        currentTreeState: InvestigationTreeState | null,
        setSteps: boolean
    ) => {
        // For new hypotheses, always start with 'pending' status
        // The backend sets them as 'Inconclusive' but we want to show 'pending' initially
        const newHypothesisNode: InvestigationTreeNode = {
            id: nodeId,
            title: hypothesis.Title || hypothesis.title,
            description: getHypothesisTreeItemDescription(hypothesis),
            status: HypothesisStatus.Pending,
            parentHypothesisDescription: hypothesis.parentHypothesisDescription || hypothesis.ParentHypothesisDescription || undefined,
            childrenIds: [],
            expanded: getExistingExpandedState(nodeId, currentTreeState), // Preserve existing expanded state
            isValidating: false,
            isLoading: false,
            parentId: undefined,
            nodeType: TreeNodeType.Hypothesis,
            // Include detailed validation steps
            steps: setSteps ? hypothesis.steps || [] : undefined,
        };

        return newHypothesisNode;
    };

    const updateExistingHypothesisNode = (
        nodeMap: Map<string, InvestigationTreeNode>,
        hypothesis: HypothesisTreeItem,
        existingHypothesis: InvestigationTreeNode,
        statusString: HypothesisStatus | string
    ) => {
        const updatedHypothesis = {
            ...existingHypothesis,
            title: hypothesis.Title || hypothesis.title,
            description: getHypothesisTreeItemDescription(hypothesis),
            status: statusString,
            isValidating: false,
            isLoading: false,
            // Update steps if available
            steps: hypothesis.steps || existingHypothesis.steps,
        };

        nodeMap.set(existingHypothesis.id, updatedHypothesis);
    };

    const linkHypothesisToParent = (
        hypothesisNode: InvestigationTreeNode,
        parentHypothesis: InvestigationTreeNode,
        nodeMap: Map<string, InvestigationTreeNode>
    ) => {
        hypothesisNode.parentId = parentHypothesis.id;
        parentHypothesis.childrenIds = [...parentHypothesis.childrenIds, hypothesisNode.id];
        nodeMap.set(parentHypothesis.id, parentHypothesis);
    };

    const updateHypothesisNodeAndParentNodeInTreeState = (
        hypothesisNode: InvestigationTreeNode,
        nodeMap: Map<string, InvestigationTreeNode>,
        rootNodeIds: string[],
        ignoreParentHypothesisNode: boolean
    ) => {
        // Check if this is a child hypothesis or initial hypothesis
        const parentDesc = hypothesisNode.parentHypothesisDescription;
        const parentHypothesisNode = parentDesc ? getHypothesisNodeByDescription(nodeMap, parentDesc) : null;

        if (parentHypothesisNode && !ignoreParentHypothesisNode) {
            linkHypothesisToParent(hypothesisNode, parentHypothesisNode, nodeMap);
        } else {
            // This is an initial hypothesis - add directly to the Initial Investigation phase
            const initialInvestigationPhase = getPhaseNodeByTitle(nodeMap, rootNodeIds, 'initial investigation');
            if (initialInvestigationPhase) {
                linkHypothesisToParent(hypothesisNode, initialInvestigationPhase, nodeMap);
            } else {
                rootNodeIds.push(hypothesisNode.id); // If no phase found, add as root node
            }
        }
    };

    const createAndProcessNewHypothesisNode = (
        hypothesis: HypothesisTreeItem,
        hypothesisNodeId: string,
        currentTreeState: InvestigationTreeState | null,
        nodeMap: Map<string, InvestigationTreeNode>,
        rootNodeIds: string[],
        ignoreParentHypothesisNode: boolean,
        setSteps: boolean
    ) => {
        const newHypothesisNode = createNewHypothesisNode(hypothesis, hypothesisNodeId, currentTreeState, setSteps);
        nodeMap.set(hypothesisNodeId, newHypothesisNode);
        updateHypothesisNodeAndParentNodeInTreeState(newHypothesisNode, nodeMap, rootNodeIds, ignoreParentHypothesisNode);
    };

    const handleInvestigationSummary = (update: TaskProgressUpdate, nodeMap: Map<string, InvestigationTreeNode>, rootNodeIds: string[]) => {
        const summary = update.summary || update.Summary || '';
        if (summary) {
            const initialInvestigationPhase = getPhaseNodeByTitle(nodeMap, rootNodeIds, 'initial investigation');
            if (initialInvestigationPhase) {
                const updatedPhaseNode = {
                    ...initialInvestigationPhase,
                    description: summary,
                    status: 'completed',
                    isLoading: false,
                };

                nodeMap.set(initialInvestigationPhase.id, updatedPhaseNode);
            }
        }
    };

    const handleConclusion = (
        update: TaskProgressUpdate,
        nodeMap: Map<string, InvestigationTreeNode>,
        rootNodeIds: string[],
        currentTreeState: InvestigationTreeState | null
    ) => {
        const taskId = getTaskIdFromTaskProgressUpdate(update);
        const status = getStatusFromTaskProgressUpdate(update);
        const conclusion = update.conclusion || update.Conclusion;
        // Use a stable ID for the conclusion node
        const conclusionNodeId = `${taskId}-conclusion`;
        // Find existing conclusion phase node by ID
        const conclusionPhase = nodeMap.get(conclusionNodeId);

        const conclusionTitle = conclusion?.title || conclusion?.title || 'Conclusion';
        const conclusionSummary = conclusion?.summary || conclusion?.Summary || '';

        // Get top-level summary
        const topLevelSummary = update.summary || update.Summary || '';
        // Combine summaries
        let combinedSummary = '';
        if (equals(status, TaskProgressStatus.Completed, AntUxStringComparison.IgnoreCase) && topLevelSummary) {
            combinedSummary = topLevelSummary;
        } else if (conclusionSummary) {
            combinedSummary = conclusionSummary;
        } else if (topLevelSummary) {
            combinedSummary = topLevelSummary;
        }

        const isLoading =
            equals(status, TaskProgressStatus.InProgress, AntUxStringComparison.IgnoreCase) ||
            equals(status, TaskProgressStatus.Started, AntUxStringComparison.IgnoreCase);

        if (conclusionPhase) {
            // Update existing conclusion node
            const updatedPhaseNode = {
                ...conclusionPhase,
                title: conclusionTitle,
                description: combinedSummary,
                status: status,
                isLoading,
            };
            nodeMap.set(conclusionNodeId, updatedPhaseNode);
        } else {
            // Create new conclusion phase node only if it doesn't exist
            const newConclusionNode: InvestigationTreeNode = {
                id: conclusionNodeId,
                title: conclusionTitle,
                description: combinedSummary,
                status: status,
                childrenIds: [],
                expanded: getExistingExpandedState(conclusionNodeId, currentTreeState),
                isValidating: false,
                isLoading,
                parentId: undefined,
                nodeType: TreeNodeType.Phase,
            };
            nodeMap.set(conclusionNodeId, newConclusionNode);
            rootNodeIds.push(newConclusionNode.id);
        }
    };

    const updatePhaseNode = (
        currentTreeState: InvestigationTreeState | null,
        nodeMap: Map<string, InvestigationTreeNode>,
        rootNodeIds: string[],
        phaseNodesStatus: Map<string, TaskProgressStatus>,
        update: TaskProgressUpdate
    ) => {
        const phaseNodeId = getPhaseId(update);
        const phase = getPhaseFromTaskProgressUpdate(update);
        const status = getStatusFromTaskProgressUpdate(update);
        const message = update.message || update.Message || 'No message';

        phaseNodesStatus.set(phaseNodeId, status);

        // Check if we already have a node for this main phase type
        const existingPhaseNode = getPhaseNodeByTitle(nodeMap, rootNodeIds, phase.replace(/_/g, ' '));

        // Skip conclusion and validating_hypothesis phases - they're handled separately
        if (
            !equals(phase, TaskProgressPhase.Conclusion, AntUxStringComparison.IgnoreCase) &&
            !equals(phase, TaskProgressPhase.ValidatingHypothesis, AntUxStringComparison.IgnoreCase)
        ) {
            const isLoading =
                equals(status, TaskProgressStatus.InProgress, AntUxStringComparison.IgnoreCase) ||
                equals(status, TaskProgressStatus.Started, AntUxStringComparison.IgnoreCase);
            if (!existingPhaseNode) {
                const phaseNode: InvestigationTreeNode = {
                    id: phaseNodeId,
                    title: phase.replace(/_/g, ' ').replace(/\b\w/g, (l: string) => l.toUpperCase()),
                    description: message,
                    childrenIds: [],
                    expanded: getExistingExpandedState(phaseNodeId, currentTreeState),
                    isValidating: false,
                    isLoading,
                    parentId: undefined,
                    nodeType: TreeNodeType.Phase,
                    status,
                };
                nodeMap.set(phaseNodeId, phaseNode);
                rootNodeIds.push(phaseNodeId);
            } else {
                // Update existing phase node
                const updatedPhaseNode = {
                    ...existingPhaseNode,
                    description: message,
                    status,
                    isLoading,
                };
                nodeMap.set(existingPhaseNode.id, updatedPhaseNode);
            }
        }

        if (equals(status, TaskProgressStatus.Completed, AntUxStringComparison.IgnoreCase)) {
            markAllRootNodesAsCompleted(nodeMap, rootNodeIds);
        }
    };

    const updateHypothesisNode = (
        currentTreeState: InvestigationTreeState | null,
        nodeMap: Map<string, InvestigationTreeNode>,
        rootNodeIds: string[],
        hypothesisNodesStatus: Map<string, HypothesisStatus | string>,
        update: TaskProgressUpdate
    ) => {
        const phase = getPhaseFromTaskProgressUpdate(update);
        const status = getStatusFromTaskProgressUpdate(update);
        const message = getMessageFromTaskProgressUpdate(update);
        const hypothesisUpdate = update.hypothesisUpdate || update.HypothesisUpdate;
        const hypothesisAction = update.hypothesisAction || update.HypothesisAction;

        if (hypothesisAction) {
            if (hypothesisUpdate) {
                const { existingHypothesis, hypothesisNodeId } = getHypothesisNodeAndId(hypothesisUpdate, hypothesisAction, nodeMap);
                const statusString = processHypothesisTreeItemStatus(hypothesisUpdate);

                if (equals(hypothesisAction, HypothesisAction.Add, AntUxStringComparison.IgnoreCase)) {
                    if (!existingHypothesis) {
                        createAndProcessNewHypothesisNode(
                            hypothesisUpdate,
                            hypothesisNodeId,
                            currentTreeState,
                            nodeMap,
                            rootNodeIds,
                            false,
                            true
                        );
                    } else {
                        // Only ignore if it would overwrite a validating status with pending/inconclusive
                        const isFinalState =
                            statusString === HypothesisStatus.Validated ||
                            statusString === HypothesisStatus.Invalidated ||
                            statusString === HypothesisStatus.Inconclusive;
                        const ignoreUpdate = existingHypothesis.isValidating && !isFinalState;

                        if (!ignoreUpdate) {
                            updateExistingHypothesisNode(nodeMap, hypothesisUpdate, existingHypothesis, statusString);
                        }
                    }
                } else if (existingHypothesis && equals(hypothesisAction, HypothesisAction.Update, AntUxStringComparison.IgnoreCase)) {
                    if (isHypothesisStatusProgressionAllowed(currentTreeState, hypothesisNodeId, statusString)) {
                        hypothesisNodesStatus.set(hypothesisNodeId, statusString);
                        updateExistingHypothesisNode(nodeMap, hypothesisUpdate, existingHypothesis, statusString);
                    }
                } else if (existingHypothesis && equals(hypothesisAction, HypothesisAction.Validate, AntUxStringComparison.IgnoreCase)) {
                    if (isHypothesisStatusProgressionAllowed(currentTreeState, hypothesisNodeId, HypothesisStatus.Validating)) {
                        hypothesisNodesStatus.set(hypothesisNodeId, HypothesisStatus.Validating);
                        const updatedHypothesis = {
                            ...existingHypothesis,
                            status: HypothesisStatus.Validating,
                            isValidating: true,
                            isLoading: true,
                        };
                        nodeMap.set(hypothesisNodeId, updatedHypothesis);
                    }
                }
            }
        } else if (hypothesisUpdate) {
            // Handle hypothesis updates without explicit action (inferred from phase and message)
            const { existingHypothesis, hypothesisNodeId } = getHypothesisNodeIdAndObjectFromNodeMap(hypothesisUpdate, nodeMap);

            if (
                equals(phase, TaskProgressPhase.FormingHypothesis, AntUxStringComparison.IgnoreCase) &&
                message.toLowerCase().includes('generated') &&
                !existingHypothesis
            ) {
                createAndProcessNewHypothesisNode(hypothesisUpdate, hypothesisNodeId, currentTreeState, nodeMap, rootNodeIds, true, false);
            }
        }

        // Handle investigation summary streaming
        if (
            equals(phase, TaskProgressPhase.InitialInvestigation, AntUxStringComparison.IgnoreCase) &&
            equals(status, TaskProgressStatus.Completed, AntUxStringComparison.IgnoreCase)
        ) {
            handleInvestigationSummary(update, nodeMap, rootNodeIds);
        }

        // Handle conclusion streaming (both in progress and completed)
        if (equals(phase, TaskProgressPhase.Conclusion, AntUxStringComparison.IgnoreCase)) {
            handleConclusion(update, nodeMap, rootNodeIds, currentTreeState);
        }
    };

    const updateTreeStateFromTaskProgress = useCallback((update: TaskProgressUpdate, currentTreeState: InvestigationTreeState | null) => {
        const updatedTreeState = currentTreeState ? { ...currentTreeState } : getDefaultTreeState();
        const newNodes = new Map(updatedTreeState.nodes || new Map());
        const newRootNodeIds = [...(updatedTreeState.rootNodeIds || [])];
        const newPhaseNodesStatus = new Map(updatedTreeState.phaseNodesStatus || []);
        const newHypothesisNodesStatus = new Map(updatedTreeState.hypothesisNodesStatus || []);

        const taskId = getTaskIdFromTaskProgressUpdate(update);
        const lastModified = update.lastModified || update.LastModified;

        // Check if this status progression is allowed (prevent backwards progression), or if this is a test task
        if (!isPhaseStatusProgressionAllowed(currentTreeState, update) || taskId.startsWith('debug-')) {
            return currentTreeState;
        }

        updatePhaseNode(currentTreeState, newNodes, newRootNodeIds, newPhaseNodesStatus, update);
        updateHypothesisNode(currentTreeState, newNodes, newRootNodeIds, newHypothesisNodesStatus, update);

        updatedTreeState.nodes = newNodes;
        updatedTreeState.rootNodeIds = newRootNodeIds;
        updatedTreeState.phaseNodesStatus = newPhaseNodesStatus;
        updatedTreeState.hypothesisNodesStatus = newHypothesisNodesStatus;

        if (lastModified) {
            updatedTreeState.lastModified = lastModified;
        }

        // ToDo: Update this once the agent task opening trigger design is finalized
        if (!updatedTreeState.isVisible && newRootNodeIds.length > 0) {
            updatedTreeState.isVisible = true;
        }

        return cloneDeep(updatedTreeState);
    }, []);

    const updateTreeStateFromTaskUpdate = useCallback((task: AgentTask | null, currentTreeState: InvestigationTreeState | null) => {
        const updatedTreeState = currentTreeState ? { ...currentTreeState } : getDefaultTreeState();

        if (!task) {
            return updatedTreeState;
        }

        const newNodes = new Map(updatedTreeState.nodes || new Map());
        const newRootNodeIds = [...(updatedTreeState.rootNodeIds || [])];

        const { lastModified, type, properties } = task;

        if (isStreamOutdated(currentTreeState?.lastModified, lastModified)) {
            return currentTreeState;
        }

        if (lastModified) {
            updatedTreeState.lastModified = lastModified;
        }

        // ToDo: Update this once the agent task opening trigger design is finalized
        if (!updatedTreeState.isVisible) {
            updatedTreeState.isVisible = true;
        }

        if (equals(type, AgentTaskType.IncidentInvestigation, AntUxStringComparison.IgnoreCase)) {
            if (properties) {
                const { initialInvestigation, formingHypothesis, conclusion } = properties;

                // Handle Initial Investigation
                if (initialInvestigation) {
                    const id = `${task.id}-${AgentTaskPhaseNodeIdSuffix.InitialInvestigation}`;
                    const initialInvestigationNode: InvestigationTreeNode = {
                        id: id,
                        title: 'Initial Investigation',
                        description:
                            initialInvestigation.summary || initialInvestigation.statusMessage || 'Initial investigation in progress...',
                        status: equals(initialInvestigation.status, InvestigationStatusCommon.Complete, AntUxStringComparison.IgnoreCase)
                            ? TaskProgressStatus.Completed
                            : TaskProgressStatus.InProgress,
                        childrenIds: [],
                        expanded: getExistingExpandedState(id, currentTreeState),
                        isValidating: false,
                        isLoading: equals(
                            initialInvestigation.status,
                            InvestigationStatusCommon.InProgress,
                            AntUxStringComparison.IgnoreCase
                        ),
                        parentId: undefined,
                        nodeType: TreeNodeType.Phase,
                        // Include detailed gathering context steps
                        gatheringContextSteps: initialInvestigation.gatheringContext?.steps || [],
                    };
                    newNodes.set(initialInvestigationNode.id, initialInvestigationNode);
                    newRootNodeIds.push(initialInvestigationNode.id);
                }

                // Handle Forming Hypothesis
                const initialInvestigationStatus = initialInvestigation?.status;
                if (
                    formingHypothesis &&
                    initialInvestigationStatus &&
                    equals(initialInvestigationStatus, InvestigationStatusCommon.Complete, AntUxStringComparison.IgnoreCase)
                ) {
                    const id = `${task.id}-${AgentTaskPhaseNodeIdSuffix.FormingHypothesis}`;
                    const formingHypothesisNode: InvestigationTreeNode = {
                        id: id,
                        title: 'Forming Hypothesis',
                        description: `Generated ${formingHypothesis.hypotheses?.length || 0} hypotheses`,
                        status: equals(formingHypothesis.status, InvestigationStatusCommon.Complete, AntUxStringComparison.IgnoreCase)
                            ? TaskProgressStatus.Completed
                            : TaskProgressStatus.InProgress,
                        childrenIds: [],
                        expanded: getExistingExpandedState(id, currentTreeState),
                        isValidating: false,
                        isLoading: equals(formingHypothesis.status, InvestigationStatusCommon.InProgress, AntUxStringComparison.IgnoreCase),
                        parentId: undefined,
                        nodeType: TreeNodeType.Phase,
                    };

                    // Add hypotheses as children
                    if (formingHypothesis.hypotheses) {
                        formingHypothesisNode.childrenIds = formingHypothesis.hypotheses.map((hypothesis: HypothesisTreeItem) => {
                            const hypothesisNode: InvestigationTreeNode = {
                                id: hypothesis.id,
                                title: hypothesis.title,
                                description: hypothesis.description,
                                status: hypothesis.status.toLowerCase(),
                                childrenIds: [],
                                expanded: getExistingExpandedState(hypothesis.id, currentTreeState),
                                isValidating: equals(hypothesis.status, HypothesisStatus.Validating, AntUxStringComparison.IgnoreCase),
                                isLoading: false,
                                parentId: formingHypothesisNode.id,
                                nodeType: TreeNodeType.Hypothesis,
                                // Include detailed validation steps
                                steps: hypothesis.steps || [],
                            };
                            newNodes.set(hypothesisNode.id, hypothesisNode);
                            return hypothesis.id;
                        });
                    }

                    // Recursively add hypothesis children to the tree
                    const addHypothesisChildren = (parentNode: InvestigationTreeNode, hypothesis: HypothesisTreeItem) => {
                        if (hypothesis.children && hypothesis.children.length > 0) {
                            parentNode.childrenIds = hypothesis.children.map((child: HypothesisTreeItem) => {
                                const childNode: InvestigationTreeNode = {
                                    id: child.id,
                                    title: child.title,
                                    description: child.description,
                                    status: child.status.toLowerCase(),
                                    childrenIds: [],
                                    expanded: getExistingExpandedState(child.id, currentTreeState),
                                    isValidating: equals(child.status, HypothesisStatus.Validating, AntUxStringComparison.IgnoreCase),
                                    isLoading: false,
                                    parentId: parentNode.id,
                                    nodeType: TreeNodeType.Hypothesis,
                                    // Include detailed validation steps
                                    steps: child.steps || [],
                                };
                                newNodes.set(childNode.id, childNode);
                                // Recursively add further children
                                addHypothesisChildren(childNode, child);
                                return child.id;
                            });
                        }
                    };

                    // After creating the children for the formingHypothesisNode, recursively add their children
                    if (formingHypothesisNode.childrenIds && formingHypothesisNode.childrenIds.length > 0) {
                        formingHypothesisNode.childrenIds.forEach((childId, idx) => {
                            const hypothesis = formingHypothesis.hypotheses[idx];
                            const childNode = newNodes.get(childId);
                            if (hypothesis && childNode) {
                                addHypothesisChildren(childNode, hypothesis);
                            }
                        });
                    }

                    newNodes.set(formingHypothesisNode.id, formingHypothesisNode);
                    newRootNodeIds.push(formingHypothesisNode.id);
                }

                // Handle Conclusion
                const formingHypothesisStatus = formingHypothesis?.status;
                if (
                    conclusion &&
                    formingHypothesisStatus &&
                    equals(formingHypothesisStatus, InvestigationStatusCommon.Complete, AntUxStringComparison.IgnoreCase)
                ) {
                    const conclusion = properties.conclusion;
                    const id = `${task.id}-${AgentTaskPhaseNodeIdSuffix.Conclusion}`;
                    const conclusionNode: InvestigationTreeNode = {
                        id: id,
                        title: conclusion.title || 'Conclusion',
                        description: conclusion.summary || 'Conclusion in progress...',
                        status: equals(task.status, AgentTaskStatus.Complete, AntUxStringComparison.IgnoreCase)
                            ? TaskProgressStatus.Completed
                            : TaskProgressStatus.InProgress,
                        childrenIds: [],
                        expanded: getExistingExpandedState(id, currentTreeState),
                        isValidating: false,
                        isLoading: equals(task.status, AgentTaskStatus.InProgress, AntUxStringComparison.IgnoreCase),
                        parentId: undefined,
                        nodeType: TreeNodeType.Phase,
                    };
                    newNodes.set(conclusionNode.id, conclusionNode);
                    newRootNodeIds.push(conclusionNode.id);
                }
            }
        } else {
            // Fallback: create a basic tree node for any task type to ensure something shows
            console.log('📋 Creating basic tree node for task type:', task.type);
            const basicTaskNode: InvestigationTreeNode = {
                id: `${task.id}-basic`,
                title: task.title || `Task ${task.id}`,
                description: `Task of type ${task.type} - ${task.status || 'Unknown status'}`,
                status: task.status?.toLowerCase() || 'unknown',
                childrenIds: [],
                expanded: true,
                isValidating: false,
                isLoading: equals(task.status, AgentTaskStatus.InProgress, AntUxStringComparison.IgnoreCase),
                parentId: undefined,
                nodeType: TreeNodeType.Phase,
            };
            newNodes.set(basicTaskNode.id, basicTaskNode);
            newRootNodeIds.push(basicTaskNode.id);
        }

        updatedTreeState.nodes = newNodes;
        updatedTreeState.rootNodeIds = newRootNodeIds;

        return cloneDeep(updatedTreeState);
    }, []);

    return {
        updateTreeStateFromTaskProgress,
        updateTreeStateFromTaskUpdate,
    };
};
