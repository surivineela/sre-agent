import { memo, useEffect, useRef, useState } from 'react';
import ForceGraph2D, { ForceGraphMethods, LinkObject, NodeObject } from 'react-force-graph-2d';
import { forceCollide } from 'd3-force';
import { GraphLink, GraphNode } from '../Hooks/useGraph';

interface IGraphProps {
    nodes: GraphNode[];
    links: GraphLink[];
}

const Graph = ({ nodes, links }: IGraphProps) => {
    const [initialCenter, setInitialCenter] = useState(true);
    const [width, setWidth] = useState(0);
    const [height, setHeight] = useState(0);
    const [selectedNode, setSelectedNode] = useState<GraphNode>();

    const forceRef = useRef<ForceGraphMethods<NodeObject<GraphNode>, LinkObject<GraphNode, GraphLink>>>();

    useEffect(() => {
        forceRef.current?.d3Force('charge')?.distanceMax(100);
        forceRef.current?.d3Force('charge')?.strength((node: GraphNode) => (node.links.length === 0 || !node.areChildrenVisible ? -1 : -40));
        forceRef.current?.d3Force('link')?.distance(60);
        forceRef.current?.d3Force(
            'collide',
            forceCollide(15)
        );
    }, []);

    useEffect(() => {
        const handleResize = () => {
            setWidth(window.innerWidth - 350);
            setHeight(window.innerHeight - 40);
        };

        window.addEventListener('resize', handleResize);
        handleResize();

        return () => {
            window.removeEventListener('resize', handleResize);
        };
    }, []);

    console.log(selectedNode);

    return (
        <div style={{ display: 'flex', flexDirection: 'row', width: '100%', height: '100%' }}>
            <ForceGraph2D
                width={width}
                height={height}
                ref={forceRef}
                graphData={{ nodes, links }}
                cooldownTicks={20}
                nodeRelSize={5}
                onNodeClick={node => setSelectedNode(node)}
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
                nodeCanvasObject={(node, ctx, globalScale) => {
                    const isNodeHealthy = node?.properties?.scorecard?.health !== 'unhealthy';
                    const iconSize = isNodeHealthy ? 0 : 26 / (globalScale * 1.2); // Adjust icon size
                    // const fontSize = 20 / (globalScale * 1.2); // Scale text size based on zoom
                    // const padding = 1 / globalScale; // Space between icon and text

                    // ctx.font = `${fontSize}px Arial`;
                    // const textWidth = ctx.measureText(node.name).width;
                    // const totalWidth = iconSize + padding + textWidth;

                    // // Calculate the starting X position to center align
                    // const startX = node?.x ?? 0 - totalWidth / 2;
                    // const startY = node?.y ?? 0 - iconSize / 2; // Align icon & text to middle

                    // // const img = images[node.id]; // Preloaded images

                    // ctx.fillStyle = 'gray'
                    // ctx.fillRect(node?.x ?? 0 - totalWidth / 2, node?.y ?? 0 - iconSize / 2, totalWidth, fontSize * 2);

                    // // if (img) {
                    // const image = new Image();
                    // image.src = 'https://upload.wikimedia.org/wikipedia/commons/a/a6/Anonymous_emblem.svg';
                    // image.onload = () => {
                    //     ctx.drawImage(image, startX, startY, iconSize, iconSize);
                    // }

                    // // Set text properties
                    // ctx.textAlign = "left";
                    // ctx.textBaseline = "middle";
                    // ctx.fillStyle = "black"; // Adjust text color
                    // ctx.fillText(node.name, startX + iconSize + padding, node?.y ?? 0 - fontSize / 2);
                    // ctx.fillText(node.name, startX + iconSize + padding, node?.y ?? 0 + fontSize / 2); // Draw text twice for better visibility
                    // const mainText = node.name;
                    // const mainTextWidth = globalScale > 1.5 ? ctx.measureText(mainText).width + 2 : 0;

                    // const mainX = node.x - mainTextWidth / 2;
                    // const mainY = node.y - iconSize;
                    // ctx.textAlign = 'center';
                    // ctx.textBaseline = 'middle';
                    // ctx.fillRect(mainX, mainY, mainTextWidth, iconSize);
                    // // ctx.fillStyle = 'black';
                    // ctx.fillText(mainText, node.x, node.y - iconSize / 2);

                    // ctx.arc(node.x, node.y, 10, 2, 20, false);
                    const text = node.name;
                    const isRoot = node.type === 'subscription';
                    const fontSize = (isRoot ? 20 : 14) / (globalScale * 1.2);
                    ctx.font = `${fontSize}px Sans-Serif`;
                    // ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    const textWidth = ctx.measureText(text).width;
                    // const bgHeight = globalScale > 1.5 ? fontSize + 1 : 0;

                    const padding = isNodeHealthy ? 0 : 2;

                    const startX = node?.x ?? 0 - ((textWidth + padding + iconSize) / 2);
                    const startY = node?.y ?? 0 - iconSize / 2;

                    ctx.fillRect(startX, startY, textWidth + padding + iconSize, iconSize);

                    const image = new Image();
                    image.src = 'https://upload.wikimedia.org/wikipedia/commons/a/a6/Anonymous_emblem.svg';
                    image.onload = () => {
                        ctx.drawImage(image, startX, startY, iconSize, iconSize);
                    }

                    // if (globalScale > 1.5) {
                    //     if (globalScale > 3 || node.isRoot) {
                    if (node.type !== 'resource' || globalScale > 2.5 || !isNodeHealthy) {

                        // ctx.fillStyle = node.type === 'resource' ? 'white' : 'black';
                        ctx.fillStyle = 'black'
                        ctx.fillText(text, startX + padding + iconSize, node?.y ?? 0);
                    }
                }}
                nodeVal={node => {
                    return node.type === 'subscription' ? 8 : node.type === 'appGroup' ? 5 : 2;
                }}
                enableNodeDrag={true}
                enableZoomInteraction={true}
                linkDirectionalArrowRelPos={2}
                linkDirectionalArrowLength={5}
            />
            {/* <div style={{ borderLeft: selectedNode ? '1px solid rgba(204,204,204,.8)' : undefined, flex: '0 0 300px', padding: '10px' }}>
                {selectedNode ? (
                    <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'flex-start', gap: '10px' }}>
                        <FormLabel displayValue="Name" styles={{ root: { fontWeight: 'bold' } }}>
                            <span style={{ overflowWrap: 'break-word' }}>{selectedNode.name}</span>
                        </FormLabel>
                        <FormLabel displayValue="Type" styles={{ root: { fontWeight: 'bold' } }}>
                            <span style={{ overflowWrap: 'break-word' }}>{selectedNode.resourceType}</span>
                        </FormLabel>
                    </div>
                ) : null}
            </div> */}
        </div>
    );
};

export default memo(Graph);
