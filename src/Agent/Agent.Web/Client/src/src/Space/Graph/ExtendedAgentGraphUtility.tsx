import { Edge, Node } from '@xyflow/react';
import {
    ExtendedAgent,
    ExtendedAgentGraphEdge,
    ExtendedAgentGraphNode,
    ExtendedAgentNodeType,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    Skill,
    SkillGroupData,
    SystemTool,
    ToolboxData,
    ToolboxToolItem,
} from '../Contracts/ExtendedAgentGraph';
import { McpConnection } from './ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { EntityType } from './ExtendedAgentCreationDialog/types';

export const EXTENDED_AGENT_CARD_TYPE = 'ExtendedAgentCard';
export const TOOL_CARD_TYPE = 'ToolCard';
export const CONNECTOR_CARD_TYPE = 'ConnectorCard';
export const TRIGGER_CARD_TYPE = 'TriggerCard';
export const SKILL_CARD_TYPE = 'SkillCard';
export const SKILL_GROUP_CARD_TYPE = 'SkillGroupCard';
export const EXPANDED_SKILL_GROUP_CARD_TYPE = 'ExpandedSkillGroupCard';
export const TOOLBOX_CARD_TYPE = 'ToolboxCard';
export const EXPANDED_TOOLBOX_CARD_TYPE = 'ExpandedToolboxCard';
export const EXTENDED_AGENT_EDGE_TYPE = 'ExtendedAgentEdge';

export const SKILL_GROUP_NODE_ID = 'skill_group';

const EMPTY_DISPLAY = '-' as const;

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

export const createSkillNode = (skill: Skill, isLastInGroup: boolean = false): Node<ExtendedAgentGraphNode> => {
    return {
        id: `skill_${skill.name}`,
        type: SKILL_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: `skill_${skill.name}`,
            name: skill.name,
            type: ExtendedAgentNodeType.Skill,
            isLastInGroup,
            data: skill,
        },
    };
};

export const createSkillGroupNode = (skills: Skill[]): Node<ExtendedAgentGraphNode> => {
    const skillGroupData: SkillGroupData = {
        skillCount: skills.length,
        skills,
    };
    return {
        id: SKILL_GROUP_NODE_ID,
        type: SKILL_GROUP_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: SKILL_GROUP_NODE_ID,
            name: 'Skills',
            type: ExtendedAgentNodeType.SkillGroup,
            data: skillGroupData,
        },
    };
};

export const createExpandedSkillGroupNode = (skills: Skill[]): Node<ExtendedAgentGraphNode> => {
    const skillGroupData: SkillGroupData = {
        skillCount: skills.length,
        skills,
    };
    return {
        id: SKILL_GROUP_NODE_ID,
        type: EXPANDED_SKILL_GROUP_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: SKILL_GROUP_NODE_ID,
            name: 'Skills',
            type: ExtendedAgentNodeType.SkillGroup,
            data: skillGroupData,
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

export const createToolboxNode = (agentName: string, tools: ToolboxToolItem[]): Node<ExtendedAgentGraphNode> => {
    const toolboxData: ToolboxData = {
        agentName,
        toolCount: tools.length,
        tools,
    };
    return {
        id: `toolbox_${agentName}`,
        type: TOOLBOX_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: `toolbox_${agentName}`,
            name: `${agentName} Toolbox`,
            type: ExtendedAgentNodeType.Toolbox,
            data: toolboxData,
        },
    };
};

export const createExpandedToolboxNode = (agentName: string, tools: ToolboxToolItem[]): Node<ExtendedAgentGraphNode> => {
    const toolboxData: ToolboxData = {
        agentName,
        toolCount: tools.length,
        tools,
    };
    return {
        id: `toolbox_${agentName}`,
        type: EXPANDED_TOOLBOX_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: `toolbox_${agentName}`,
            name: `${agentName} Toolbox`,
            type: ExtendedAgentNodeType.Toolbox,
            data: toolboxData,
        },
    };
};

export const createExtendedAgentEdge = (
    sourceId: string,
    targetId: string,
    sourceType: EntityType,
    targetType: EntityType
): Edge<ExtendedAgentGraphEdge> => {
    const edgeId = `${sourceId}-${targetId}`;

    return {
        id: edgeId,
        type: EXTENDED_AGENT_EDGE_TYPE,
        source: sourceId,
        target: targetId,
        data: {
            source: sourceId,
            target: targetId,
            sourceType: sourceType,
            targetType: targetType,
        },
    };
};

