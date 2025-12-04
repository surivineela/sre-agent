import { Card, Link, mergeClasses, Text } from '@fluentui/react-components';
import { ChevronUpDown20Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps } from '@xyflow/react';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, SkillGroupData } from '../../../Contracts/ExtendedAgentGraph';
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

Handles.displayName = 'SkillGroupHandles';

export const SkillGroupCard = (props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const { id, data } = props;
    const intl = useIntl();
    const { hoverNode, unHoverNode, nodesToHighlight, hoveredNodeId, toggleSkillGroupExpanded } = useContext(ExtendedAgentGraphContext);

    const { toolCard, cardHighlighted, cardHovered, cardContent, titleRow, nameBlock, nameText } = useToolNodeStyles();
    const { groupContainer, expandLink, expandLinkContainer } = useSkillGroupCardStyles();

    const isHovered = hoveredNodeId === id;
    const skillGroupData = data?.data as SkillGroupData | undefined;
    const skillCount = skillGroupData?.skillCount ?? 0;

    const cardStyles = mergeClasses(
        toolCard,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined
    );

    const handleExpandClick = (e: React.MouseEvent) => {
        e.stopPropagation();
        toggleSkillGroupExpanded?.();
    };

    return (
        <div className={groupContainer} onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card className={cardStyles}>
                <div className={cardContent}>
                    <div className={titleRow}>
                        <EntityIcon type="skill" iconStyle={{ height: '24px', width: '24px' }} />
                        <div className={nameBlock}>
                            <Text className={nameText}>Skills ({skillCount})</Text>
                        </div>
                    </div>
                </div>
            </Card>
            <div className={expandLinkContainer}>
                <Link className={expandLink} onClick={handleExpandClick}>
                    <ChevronUpDown20Regular aria-hidden="true" />
                    {intl.formatMessage(ExtendedAgentsGraphResources.expandToShowMore)}
                </Link>
            </div>
        </div>
    );
};

SkillGroupCard.displayName = 'SkillGroupCard';
