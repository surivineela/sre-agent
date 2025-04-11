import { memo } from "react";
import { useGraph } from "../Hooks/useGraph";
import { Spinner } from "@fluentui/react-components";
import Graph from "./Graph";

const KnowledgeGraph = () => {
    const { nodes, links, isLoading, addNodes, hideNode, showNode } = useGraph();

    return isLoading ?
        <Spinner size={'large'} style={{ position: 'fixed', top: '50%', left: '50%' }} /> :
        <Graph nodes={nodes} links={links} addNodes={addNodes} hideNode={hideNode} showNode={showNode} />;
};

export default memo(KnowledgeGraph);