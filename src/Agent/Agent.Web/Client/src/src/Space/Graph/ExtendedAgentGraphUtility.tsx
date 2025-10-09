import { Edge, Node } from '@xyflow/react';
import {
    ExtendedAgent,
    ExtendedAgentGraphEdge,
    ExtendedAgentGraphNode,
    ExtendedAgentNodeType,
    ExtendedAgentRelationType,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    SystemTool,
} from '../Contracts/ExtendedAgentGraph';

export const EXTENDED_AGENT_CARD_TYPE = 'ExtendedAgentCard';
export const TOOL_CARD_TYPE = 'ToolCard';
export const CONNECTOR_CARD_TYPE = 'ConnectorCard';
export const TRIGGER_CARD_TYPE = 'TriggerCard';
export const EXTENDED_AGENT_EDGE_TYPE = 'ExtendedAgentEdge';

export const createAgentNode = (agent: ExtendedAgent): Node<ExtendedAgentGraphNode> => {
    return {
        id: `agent_${agent.name}`,
        type: EXTENDED_AGENT_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: `agent_${agent.name}`,
            name: agent.name,
            type: ExtendedAgentNodeType.Agent,
            agentType: agent.agentType,
            data: agent,
        },
    };
};

export const createToolNode = (tool: ExtendedTool): Node<ExtendedAgentGraphNode> => {
    return {
        id: `tool_${tool.name}`,
        type: TOOL_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: `tool_${tool.name}`,
            name: tool.name,
            type: ExtendedAgentNodeType.Tool,
            toolType: tool.type,
            data: tool,
        },
    };
};

export const createConnectorNode = (connector: ExtendedConnector): Node<ExtendedAgentGraphNode> => {
    return {
        id: `connector_${connector.name}`,
        type: CONNECTOR_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: `connector_${connector.name}`,
            name: connector.name,
            type: ExtendedAgentNodeType.Connector,
            connectorType: connector.type,
            data: connector,
        },
    };
};

export const createTriggerNode = (trigger: ExtendedTrigger): Node<ExtendedAgentGraphNode> => {
    return {
        id: `trigger_${trigger.name}`,
        type: TRIGGER_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: `trigger_${trigger.name}`,
            name: trigger.name,
            type: ExtendedAgentNodeType.Trigger,
            triggerType: trigger.type,
            data: trigger,
        },
    };
};

export const createSystemToolNode = (systemTool: SystemTool): Node<ExtendedAgentGraphNode> => {
    return {
        id: `systemtool_${systemTool.name}`,
        type: TOOL_CARD_TYPE, // We'll reuse the existing TOOL_CARD_TYPE for now
        position: { x: 0, y: 0 },
        data: {
            id: `systemtool_${systemTool.name}`,
            name: systemTool.name,
            type: ExtendedAgentNodeType.SystemTool,
            toolType: systemTool.category,
            data: systemTool,
        },
    };
};

export const createExtendedAgentEdge = (
    sourceId: string,
    targetId: string,
    relationType: ExtendedAgentRelationType,
    label?: string
): Edge<ExtendedAgentGraphEdge> => {
    const edgeId = `${sourceId}-${targetId}`;

    return {
        id: edgeId,
        type: EXTENDED_AGENT_EDGE_TYPE,
        source: sourceId,
        target: targetId,
        label: label,
        data: {
            source: sourceId,
            target: targetId,
            label: label,
            relationType: relationType,
        },
    };
};

