import { graphlib, layout } from '@dagrejs/dagre';
import { useTheme } from '@fluentui/react';
import { Badge, Card, CardHeader, makeStyles, shorthands, Text, tokens, Tooltip } from '@fluentui/react-components';
import { Organization24Regular } from '@fluentui/react-icons';
import {
    BaseEdge,
    Controls,
    Edge,
    EdgeLabelRenderer,
    EdgeProps,
    getBezierPath,
    Handle,
    MiniMap,
    Node,
    NodeProps,
    Position,
    ReactFlow,
    ReactFlowProvider,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { memo, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeGraphSearchResult } from '../../Common/Contracts/DataPlane/Message';
import { KnowledgeGraphCardResources } from '../../Strings/SREAgentResources';

interface KnowledgeGraphPanelContentProps {
    knowledgeGraphResult: KnowledgeGraphSearchResult;
}

// Node data type for knowledge graph entities - index signature required by React Flow
interface KnowledgeGraphNodeData {
    [key: string]: unknown;
    label: string;
    name: string;
    entityType: string;
    observations: string[];
}

// Edge data type for knowledge graph relations - index signature required by React Flow
interface KnowledgeGraphEdgeData {
    [key: string]: unknown;
    relationType: string;
}

// Node and edge size constants
const NODE_WIDTH = 200;
const NODE_HEIGHT = 80;

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
        width: '100%',
        height: '100%',
        minHeight: '400px',
    },
    queryInfo: {
        backgroundColor: tokens.colorNeutralBackground3,
        ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
        ...shorthands.borderRadius(tokens.borderRadiusSmall),
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        flexShrink: 0,
    },
    noResults: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        fontStyle: 'italic',
    },
    graphContainer: {
        flex: 1,
        minHeight: '350px',
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        overflow: 'hidden',
    },
    // Entity node styles
    entityCard: {
        width: `${NODE_WIDTH}px`,
        minHeight: `${NODE_HEIGHT}px`,
        cursor: 'pointer',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke1),
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
            ...shorthands.border('1px', 'solid', tokens.colorBrandStroke1),
        },
    },
    entityCardSelected: {
        ...shorthands.border('2px', 'solid', tokens.colorBrandStroke1),
        boxShadow: tokens.shadow8,
    },
    entityHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    entityIcon: {
        fontSize: '20px',
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
    },
    entityName: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase300,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        maxWidth: '140px',
    },
    entityTypeBadge: {
        fontSize: tokens.fontSizeBase100,
        marginTop: tokens.spacingVerticalXS,
    },
    handle: {
        width: '8px',
        height: '8px',
        backgroundColor: tokens.colorBrandBackground,
        ...shorthands.border('2px', 'solid', tokens.colorNeutralBackground1),
    },
    // Edge label styles
    edgeLabel: {
        position: 'absolute',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding(tokens.spacingVerticalXXS, tokens.spacingHorizontalXS),
        ...shorthands.borderRadius(tokens.borderRadiusSmall),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        fontSize: tokens.fontSizeBase100,
        fontWeight: tokens.fontWeightMedium,
        color: tokens.colorBrandForeground1,
        pointerEvents: 'all',
        cursor: 'default',
        transform: 'translate(-50%, -50%)',
    },
    // Details panel styles
    detailsPanel: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.padding(tokens.spacingVerticalM),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        flexShrink: 0,
    },
    detailsTitle: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase400,
        marginBottom: tokens.spacingVerticalS,
    },
    detailsType: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        marginBottom: tokens.spacingVerticalS,
    },
    observationsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    observationsTitle: {
        fontWeight: tokens.fontWeightMedium,
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase200,
        marginBottom: tokens.spacingVerticalXS,
    },
    observationItem: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        paddingLeft: tokens.spacingHorizontalM,
        ...shorthands.borderLeft('2px', 'solid', tokens.colorBrandStroke1),
    },
});