export const buildExtendedAgentGraph = (
    agents: ExtendedAgent[],
    tools: ExtendedTool[],
    connectors: ExtendedConnector[],
    triggers: ExtendedTrigger[] = [],
    systemTools: SystemTool[] = [],
    mcpConnections: McpConnection[] = [],
    skills: Skill[] = [],
    isSkillGroupExpanded: boolean = false,
    expandedToolboxes: Set<string> = new Set()
): { nodes: Node<ExtendedAgentGraphNode>[]; edges: Edge<ExtendedAgentGraphEdge>[] } => {
    const nodes: Node<ExtendedAgentGraphNode>[] = [];
    const edges: Edge<ExtendedAgentGraphEdge>[] = [];

    // Create a map for quick lookup
    const toolMap = new Map<string, ExtendedTool>();
    const connectorMap = new Map<string, ExtendedConnector>();
    const systemToolMap = new Map<string, SystemTool>();
    const mcpToolMap = new Map<string, ExtendedTool>();
    const agentMap = new Map<string, ExtendedAgent>();

    // Populate maps
    tools.forEach(tool => toolMap.set(tool.name, tool));
    connectors.forEach(connector => connectorMap.set(connector.name, connector));
    systemTools.forEach(systemTool => systemToolMap.set(systemTool.name, systemTool));
    mcpConnections.forEach(mcpConnection => {
        mcpConnection.tools?.forEach(tool =>
            mcpToolMap.set(tool.name, {
                ...tool,
                connector: mcpConnection.name,
                type: 'mcp',
            })
        );
    });
    agents.forEach(agent => agentMap.set(agent.name, agent));

    // Create trigger nodes tracking
    const triggerNodesCreated = new Set<string>();

    // Create agent nodes and toolbox nodes
    agents.forEach(agent => {
        const agentNode = createAgentNode(agent);
        nodes.push(agentNode);

        // Collect all tools for this agent's toolbox
        const toolboxItems: ToolboxToolItem[] = [];
        const agentSystemToolNames = new Set<string>();

        // Prefer explicit systemTools property when present
        agent.systemTools?.forEach(systemToolName => {
            if (systemToolName) {
                agentSystemToolNames.add(systemToolName);
            }
        });

        // Process regular tools
        agent.tools?.forEach(toolName => {
            const tool = toolMap.get(toolName);
            if (tool) {
                const connector = tool.connector ? connectorMap.get(tool.connector) : undefined;
                toolboxItems.push({
                    tool,
                    connector,
                    isSystemTool: false,
                });
                return;
            }

            const systemTool = systemToolMap.get(toolName);
            if (systemTool) {
                agentSystemToolNames.add(toolName);
            }
        });

        // Process MCP tools
        agent.mcpTools?.forEach(mcpToolName => {
            const mcpTool = mcpToolMap.get(mcpToolName);

            // Always create a tool node even if connector is missing
            const connector = mcpTool?.connector ? connectorMap.get(mcpTool.connector) : undefined;
            toolboxItems.push({
                tool: mcpTool ?? {
                    name: mcpToolName,
                    type: 'mcp',
                    connector: mcpToolName.includes('_') ? mcpToolName.split('_')[0] : EMPTY_DISPLAY, // Assuming connector name is the prefix before first underscore
                },
                connector,
                isSystemTool: false,
            });
        });

        // If explicit systemTools is not populated, fall back to detecting any tools that match a system tool
        if (agent.systemTools == null) {
            agent.tools?.forEach(toolName => {
                if (systemToolMap.has(toolName)) {
                    agentSystemToolNames.add(toolName);
                }
            });
        }

        // Add system tools to toolbox
        agentSystemToolNames.forEach(systemToolName => {
            const systemTool = systemToolMap.get(systemToolName);
            if (systemTool) {
                toolboxItems.push({
                    tool: systemTool,
                    connector: undefined,
                    isSystemTool: true,
                });
            }
        });

        // Create toolbox node if agent has any tools
        if (toolboxItems.length > 0) {
            const isExpanded = expandedToolboxes.has(agent.name);
            const toolboxNode = isExpanded
                ? createExpandedToolboxNode(agent.name, toolboxItems)
                : createToolboxNode(agent.name, toolboxItems);
            nodes.push(toolboxNode);

            // Create edge from agent to toolbox
            const edge = createExtendedAgentEdge(`agent_${agent.name}`, `toolbox_${agent.name}`, 'agent', 'tool');
            edges.push(edge);
        }

        // Create edges for agentsAsTools (agent-to-agent relationships)
        agent.agentsAsTools?.forEach(agentAsToolRef => {
            const targetAgent = agentMap.get(agentAsToolRef.agentName);
            if (targetAgent) {
                const edge = createExtendedAgentEdge(`agent_${agent.name}`, `agent_${agentAsToolRef.agentName}`, 'agent', 'agent');
                edges.push(edge);
            }
        });

        // Create edges for handoffs
        agent.handoffs?.forEach(handoffAgentName => {
            const targetAgent = agentMap.get(handoffAgentName);
            if (targetAgent) {
                const edge = createExtendedAgentEdge(`agent_${agent.name}`, `agent_${handoffAgentName}`, 'agent', 'agent');
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

        // Create edges from trigger to ALL handling agents
        // Use agentNames array if available, otherwise fall back to agentName for backward compatibility
        const agentNames = trigger.agentNames?.length ? trigger.agentNames : trigger.agentName ? [trigger.agentName] : [];
        agentNames.forEach(agentName => {
            const targetAgent = agentMap.get(agentName);
            if (targetAgent) {
                const edge = createExtendedAgentEdge(`trigger_${trigger.name}`, `agent_${agentName}`, 'trigger', 'agent');
                edges.push(edge);
            }
        });
    });

    // Create skill nodes based on expanded state
    const metaAgent = agents.find(agent => agent.name === 'meta_agent');
    if (skills.length > 0) {
        if (isSkillGroupExpanded) {
            // When expanded, show the expanded skill group container
            const expandedGroupNode = createExpandedSkillGroupNode(skills);
            nodes.push(expandedGroupNode);
            // Edge from meta_agent to expanded skill group
            if (metaAgent) {
                const edge = createExtendedAgentEdge(`agent_meta_agent`, SKILL_GROUP_NODE_ID, 'agent', 'skill');
                edges.push(edge);
            }
        } else {
            // When collapsed, show single group node
            const skillGroupNode = createSkillGroupNode(skills);
            nodes.push(skillGroupNode);
            // Edge from meta_agent to skill group
            if (metaAgent) {
                const edge = createExtendedAgentEdge(`agent_meta_agent`, SKILL_GROUP_NODE_ID, 'agent', 'skill');
                edges.push(edge);
            }
        }
    }

    // Create connections from overridden meta agent to all extended agents
    const overriddenMetaAgent = agents.find(agent => agent.name === 'meta-agent' && (agent as any).metaAgentOverride === true);
    if (overriddenMetaAgent) {
        agents.forEach(agent => {
            if (agent.name !== 'meta-agent') {
                const edge = createExtendedAgentEdge(`agent_${overriddenMetaAgent.name}`, `agent_${agent.name}`, 'agent', 'agent');
                edges.push(edge);
            }
        });
    }

    return { nodes, edges };
};

export const getEdgeId = (sourceId: string, targetId: string): string => {
    return `${sourceId}-${targetId}`;
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
        let description = '';

        if (node.data?.type === ExtendedAgentNodeType.Agent) {
            description = (node.data.data as ExtendedAgent)?.instructions?.toLowerCase() || '';
        } else if (node.data?.type === ExtendedAgentNodeType.Tool) {
            description = (node.data.data as ExtendedTool)?.description?.toLowerCase() || '';
        } else if (node.data?.type === ExtendedAgentNodeType.SystemTool) {
            description = (node.data.data as SystemTool)?.description?.toLowerCase() || '';
        } else if (node.data?.type === ExtendedAgentNodeType.Trigger) {
            description = (node.data.data as ExtendedTrigger)?.description?.toLowerCase() || '';
        } else if (node.data?.type === ExtendedAgentNodeType.Toolbox) {
            // Search within toolbox contents (tools and connectors)
            const toolboxData = node.data.data as ToolboxData | undefined;
            const toolMatches = toolboxData?.tools?.some(item => {
                const toolName = item.tool.name.toLowerCase();
                const toolDesc = (item.tool as ExtendedTool).description?.toLowerCase() || '';
                const connectorName = item.connector?.name?.toLowerCase() || '';
                return toolName.includes(query) || toolDesc.includes(query) || connectorName.includes(query);
            });
            if (toolMatches) {
                matchingNodeIds.add(node.id);
                return;
            }
        } else {
            description = (node.data?.data as ExtendedConnector)?.description?.toLowerCase() || '';
        }

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

export const getNodesMatchingSearchQuery = (nodes: Node<ExtendedAgentGraphNode>[], searchQuery: string): Node<ExtendedAgentGraphNode>[] => {
    if (!searchQuery.trim()) {
        return nodes.filter(node => node.data?.type !== ExtendedAgentNodeType.Toolbox);
    }

    const query = searchQuery.toLowerCase();
    const matchingNodes: Node<ExtendedAgentGraphNode>[] = [];

    // Find matching nodes
    nodes.forEach(node => {
        let name = node.data?.name?.toLowerCase();
        let description = '';

        if (node.data?.type === ExtendedAgentNodeType.Agent) {
            description = (node.data.data as ExtendedAgent)?.instructions?.toLowerCase() || '';
        } else if (node.data?.type === ExtendedAgentNodeType.Tool) {
            description = (node.data.data as ExtendedTool)?.description?.toLowerCase() || '';
        } else if (node.data?.type === ExtendedAgentNodeType.SystemTool) {
            description = (node.data.data as SystemTool)?.description?.toLowerCase() || '';
        } else if (node.data?.type === ExtendedAgentNodeType.Trigger) {
            description = (node.data.data as ExtendedTrigger)?.description?.toLowerCase() || '';
        } else if (node.data?.type === ExtendedAgentNodeType.Toolbox) {
            name = ''; // Ignore toolbox name for search
            description = ''; // Ignore toolbox description for search
        } else {
            description = (node.data?.data as ExtendedConnector)?.description?.toLowerCase() || '';
        }

        if (name.includes(query) || description.includes(query)) {
            matchingNodes.push(node);
        }
    });
    return matchingNodes;
};

export const doesNodeExistInGraph = (graphNodes: Node<ExtendedAgentGraphNode>[], prevSelectedNodeId?: string): boolean => {
    if (!prevSelectedNodeId) {
        return false;
    }

    const isVirtualTool = prevSelectedNodeId.startsWith('toolbox_tool_');
    const isVirtualConnector = prevSelectedNodeId.startsWith('toolbox_connector_');
    const isVirtualNode = isVirtualTool || isVirtualConnector;

    if (!isVirtualNode) {
        // The node isn't a virtual node. Check for it directly in the graph.
        return graphNodes.some(node => node.id === prevSelectedNodeId);
    }

    // The node is a virtual tool or connector inside a toolbox. Check within the toolbox nodes in the graph.

    // Extract agent name from the virtual node ID to find the parent toolbox
    const prefix = isVirtualTool ? 'toolbox_tool_' : 'toolbox_connector_';
    const remainder = prevSelectedNodeId.substring(prefix.length);
    const firstUnderscoreIdx = remainder.indexOf('_');
    if (firstUnderscoreIdx !== -1) {
        const agentName = remainder.substring(0, firstUnderscoreIdx);
        const toolboxNode = graphNodes.find(n => n.id === `toolbox_${agentName}`);
        if (toolboxNode) {
            const toolboxData = toolboxNode.data?.data as ToolboxData | undefined;
            if (toolboxData?.tools) {
                // For virtual tool: check if tool exists in toolbox
                // For virtual connector: check if tool+connector combo exists
                const toolName = isVirtualTool
                    ? remainder.substring(firstUnderscoreIdx + 1)
                    : remainder.substring(firstUnderscoreIdx + 1, remainder.lastIndexOf('_'));
                const connectorName = isVirtualConnector ? remainder.substring(remainder.lastIndexOf('_') + 1) : undefined;

                const matchingToolOrConnector = toolboxData.tools.find(t => {
                    if (t.tool.name !== toolName) return false;
                    if (isVirtualConnector) {
                        return t.connector?.name === connectorName;
                    }
                    return true;
                });

                return !!matchingToolOrConnector;
            }
        }
    }

    return false;
};