export const buildExtendedAgentGraph = (
    agents: ExtendedAgent[],
    tools: ExtendedTool[],
    connectors: ExtendedConnector[],
    triggers: ExtendedTrigger[] = [],
    systemTools: SystemTool[] = []
): { nodes: Node<ExtendedAgentGraphNode>[]; edges: Edge<ExtendedAgentGraphEdge>[] } => {
    const nodes: Node<ExtendedAgentGraphNode>[] = [];
    const edges: Edge<ExtendedAgentGraphEdge>[] = [];

    // Create a map for quick lookup
    const toolMap = new Map<string, ExtendedTool>();
    const connectorMap = new Map<string, ExtendedConnector>();
    const systemToolMap = new Map<string, SystemTool>();
    const agentMap = new Map<string, ExtendedAgent>();

    // Populate maps
    tools.forEach(tool => toolMap.set(tool.name, tool));
    connectors.forEach(connector => connectorMap.set(connector.name, connector));
    systemTools.forEach(systemTool => systemToolMap.set(systemTool.name, systemTool));
    agents.forEach(agent => agentMap.set(agent.name, agent));

    // Create tool nodes, connector nodes, and trigger nodes
    const toolNodesCreated = new Set<string>();
    const connectorNodesCreated = new Set<string>();
    const triggerNodesCreated = new Set<string>();
    const systemToolNodesCreated = new Set<string>();
    const systemToolEdgesCreated = new Set<string>();

    // Create agent nodes and edges
    agents.forEach(agent => {
        const agentNode = createAgentNode(agent);
        nodes.push(agentNode);

        // Create edges for tools used by this agent
        const agentSystemToolNames = new Set<string>();

        // Prefer explicit systemTools property when present
        agent.systemTools?.forEach(systemToolName => {
            if (systemToolName) {
                agentSystemToolNames.add(systemToolName);
            }
        });

        agent.tools?.forEach(toolName => {
            const tool = toolMap.get(toolName);
            if (tool) {
                // Create tool node if not already created
                if (!toolNodesCreated.has(toolName)) {
                    const toolNode = createToolNode(tool);
                    nodes.push(toolNode);
                    toolNodesCreated.add(toolName);
                }

                // Create edge from agent to tool
                const edge = createExtendedAgentEdge(`agent_${agent.name}`, `tool_${toolName}`, ExtendedAgentRelationType.UsesTool, 'uses');
                edges.push(edge);

                // Create connector node and edge if tool has a connector
                if (tool.connector) {
                    const connector = connectorMap.get(tool.connector);
                    if (connector && !connectorNodesCreated.has(tool.connector)) {
                        const connectorNode = createConnectorNode(connector);
                        nodes.push(connectorNode);
                        connectorNodesCreated.add(tool.connector);
                    }

                    if (connector) {
                        const connectorEdge = createExtendedAgentEdge(
                            `tool_${toolName}`,
                            `connector_${tool.connector}`,
                            ExtendedAgentRelationType.ToolUsesConnector,
                            'connects to'
                        );
                        edges.push(connectorEdge);
                    }
                }

                return;
            }

            const systemTool = systemToolMap.get(toolName);
            if (systemTool) {
                agentSystemToolNames.add(toolName);
            }
        });

        // If explicit systemTools is not populated, fall back to detecting any tools that match a system tool
        if (agent.systemTools == null) {
            agent.tools?.forEach(toolName => {
                if (systemToolMap.has(toolName)) {
                    agentSystemToolNames.add(toolName);
                }
            });
        }

        agentSystemToolNames.forEach(systemToolName => {
            const systemTool = systemToolMap.get(systemToolName);
            if (!systemTool) {
                return;
            }

            if (!systemToolNodesCreated.has(systemToolName)) {
                nodes.push(createSystemToolNode(systemTool));
                systemToolNodesCreated.add(systemToolName);
            }

            const edgeId = getEdgeId(`agent_${agent.name}`, `systemtool_${systemToolName}`);
            if (!systemToolEdgesCreated.has(edgeId)) {
                edges.push(
                    createExtendedAgentEdge(
                        `agent_${agent.name}`,
                        `systemtool_${systemToolName}`,
                        ExtendedAgentRelationType.UsesSystemTool,
                        'uses'
                    )
                );
                systemToolEdgesCreated.add(edgeId);
            }
        });

        // Create edges for agentsAsTools (agent-to-agent relationships)
        agent.agentsAsTools?.forEach(agentAsToolRef => {
            const targetAgent = agentMap.get(agentAsToolRef.agentName);
            if (targetAgent) {
                const edge = createExtendedAgentEdge(
                    `agent_${agent.name}`,
                    `agent_${agentAsToolRef.agentName}`,
                    ExtendedAgentRelationType.AgentAsTool,
                    agentAsToolRef.toolName || 'uses as tool'
                );
                edges.push(edge);
            }
        });

        // Create edges for handoffs
        agent.handoffs?.forEach(handoffAgentName => {
            const targetAgent = agentMap.get(handoffAgentName);
            if (targetAgent) {
                const edge = createExtendedAgentEdge(
                    `agent_${agent.name}`,
                    `agent_${handoffAgentName}`,
                    ExtendedAgentRelationType.HandoffTo,
                    'hands off to'
                );
                edges.push(edge);
            }
        });
    });

    // Create trigger nodes and edges to agents
    triggers.forEach(trigger => {
        if (!triggerNodesCreated.has(trigger.name)) {
            const triggerNode = createTriggerNode(trigger);
            nodes.push(triggerNode);
            triggerNodesCreated.add(trigger.name);
        }

        // Create edge from trigger to agent if agent name is specified
        if (trigger.agentName) {
            const targetAgent = agentMap.get(trigger.agentName);
            if (targetAgent) {
                const edge = createExtendedAgentEdge(
                    `trigger_${trigger.name}`,
                    `agent_${trigger.agentName}`,
                    ExtendedAgentRelationType.TriggerStartsAgent,
                    'starts'
                );
                edges.push(edge);
            }
        }
    });

    // Create connections from overridden meta agent to all extended agents
    const overriddenMetaAgent = agents.find(agent => agent.name === 'meta-agent' && (agent as any).metaAgentOverride === true);
    if (overriddenMetaAgent) {
        agents.forEach(agent => {
            if (agent.name !== 'meta-agent') {
                const edge = createExtendedAgentEdge(
                    `agent_${overriddenMetaAgent.name}`,
                    `agent_${agent.name}`,
                    ExtendedAgentRelationType.MetaAgentConnectsTo,
                    'provides context to'
                );
                edges.push(edge);
            }
        });
    }

    return { nodes, edges };
};