// Custom node component for knowledge graph entities
const EntityNode = memo(({ data, selected }: NodeProps<Node<KnowledgeGraphNodeData>>) => {
    const styles = useStyles();

    return (
        <>
            <Handle type="target" position={Position.Left} className={styles.handle} />
            <Tooltip content={data.name} relationship="label">
                <Card className={`${styles.entityCard} ${selected ? styles.entityCardSelected : ''}`}>
                    <CardHeader
                        header={
                            <div className={styles.entityHeader}>
                                <Organization24Regular className={styles.entityIcon} />
                                <Text className={styles.entityName}>{data.name}</Text>
                            </div>
                        }
                        description={
                            data.entityType && (
                                <Badge appearance="tint" size="small" className={styles.entityTypeBadge}>
                                    {data.entityType}
                                </Badge>
                            )
                        }
                    />
                </Card>
            </Tooltip>
            <Handle type="source" position={Position.Right} className={styles.handle} />
        </>
    );
});

EntityNode.displayName = 'EntityNode';

// Custom edge component with relation type label
const RelationEdge = memo(
    ({ id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data }: EdgeProps<Edge<KnowledgeGraphEdgeData>>) => {
        const styles = useStyles();
        const [edgePath, labelX, labelY] = getBezierPath({
            sourceX,
            sourceY,
            sourcePosition,
            targetX,
            targetY,
            targetPosition,
        });

        return (
            <>
                <BaseEdge id={id} path={edgePath} style={{ stroke: tokens.colorBrandStroke1, strokeWidth: 2 }} />
                <EdgeLabelRenderer>
                    <div
                        className={styles.edgeLabel}
                        style={{
                            left: labelX,
                            top: labelY,
                        }}
                    >
                        {data?.relationType}
                    </div>
                </EdgeLabelRenderer>
            </>
        );
    }
);

RelationEdge.displayName = 'RelationEdge';

// Node and edge type constants
const NODE_TYPES = { entity: EntityNode };
const EDGE_TYPES = { relation: RelationEdge };

// Function to compute graph layout using dagre
const computeLayout = (
    nodes: Node<KnowledgeGraphNodeData>[],
    edges: Edge<KnowledgeGraphEdgeData>[]
): { nodes: Node<KnowledgeGraphNodeData>[]; edges: Edge<KnowledgeGraphEdgeData>[] } => {
    const dagreGraph = new graphlib.Graph().setDefaultEdgeLabel(() => ({}));
    dagreGraph.setGraph({ rankdir: 'LR', ranksep: 100, nodesep: 50 });

    nodes.forEach(node => {
        dagreGraph.setNode(node.id, { width: NODE_WIDTH, height: NODE_HEIGHT });
    });

    edges.forEach(edge => {
        dagreGraph.setEdge(edge.source, edge.target);
    });

    layout(dagreGraph);

    const layoutedNodes = nodes.map(node => {
        const nodeWithPosition = dagreGraph.node(node.id);
        return {
            ...node,
            position: {
                x: nodeWithPosition.x - NODE_WIDTH / 2,
                y: nodeWithPosition.y - NODE_HEIGHT / 2,
            },
        };
    });

    return { nodes: layoutedNodes, edges };
};

// Convert knowledge graph data to React Flow nodes and edges
const useKnowledgeGraphFlow = (knowledgeGraphResult: KnowledgeGraphSearchResult) => {
    return useMemo(() => {
        const { entities, relations } = knowledgeGraphResult;

        // Create a map of entity names to indices for quick lookup
        const entityMap = new Map<string, number>();
        entities.forEach((entity, index) => {
            entityMap.set(entity.name, index);
        });

        // Create nodes from entities
        const nodes: Node<KnowledgeGraphNodeData>[] = entities.map((entity, index) => ({
            id: `entity-${index}`,
            type: 'entity',
            position: { x: 0, y: 0 }, // Will be computed by layout
            data: {
                ...entity,
                label: entity.name,
            },
        }));

        // Create edges from relations
        const edges: Edge<KnowledgeGraphEdgeData>[] = [];
        relations.forEach((relation, index) => {
            const sourceIndex = entityMap.get(relation.from);
            const targetIndex = entityMap.get(relation.to);

            // Only create edge if both source and target entities exist
            if (sourceIndex !== undefined && targetIndex !== undefined) {
                edges.push({
                    id: `edge-${index}`,
                    source: `entity-${sourceIndex}`,
                    target: `entity-${targetIndex}`,
                    type: 'relation',
                    data: {
                        relationType: relation.relationType,
                    },
                });
            }
        });

        // Apply layout
        return computeLayout(nodes, edges);
    }, [knowledgeGraphResult]);
};

