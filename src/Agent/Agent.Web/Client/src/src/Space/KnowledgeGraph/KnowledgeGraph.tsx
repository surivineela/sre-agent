import { memo } from "react";
import { useGraph } from "../Hooks/useGraph";
import { Spinner } from "@fluentui/react-components";
import Graph from "./Graph";

const KnowledgeGraph = () => {
    const { nodes, links, isLoading } = useGraph();

    return isLoading ? <Spinner size={'large'} style={{ position: 'fixed', top: '50%', left: '50%' }} /> : <Graph nodes={nodes} links={links} />;
};

export default memo(KnowledgeGraph);