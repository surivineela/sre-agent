import { Skeleton, SkeletonItem, Text, ToolbarButton } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { useAgentCreateDialogStyles } from './AgentCreateDialog.Styles';
import { SuggestionsPanelProps } from './Contracts';

export const SuggestionsPanel: FC<SuggestionsPanelProps> = ({
    close,
    isLoading,
    suggestions,
    warnings,
    improvedPrompt,
    handoffDescription,
}) => {
    const intl = useIntl();
    const styles = useAgentCreateDialogStyles();

    return (
        <div className={styles.dialogContentWrapper}>
            <div className={styles.toolsPickerTitleWrapper}>
                <Text size={400} weight="semibold">
                    {intl.formatMessage(ExtendedAgentsGraphResources.suggestedImprovements)}
                </Text>
                <ToolbarButton appearance="transparent" icon={<Dismiss24Regular />} onClick={close}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.closePanel)}
                </ToolbarButton>
            </div>
            <div className={styles.suggestionsContainer}>
                <SuggestionSection
                    title={intl.formatMessage(ExtendedAgentsGraphResources.suggestions)}
                    isLoading={isLoading}
                    content={suggestions}
                />
                <SuggestionSection
                    title={intl.formatMessage(ExtendedAgentsGraphResources.warnings)}
                    isLoading={isLoading}
                    content={warnings}
                />
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
        </div>
    );
};

interface SuggestionSectionProps {
    title: string;
    isLoading: boolean;
    content: string | string[] | undefined;
}

const SuggestionSection: React.FC<SuggestionSectionProps> = ({ title, isLoading, content }) => {
    const styles = useAgentCreateDialogStyles();
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
