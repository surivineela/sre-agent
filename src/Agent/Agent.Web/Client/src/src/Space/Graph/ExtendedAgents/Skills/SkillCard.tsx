import { Badge, Card, Link, mergeClasses, Text } from '@fluentui/react-components';
import { ChevronDownUp20Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps } from '@xyflow/react';
import { memo, useContext } from 'react';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, Skill } from '../../../Contracts/ExtendedAgentGraph';
import { HANDLE_POSITION_MAP, HANDLE_POSITIONS } from '../../../Contracts/Graph';
import { useToolNodeStyles } from '../../../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from '../../EntityIcon';
import { getHandleId } from '../../Utility';
import { useSkillGroupCardStyles } from './SkillGroupCard.styles';

const Handles = memo(() => {
    const { handle } = useToolNodeStyles();

    return (
        <>
            {HANDLE_POSITIONS.map(position => (
                <div key={position}>
                    <Handle className={handle} type="target" position={HANDLE_POSITION_MAP[position]} id={getHandleId(position, true)} />
                    <Handle className={handle} type="source" position={HANDLE_POSITION_MAP[position]} id={getHandleId(position, false)} />
                </div>
            ))}
        </>
    );
});

Handles.displayName = 'SkillHandles';

export const SkillCard = (props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const { id, data } = props;
    const {
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        selectedNodeId,
        setSelectedNodeId,
        expandInfoPanel,
        hoveredNodeId,
        toggleSkillGroupExpanded,
    } = useContext(ExtendedAgentGraphContext);

    const { toolCard, cardHighlighted, cardHovered, cardSelected, cardContent, titleRow, nameBlock, nameText } = useToolNodeStyles();
    const { collapseLinkContainer, collapseLink, compactSkillCard } = useSkillGroupCardStyles();
    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNodeId === id;

    const skill = (data?.data as Skill | undefined) ?? undefined;
    const toolCount = skill?.tools?.length ?? 0;
    const isLastInGroup = data?.isLastInGroup ?? false;

    const cardStyles = mergeClasses(
        toolCard,
        compactSkillCard,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const handleCollapseClick = (e: React.MouseEvent) => {
        e.stopPropagation();
        toggleSkillGroupExpanded?.();
    };

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card
                onClick={() => {
                    setSelectedNodeId(data?.id);
                    expandInfoPanel();
                }}
                className={cardStyles}
            >
                <div className={cardContent}>
                    <div className={titleRow}>
                        <EntityIcon type="skill" iconStyle={{ height: '24px', width: '24px' }} />
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                        </div>
                        {toolCount > 0 && (
                            <Badge appearance="tint" color="informative" size="small">
                                {toolCount} {toolCount === 1 ? 'tool' : 'tools'}
                            </Badge>
                        )}
                    </div>
                </div>
            </Card>
            {isLastInGroup && (
                <div className={collapseLinkContainer}>
                    <Link className={collapseLink} onClick={handleCollapseClick}>
                        <ChevronDownUp20Regular aria-hidden="true" />
                        Collapse to show less
                    </Link>
                </div>
            )}
        </div>
    );
};

SkillCard.displayName = 'SkillCard';
