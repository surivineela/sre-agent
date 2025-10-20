import { Button, Text } from '@fluentui/react-components';
import * as React from 'react';
import { useIntl } from 'react-intl';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { DataConnectorsResources, KnowledgeBaseResources } from '../../../Strings/SREAgentResources';
import { useEmptyStateStyles } from '../Styles/DataKnowledgeSpace.styles';

interface EmptyStateProps {
    type: 'dataConnectors' | 'knowledgeBase';
    variant: 'noItems' | 'noSearchResults';
    onPrimaryAction: () => void;
    isActionDisabled?: boolean;
}

interface EmptyStateConfig {
    iconName: string;
    iconSize: { height: number; width: number };
    getActionText: (intl: ReturnType<typeof useIntl>) => string;
    getAltText: (intl: ReturnType<typeof useIntl>) => string;
}

const getEmptyStateConfig = (type: EmptyStateProps['type']): EmptyStateConfig => {
    switch (type) {
        case 'dataConnectors':
            return {
                iconName: 'DataConnectors',
                iconSize: { height: 180, width: 180 },
                getActionText: intl => intl.formatMessage(DataConnectorsResources.addDataConnector),
                getAltText: intl => intl.formatMessage(DataConnectorsResources.dataConnectors),
            };
        case 'knowledgeBase':
            return {
                iconName: 'KnowledgeBase',
                iconSize: { height: 150, width: 150 },
                getActionText: intl => intl.formatMessage(KnowledgeBaseResources.addFileAction),
                getAltText: intl => intl.formatMessage(KnowledgeBaseResources.knowledgeBase),
            };
    }
};

export const EmptyState: React.FC<EmptyStateProps> = ({ type, variant, onPrimaryAction, isActionDisabled = false }) => {
    const intl = useIntl();
    const styles = useEmptyStateStyles();
    const config = getEmptyStateConfig(type);
    const resources = type === 'dataConnectors' ? DataConnectorsResources : KnowledgeBaseResources;

    if (variant === 'noSearchResults') {
        return (
            <div className={styles.noSearchResultsContainer}>
                <div className={styles.emptyStateContent}>
                    <div className={styles.textContainer}>
                        <Text className={styles.secondaryTitle}>{intl.formatMessage(resources.noSearchResults)}</Text>
                        <Text className={styles.searchDescription}>{intl.formatMessage(resources.noSearchResultsDescription)}</Text>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.noItemsContainer}>
            <div className={styles.emptyStateContent}>
                <img src={resolveResourceIcon(config.iconName)} style={config.iconSize} alt={config.getAltText(intl)} />
                <div className={styles.textContainer}>
                    <Text className={styles.primaryTitle}>{intl.formatMessage(DataConnectorsResources.emptyStateTitle)}</Text>
                    <Text className={styles.description}>{intl.formatMessage(DataConnectorsResources.emptyStateDescription)}</Text>
                </div>
                <Button appearance="primary" onClick={onPrimaryAction} disabled={isActionDisabled}>
                    {config.getActionText(intl)}
                </Button>
            </div>
        </div>
    );
};

export default EmptyState;