export const getEdgeId = (sourceId: string, targetId: string): string => {
    return `${sourceId}-${targetId}`;
};

export const getRelationshipLabel = (relationType: ExtendedAgentRelationType): string => {
    switch (relationType) {
        case ExtendedAgentRelationType.UsesTool:
            return 'uses';
        case ExtendedAgentRelationType.UsesSystemTool:
            return 'uses system tool';
        case ExtendedAgentRelationType.ToolUsesConnector:
            return 'connects to';
        case ExtendedAgentRelationType.AgentAsTool:
            return 'uses as tool';
        case ExtendedAgentRelationType.HandoffTo:
            return 'hands off to';
        case ExtendedAgentRelationType.TriggerStartsAgent:
            return 'starts';
        case ExtendedAgentRelationType.MetaAgentConnectsTo:
            return 'provides context to';
        default:
            return '';
    }
};

export const filterGraphBySearch = (
    nodes: Node<ExtendedAgentGraphNode>[],
    edges: Edge<ExtendedAgentGraphEdge>[],
    searchQuery: string
): { nodes: Node<ExtendedAgentGraphNode>[]; edges: Edge<ExtendedAgentGraphEdge>[] } => {
    if (!searchQuery.trim()) {
        return { nodes, edges };
    }

    const query = searchQuery.toLowerCase();
    const matchingNodeIds = new Set<string>();

    // Find matching nodes
    nodes.forEach(node => {
        const name = node.data?.name?.toLowerCase() || '';
        const description =
            node.data?.type === ExtendedAgentNodeType.Agent
                ? (node.data.data as ExtendedAgent)?.instructions?.toLowerCase() || ''
                : node.data?.type === ExtendedAgentNodeType.Tool
                  ? (node.data.data as ExtendedTool)?.description?.toLowerCase() || ''
                  : node.data?.type === ExtendedAgentNodeType.SystemTool
                    ? (node.data.data as SystemTool)?.description?.toLowerCase() || ''
                    : node.data?.type === ExtendedAgentNodeType.Trigger
                      ? (node.data.data as ExtendedTrigger)?.description?.toLowerCase() || ''
                      : (node.data.data as ExtendedConnector)?.description?.toLowerCase() || '';

        if (name.includes(query) || description.includes(query)) {
            matchingNodeIds.add(node.id);
        }
    });

    // Include connected nodes
    const connectedNodeIds = new Set(matchingNodeIds);
    edges.forEach(edge => {
        if (matchingNodeIds.has(edge.source) || matchingNodeIds.has(edge.target)) {
            connectedNodeIds.add(edge.source);
            connectedNodeIds.add(edge.target);
        }
    });

    // Filter nodes and edges
    const filteredNodes = nodes.filter(node => connectedNodeIds.has(node.id));
    const filteredEdges = edges.filter(edge => connectedNodeIds.has(edge.source) && connectedNodeIds.has(edge.target));

    return { nodes: filteredNodes, edges: filteredEdges };
};
