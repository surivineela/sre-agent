import { memo } from "react";
import { useGraph } from "../Hooks/useGraph";
import { Spinner } from "@fluentui/react-components";
import Graph from "./Graph";

const KnowledgeGraph = ({ transferDataToActivities }: { transferDataToActivities: (threadId?: string | null) => void }) => {
    const { nodes, links, isLoading, addNodes, hideNode, showNode, shouldShowNode, queryNodes } = useGraph();

    return isLoading ?
        <Spinner size={'large'} style={{ position: 'fixed', top: '50%', left: '50%' }} /> :
        <Graph nodes={nodes} links={links} addNodes={addNodes} hideNode={hideNode} showNode={showNode} queryNodes={queryNodes} shouldShowNode={shouldShowNode} transferDataToActivities={transferDataToActivities} />;
};

export default memo(KnowledgeGraph);