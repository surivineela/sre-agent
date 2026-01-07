import { EntityCard as CopilotEntityCard, EntityTitle, Subtitle1 } from '@fluentui-copilot/react-copilot';
import { FC, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, ScheduledTasksResources, SettingsTabResources } from '../../../../Strings/SREAgentResources';
import { EntityIcon, EntityIconType } from '../../EntityIcon';
import { TableViewTabValue } from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';

interface EntityCardProps {
    type: TableViewTabValue;
    entityCount: number;
    handleCardClick: (cardType: TableViewTabValue) => void;
}

export const EntityCard: FC<EntityCardProps> = ({ type, entityCount, handleCardClick }) => {
    const intl = useIntl();
    const styles = useListViewStyles();

    const title = useMemo(() => {
        switch (type) {
            case 'agents':
                return intl.formatMessage(SettingsTabResources.subAgents);
            case 'incidentTriggers':
                return intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggers);
            case 'scheduledTasks':
                return intl.formatMessage(ScheduledTasksResources.scheduledTasks);
            case 'kustoTools':
                return intl.formatMessage(ExtendedAgentsGraphResources.kustoTools);
            case 'skills':
                return intl.formatMessage(ExtendedAgentsGraphResources.skillsLabel);
            default:
                return type;
        }
    }, [intl, type]);

    const iconType = useMemo<EntityIconType>(() => {
        switch (type) {
            case 'agents':
                return 'agent';
            case 'incidentTriggers':
                return 'incidentTrigger';
            case 'scheduledTasks':
                return 'scheduledTask';
            case 'kustoTools':
                return 'toolWithGear';
            case 'skills':
                return 'skill';
            default:
                return 'genericTrigger';
        }
    }, [type]);

    return (
        <CopilotEntityCard
            orientation={'horizontal'}
            className={styles.clickableCard}
            onClick={() => handleCardClick(type)}
            entityTitle={
                <EntityTitle
                    media={<EntityIcon type={iconType} shorthandStyle={{ wrapperSize: 36, iconSize: 22, borderRadius: 6 }} />}
                    primaryText={title}
                    className={styles.cardTitle}
                />
            }
            content={{ className: styles.cardContent }}
        >
            <Subtitle1>{entityCount.toString()}</Subtitle1>
        </CopilotEntityCard>
    );
};
