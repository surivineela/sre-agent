import cloneDeep from 'lodash/cloneDeep';
import { useCallback } from 'react';
import {
    AgentTask,
    AgentTaskStatus,
    AgentTaskType,
    HypothesisStatus,
    HypothesisTreeItem,
    InvestigationStatusCommon,
    InvestigationTreeNode,
    InvestigationTreeState,
    TaskProgressStatus,
    TreeNodeType,
} from '../../Common/Contracts/DataPlane/AgentTask';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { AgentTaskPhaseNodeIdSuffix } from '../Contracts/Activities';

export const useAgentTaskStreamHandler = () => {
    const isStreamOutdated = (currentTimestamp?: string | null, updateTimestamp?: string | null): boolean => {
        if (!currentTimestamp || !updateTimestamp) return false;

        return getSafeDateTime(currentTimestamp).getTime() > getSafeDateTime(updateTimestamp).getTime();
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

    const updateTreeState = useCallback((task: AgentTask | null, currentTreeState: InvestigationTreeState | null) => {
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
                        nodeType: TreeNodeType.InitialInvestigation,
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
                        nodeType: TreeNodeType.InitialInvestigation,
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
                        nodeType: TreeNodeType.Conclusion,
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
                nodeType: TreeNodeType.InitialInvestigation,
            };
            newNodes.set(basicTaskNode.id, basicTaskNode);
            newRootNodeIds.push(basicTaskNode.id);
        }

        updatedTreeState.nodes = newNodes;
        updatedTreeState.rootNodeIds = newRootNodeIds;

        return cloneDeep(updatedTreeState);
    }, []);

    return {
        updateTreeState,
    };
};
