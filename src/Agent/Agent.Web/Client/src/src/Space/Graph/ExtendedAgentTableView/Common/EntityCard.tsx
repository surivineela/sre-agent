import { Card, Text, mergeClasses } from '@fluentui/react-components';
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
        <Card className={mergeClasses(styles.card, styles.clickableCard)} onClick={() => handleCardClick(type)}>
            <div className={styles.cardHeader}>
                <div className={styles.cardTitleSection}>
                    <EntityIcon type={iconType} shorthandStyle={{ wrapperSize: 36, iconSize: 22, borderRadius: 6 }} />
                    <Text className={styles.cardTitle}>{title}</Text>
                </div>
                <Text className={styles.cardCount}>{entityCount}</Text>
            </div>
        </Card>
    );
};
