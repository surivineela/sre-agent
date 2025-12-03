import { Button, Text } from '@fluentui/react-components';
import * as React from 'react';
import { useIntl } from 'react-intl';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { KnowledgeBaseResources } from '../../../Strings/SREAgentResources';
import { useEmptyStateStyles } from '../Styles/DataKnowledgeSpace.styles';

interface EmptyStateProps {
    variant: 'noItems' | 'noSearchResults';
    onPrimaryAction: () => void;
    isActionDisabled?: boolean;
}

export const EmptyState: React.FC<EmptyStateProps> = ({ variant, onPrimaryAction, isActionDisabled = false }) => {
    const intl = useIntl();
    const styles = useEmptyStateStyles();

    if (variant === 'noSearchResults') {
        return (
            <div className={styles.noSearchResultsContainer}>
                <div className={styles.emptyStateContent}>
                    <div className={styles.textContainer}>
                        <Text className={styles.secondaryTitle}>{intl.formatMessage(KnowledgeBaseResources.noSearchResults)}</Text>
                        <Text className={styles.searchDescription}>
                            {intl.formatMessage(KnowledgeBaseResources.noSearchResultsDescription)}
                        </Text>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.noItemsContainer}>
            <div className={styles.emptyStateContent}>
                <img
                    src={resolveResourceIcon('KnowledgeBase')}
                    style={{ height: 150, width: 150 }}
                    alt={intl.formatMessage(KnowledgeBaseResources.knowledgeBase)}
                />
                <div className={styles.textContainer}>
                    <Text className={styles.primaryTitle}>{intl.formatMessage(KnowledgeBaseResources.fileUploadTitle)}</Text>
                    <Text className={styles.description}>{intl.formatMessage(KnowledgeBaseResources.fileUploadDescription)}</Text>
                </div>
                <Button appearance="primary" onClick={onPrimaryAction} disabled={isActionDisabled}>
                    {intl.formatMessage(KnowledgeBaseResources.addFileAction)}
                </Button>
            </div>
        </div>
    );
};

export default EmptyState;
