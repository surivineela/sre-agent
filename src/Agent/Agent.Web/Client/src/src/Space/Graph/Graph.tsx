import { memo } from "react";
import { useGraph } from "../Hooks/useGraph";
import { GraphContext } from "../Contracts/Graph";
import { Controls, MiniMap, ReactFlow, ReactFlowProvider } from "@xyflow/react";
import { GraphCard } from "./GraphCard";
import Panel from "./Panel";
import ResourceSelector from "./ResourceSelector";
import { Spinner } from "@fluentui/react-components";
import { CustomEdge } from "./CustomEdge";
import { CUSTOM_EDGE_TYPE, GRAF_CARD_TYPE } from "./Constants";

import '@xyflow/react/dist/style.css';
import { useGraphStyles } from "../Styles/Graph.styles";

interface IGraphProps {
    transferDataToActivities: (threadId?: string | null) => void
}

const Graph = (props: IGraphProps) => {
    return (
        <ReactFlowProvider>
            <GraphContent {...props} />
        </ReactFlowProvider>
    )
};

const GraphContent = (props: IGraphProps) => {
    const {
        nodes,
        edges,
        isLoadingSubresources,
        onNodesChange,
        onEdgesChange,
        showSubresources,
        hideSubresources,
        areSubresourcesVisible,
        openPanel,
        closePanel,
        isPanelOpen,
        selectedNode,
        onAppGroupUpdate,
        isLoading,
        hoverNode,
        unHoverNode,
        nodesToHightlight,
        edgesToHightlight
    } = useGraph();

    const { root, reactFlow, spinner } = useGraphStyles();

    return <GraphContext.Provider
        value={{
            showSubresources,
            hideSubresources,
            areSubresourcesVisible,
            isLoadingSubresources,
            openPanel,
            closePanel,
            isPanelOpen,
            selectedNode,
            hoverNode,
            unHoverNode,
            nodesToHightlight,
            edgesToHightlight,
        }}>
        <div className={root}>
            <ResourceSelector onAppGroupUpdate={onAppGroupUpdate} />
            <div className={reactFlow}>
                {isLoading ?
                    <Spinner size={'large'} className={spinner} /> :
                    <ReactFlow
                        fitView
                        nodeTypes={{ [GRAF_CARD_TYPE]: GraphCard }}
                        edgeTypes={{ [CUSTOM_EDGE_TYPE]: CustomEdge }}
                        nodes={nodes}
                        edges={edges}
                        onNodesChange={onNodesChange}
                        onEdgesChange={onEdgesChange}
                        proOptions={{ hideAttribution: true }}>
                        <Controls />
                        <MiniMap />
                    </ReactFlow >
                }
            </div>
            <Panel
                {...props}
            />
        </div>
    </GraphContext.Provider>
}

export default memo(Graph);