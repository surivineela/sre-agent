import { memo, useEffect, useRef, useState } from 'react';
import ForceGraph2D, { ForceGraphMethods, LinkObject, NodeObject } from 'react-force-graph-2d';
import { forceCollide } from 'd3-force';
import { GraphLink, GraphNode, ResourceExtended, ScoreCardObject } from '../Hooks/useGraph';
import Panel from './Panel';

interface IGraphProps {
    nodes: GraphNode[];
    links: GraphLink[];
    addNodes: (parentNode: GraphNode, nodes: GraphNode[]) => void;
    hideNode: (node: GraphNode) => void;
    showNode: (node: GraphNode) => void;
    queryNodes: (parentNode: GraphNode) => Promise<GraphNode[]>;
    shouldShowNode: (node: GraphNode) => boolean;
    transferDataToActivities: (threadId?: string | null) => void
}

const Graph = ({ nodes, links, addNodes, hideNode, showNode, queryNodes, shouldShowNode, transferDataToActivities }: IGraphProps) => {
    const [initialCenter, setInitialCenter] = useState(true);
    const [width, setWidth] = useState(0);
    const [height, setHeight] = useState(0);
    const [selectedNode, setSelectedNode] = useState<GraphNode>();
    const [loadingNodeId, setLoadingNodeId] = useState<string>();

    const forceRef = useRef<ForceGraphMethods<NodeObject<GraphNode>, LinkObject<GraphNode, GraphLink>>>();

    useEffect(() => {
        forceRef.current?.d3Force('charge')?.distanceMax(100);
        forceRef.current?.d3Force('charge')?.strength((node: NodeObject<GraphNode>) => (node.links ?? []).length === 0 ? -1 : -40);
        forceRef.current?.d3Force('link')?.distance(60);
        forceRef.current?.d3Force(
            'collide',
            forceCollide(20)
        );
    }, []);

    useEffect(() => {
        const handleResize = () => {
            setWidth(window.innerWidth - 150);
            setHeight(window.innerHeight - 40);
        };

        window.addEventListener('resize', handleResize);
        handleResize();

        return () => {
            window.removeEventListener('resize', handleResize);
        };
    }, []);


    return (
        <div>
            <ForceGraph2D
                width={width}
                height={height}
                ref={forceRef}
                graphData={{ nodes, links }}
                cooldownTicks={50}
                nodeRelSize={5}
                nodeVisibility={(node: GraphNode) => node.isVisible}
                linkVisibility={(link: GraphLink) => link.isVisible}
                nodeColor={node => node.type === 'subscription' ? '#0e4775' : node.type === 'appGroup' ? '#0f6cbd' : '#62abf5'}
                onNodeClick={async (node: GraphNode) => {
                    if (!loadingNodeId) {
                        setSelectedNode(node);

                        if (!node.links) {
                            setLoadingNodeId(node.id);
                            const nodes = await queryNodes(node);
                            setLoadingNodeId(undefined)
                            addNodes(node, nodes);
                        } else if (shouldShowNode(node)) {
                            showNode(node);
                        } else {
                            hideNode(node);
                        }
                    }

                }}
                onBackgroundClick={() => setSelectedNode(undefined)}
                onNodeDragEnd={node => {
                    node.fx = node.x;
                    node.fy = node.y;
                }}
                onEngineStop={() => {
                    if (initialCenter && nodes.length > 5) {
                        forceRef.current?.zoomToFit(300, 15);
                    }
                    setInitialCenter(false);
                }}
                nodeCanvasObjectMode={() => 'after'}
                nodeCanvasObject={(node: NodeObject<GraphNode>, ctx, globalScale) => {
                    let scoreCard: string[] | null | undefined = null;
                    const nodeProperties = node.properties;

                    if (nodeProperties) {
                        if ('properties' in nodeProperties) {
                            const resourceExtended: ResourceExtended = nodeProperties as ResourceExtended;
                            scoreCard = resourceExtended.properties?.appHealthInfo
                        } else {
                            scoreCard = nodeProperties.appHealthInfo;
                        }
                    }

                    let scoreCardObject: ScoreCardObject | null | undefined = null;
                    try {
                        scoreCardObject = scoreCard?.[0] ? JSON.parse(scoreCard[0]) : null;
                    } catch {
                        scoreCardObject = null;
                    }

                    const isNodeHealthy = !scoreCardObject || scoreCardObject.Health.toLowerCase() !== 'unhealthy';
                    const iconSize = isNodeHealthy ? 0 : 26 / (globalScale * 1.2); // Adjust icon size

                    const text = node.id === loadingNodeId ? 'Loading...' : node.name;
                    const fontSize = (node.type === 'subscription' ? 20 : node.type === 'appGroup' ? 16 : 14) / (globalScale * 1.2);
                    ctx.font = `${fontSize}px Sans-Serif`;
                    ctx.textBaseline = 'middle';
                    const textWidth = ctx.measureText(text).width;
                    const bgHeight = globalScale > 1.5 ? fontSize + 1 : 0;

                    const padding = isNodeHealthy ? 0 : 2;

                    const startX = (node?.x ?? 0) - ((textWidth + padding + iconSize) / 2);
                    const startY = (node?.y ?? 0) - Math.max(iconSize, bgHeight) / 2;


                    if (node.type !== 'resource' || globalScale > 2.5 || !isNodeHealthy) {
                        ctx.fillRect(startX, startY, textWidth + padding + iconSize, Math.max(bgHeight, iconSize));

                        const image = new Image();
                        image.src = './error.svg';
                        ctx.drawImage(image, startX, startY, iconSize, iconSize);

                        ctx.fillStyle = 'white'
                        ctx.fillText(text, startX + padding + iconSize, node?.y ?? 0);
                    }
                }}
                nodeVal={node => {
                    return node.type === 'subscription' ? 10 : node.type === 'appGroup' ? 7 : 4;
                }}
                enableNodeDrag={true}
                enableZoomInteraction={true}
                linkDirectionalArrowRelPos={2}
                linkDirectionalArrowLength={5}
                nodeLabel={node => node.properties?.type ?? node.name}
            />
            <Panel
                node={selectedNode}
                setSelectedNode={setSelectedNode}
                transferDataToActivities={transferDataToActivities}
            />
        </div>
    );
};

export default memo(Graph);
