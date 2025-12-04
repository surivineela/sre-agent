import { Badge, Card, mergeClasses, Text } from '@fluentui/react-components';
import { useContext } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, Skill } from '../../../Contracts/ExtendedAgentGraph';
import { useToolNodeStyles } from '../../../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from '../../EntityIcon';
import { useSkillGroupCardStyles } from './SkillGroupCard.styles';

interface SkillCardInnerProps {
    skill: Skill;
}

export const SkillCardInner = ({ skill }: SkillCardInnerProps) => {
    const intl = useIntl();
    const { selectedNodeId, setSelectedNodeId, expandInfoPanel } = useContext(ExtendedAgentGraphContext);

    const { toolCard, cardSelected, cardContent, titleRow, nameBlock, nameText } = useToolNodeStyles();
    const { compactSkillCard } = useSkillGroupCardStyles();

    const skillId = `skill_${skill.name}`;
    const isSelectedNode = selectedNodeId === skillId;
    const toolCount = skill?.tools?.length ?? 0;

    const cardStyles = mergeClasses(toolCard, compactSkillCard, isSelectedNode ? cardSelected : undefined);

    return (
        <Card
            onClick={e => {
                e.stopPropagation();
                setSelectedNodeId(skillId);
                expandInfoPanel();
            }}
            className={cardStyles}
        >
            <div className={cardContent}>
                <div className={titleRow}>
                    <EntityIcon type="skill" iconStyle={{ height: '24px', width: '24px' }} />
                    <div className={nameBlock}>
                        <Text className={nameText}>{skill.name}</Text>
                    </div>
                    {toolCount > 0 && (
                        <Badge appearance="tint" color="informative" size="small">
                            {intl.formatMessage(ExtendedAgentsGraphResources.toolCount, { count: toolCount })}
                        </Badge>
                    )}
                </div>
            </div>
        </Card>
    );
};

SkillCardInner.displayName = 'SkillCardInner';
