import { Label, Text, mergeClasses } from '@fluentui/react-components';
import { Alert24Regular, Bot24Regular, PlugConnected24Regular, Wrench24Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { IntlShape } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { useCreationDialogStyles } from '../styles';
import { EntityType, TriggerCardConfig, TriggerMode } from '../types';

interface TypeSelectionStepProps {
    selectedType?: EntityType;
    onSelectAgent: () => void;
    onSelectTool: () => void;
    onSelectConnector: () => void;
    onSelectTrigger: (mode?: TriggerMode) => void;
    intl: IntlShape;
    triggerConfig?: TriggerCardConfig;
    onTriggerNavigate?: (destination: 'incidentManagement' | 'scheduledTasks') => void;
}

export const TypeSelectionStep: FC<TypeSelectionStepProps> = ({
    selectedType,
    onSelectAgent,
    onSelectTool,
    onSelectConnector,
    onSelectTrigger,
    intl,
}) => {
    const styles = useCreationDialogStyles();

    return (
        <div>
            <Label size="large">{intl.formatMessage(ExtendedAgentsGraphResources.whatToCreate)}</Label>
            <div className={styles.typeSelector}>
                <button
                    type="button"
                    className={mergeClasses(styles.typeCard, selectedType === 'agent' ? styles.typeCardSelected : undefined)}
                    onClick={onSelectAgent}
                >
                    <span className={styles.typeIcon} aria-hidden>
                        <Bot24Regular />
                    </span>
                    <Text className={styles.typeTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.agent)}</Text>
                    <Text className={styles.typeDescription}>{intl.formatMessage(ExtendedAgentsGraphResources.agentDescription)}</Text>
                </button>

                <button
                    type="button"
                    className={mergeClasses(styles.typeCard, selectedType === 'tool' ? styles.typeCardSelected : undefined)}
                    onClick={onSelectTool}
                >
                    <span className={styles.typeIcon} aria-hidden>
                        <Wrench24Regular />
                    </span>
                    <Text className={styles.typeTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.tool)}</Text>
                    <Text className={styles.typeDescription}>{intl.formatMessage(ExtendedAgentsGraphResources.toolDescription)}</Text>
                </button>

                <button
                    type="button"
                    className={mergeClasses(styles.typeCard, selectedType === 'connector' ? styles.typeCardSelected : undefined)}
                    onClick={onSelectConnector}
                >
                    <span className={styles.typeIcon} aria-hidden>
                        <PlugConnected24Regular />
                    </span>
                    <Text className={styles.typeTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.connector)}</Text>
                    <Text className={styles.typeDescription}>{intl.formatMessage(ExtendedAgentsGraphResources.connectorDescription)}</Text>
                </button>

                <button
                    type="button"
                    className={mergeClasses(styles.typeCard, selectedType === 'trigger' ? styles.typeCardSelected : undefined)}
                    onClick={() => onSelectTrigger('incident')}
                >
                    <span className={styles.typeIcon} aria-hidden>
                        <Alert24Regular />
                    </span>
                    <Text className={styles.typeTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.trigger)}</Text>
                    <Text className={styles.typeDescription}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerDescriptionFallback)}
                    </Text>
                </button>
            </div>
        </div>
    );
};
