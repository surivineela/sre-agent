import { Link } from '@fluentui/react-components';
import { ChevronDownUp20Regular } from '@fluentui/react-icons';
import { Handle, Node, NodeProps } from '@xyflow/react';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, SkillGroupData } from '../../../Contracts/ExtendedAgentGraph';
import { HANDLE_POSITIONS, HANDLE_POSITION_MAP } from '../../../Contracts/Graph';
import { useToolNodeStyles } from '../../../Styles/ExtendedAgentGraph.styles';
import { getHandleId } from '../../Utility';
import { SkillCardInner } from './SkillCardInner';
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

Handles.displayName = 'ExpandedSkillGroupHandles';

export const ExpandedSkillGroup = (props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const { id, data } = props;
    const intl = useIntl();
    const { hoverNode, unHoverNode, toggleSkillGroupExpanded } = useContext(ExtendedAgentGraphContext);

    const { groupContainer, skillsContainer, collapseLinkContainer, collapseLink } = useSkillGroupCardStyles();

    const skillGroupData = data?.data as SkillGroupData | undefined;
    const skills = skillGroupData?.skills ?? [];

    const handleCollapseClick = (e: React.MouseEvent) => {
        e.stopPropagation();
        toggleSkillGroupExpanded?.();
    };

    return (
        <div className={groupContainer} onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <div className={skillsContainer}>
                {skills.map(skill => (
                    <SkillCardInner key={skill.name} skill={skill} />
                ))}
            </div>
            <div className={collapseLinkContainer}>
                <Link className={collapseLink} onClick={handleCollapseClick}>
                    <ChevronDownUp20Regular aria-hidden="true" />
                    {intl.formatMessage(ExtendedAgentsGraphResources.collapseToShowLess)}
                </Link>
            </div>
        </div>
    );
};

ExpandedSkillGroup.displayName = 'ExpandedSkillGroup';