// Inner graph component
const KnowledgeGraphFlowContent = ({ knowledgeGraphResult }: KnowledgeGraphPanelContentProps) => {
    const styles = useStyles();
    const theme = useTheme();
    const { nodes, edges } = useKnowledgeGraphFlow(knowledgeGraphResult);
    const [selectedNode, setSelectedNode] = useState<Node<KnowledgeGraphNodeData> | null>(null);

    const onNodeClick = useCallback((_event: React.MouseEvent, node: Node<KnowledgeGraphNodeData>) => {
        setSelectedNode(node);
    }, []);

    const onPaneClick = useCallback(() => {
        setSelectedNode(null);
    }, []);

    return (
        <>
            <div className={styles.graphContainer}>
                <ReactFlow
                    nodes={nodes}
                    edges={edges}
                    nodeTypes={NODE_TYPES}
                    edgeTypes={EDGE_TYPES}
                    onNodeClick={onNodeClick}
                    onPaneClick={onPaneClick}
                    fitView
                    fitViewOptions={{ padding: 0.2 }}
                    proOptions={{ hideAttribution: true }}
                    colorMode={theme.isInverted ? 'dark' : 'light'}
                    minZoom={0.1}
                    maxZoom={2}
                >
                    <Controls />
                    <MiniMap />
                </ReactFlow>
            </div>
            {selectedNode && (
                <div className={styles.detailsPanel}>
                    <Text className={styles.detailsTitle}>{selectedNode.data.name}</Text>
                    {selectedNode.data.entityType && (
                        <div className={styles.detailsType}>
                            <Badge appearance="tint" size="medium">
                                {selectedNode.data.entityType}
                            </Badge>
                        </div>
                    )}
                    {selectedNode.data.observations && selectedNode.data.observations.length > 0 && (
                        <div className={styles.observationsContainer}>
                            <Text className={styles.observationsTitle}>Observations:</Text>
                            {selectedNode.data.observations.map((observation, index) => (
                                <div key={`obs-${index}`} className={styles.observationItem}>
                                    {observation}
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            )}
        </>
    );
};

const KnowledgeGraphPanelContent = ({ knowledgeGraphResult }: KnowledgeGraphPanelContentProps) => {
    const styles = useStyles();
    const intl = useIntl();

    const hasNoResults = knowledgeGraphResult.totalEntities === 0 && knowledgeGraphResult.totalRelations === 0;

    return (
        <div className={styles.container}>
            {/* Query Info */}
            {knowledgeGraphResult.query && (
                <div className={styles.queryInfo}>
                    <strong>{intl.formatMessage(KnowledgeGraphCardResources.queryLabel)}</strong> {knowledgeGraphResult.query}
                </div>
            )}

            {/* No Results Message */}
            {hasNoResults && <Text className={styles.noResults}>{intl.formatMessage(KnowledgeGraphCardResources.noResults)}</Text>}

            {/* Graph Visualization */}
            {!hasNoResults && (
                <ReactFlowProvider>
                    <KnowledgeGraphFlowContent knowledgeGraphResult={knowledgeGraphResult} />
                </ReactFlowProvider>
            )}
        </div>
    );
};

export default memo(KnowledgeGraphPanelContent);
