import { Skeleton, SkeletonItem, Text } from '@fluentui/react-components';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { useAgentPlaygroundStyles } from './AgentPlayground.Styles';
import { SuggestionsAreaProps } from './Contracts';

export const SuggestionsArea: FC<SuggestionsAreaProps> = ({ isLoading, suggestions, warnings, improvedPrompt, handoffDescription }) => {
    const intl = useIntl();
    const styles = useAgentPlaygroundStyles();

    return (
        <div className={styles.suggestionsContainer}>
            <SuggestionSection
                title={intl.formatMessage(ExtendedAgentsGraphResources.suggestions)}
                isLoading={isLoading}
                content={suggestions}
            />
            <SuggestionSection title={intl.formatMessage(ExtendedAgentsGraphResources.warnings)} isLoading={isLoading} content={warnings} />
            <SuggestionSection
                title={intl.formatMessage(ExtendedAgentsGraphResources.improvedInstructions)}
                isLoading={isLoading}
                content={improvedPrompt}
            />
            <SuggestionSection
                title={intl.formatMessage(ExtendedAgentsGraphResources.handoffInstructions)}
                isLoading={isLoading}
                content={handoffDescription}
            />
        </div>
    );
};

interface SuggestionSectionProps {
    title: string;
    isLoading: boolean;
    content: string | string[] | undefined;
}

const SuggestionSection: React.FC<SuggestionSectionProps> = ({ title, isLoading, content }) => {
    const styles = useAgentPlaygroundStyles();
    return (
        <div className={styles.suggestionSection}>
            <Text weight="semibold">{title}</Text>
            {isLoading ? (
                <Skeleton>
                    <SkeletonItem />
                </Skeleton>
            ) : !content ? (
                <Text size={200} className={styles.suggestionText}>
                    {'-'}
                </Text>
            ) : Array.isArray(content) ? (
                <ul className={styles.suggestionList}>
                    {content?.map((suggestion, index) => (
                        <div key={index} className={styles.suggestionListItem}>
                            <Text size={200} className={styles.suggestionText}>
                                {suggestion}
                            </Text>
                        </div>
                    ))}
                </ul>
            ) : (
                <Text size={200} className={styles.suggestionText}>
                    {content}
                </Text>
            )}
        </div>
    );
};
