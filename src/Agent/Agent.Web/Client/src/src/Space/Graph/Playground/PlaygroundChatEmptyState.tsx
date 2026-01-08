import { Body1, Button, Caption1Strong, makeStyles, shorthands, Subtitle2, tokens } from '@fluentui/react-components';
import { ArrowRight16Regular, Sparkle16Regular } from '@fluentui/react-icons';
import { FC, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { PlaygroundResources } from '../../../Strings/SREAgentResources';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'flex-start',
        gap: tokens.spacingVerticalM,
        height: '100%',
        ...shorthands.padding(tokens.spacingVerticalXL, tokens.spacingHorizontalXXL),
    },
    hero: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        maxWidth: '520px',
    },
    heroBadge: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        borderRadius: tokens.borderRadiusCircular,
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground2,
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        ...shorthands.padding(tokens.spacingVerticalXXS, tokens.spacingHorizontalS),
    },
    questList: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: tokens.spacingHorizontalM,
        width: '100%',
    },
    questCard: {
        borderRadius: tokens.borderRadiusLarge,
        backgroundColor: tokens.colorNeutralBackground2,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
        height: '100%',
    },
    questTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    questDescription: {
        color: tokens.colorNeutralForeground3,
        flex: 1,
    },
    questButton: {
        alignSelf: 'flex-start',
    },
    syncText: {
        color: tokens.colorStatusSuccessForeground1,
        fontSize: tokens.fontSizeBase200,
    },
});

interface PlaygroundChatEmptyStateProps {
    onSend: (prompt: string) => Promise<void>;
    agentName?: string;
    isApplyingChanges?: boolean;
}

export const PlaygroundChatEmptyState: FC<PlaygroundChatEmptyStateProps> = ({ onSend, agentName, isApplyingChanges }) => {
    const styles = useStyles();
    const intl = useIntl();
    const resolvedAgentName = agentName ?? intl.formatMessage(PlaygroundResources.playgroundChatAgentFallback);

    const quests = useMemo(
        () => [
            {
                key: 'warmup',
                title: intl.formatMessage(PlaygroundResources.playgroundChatPromptWarmupTitle),
                description: intl.formatMessage(PlaygroundResources.playgroundChatPromptWarmupDescription),
                prompt: intl.formatMessage(PlaygroundResources.playgroundChatPromptWarmupMessage, {
                    name: resolvedAgentName,
                }),
            },
            {
                key: 'stress',
                title: intl.formatMessage(PlaygroundResources.playgroundChatPromptStressTitle),
                description: intl.formatMessage(PlaygroundResources.playgroundChatPromptStressDescription),
                prompt: intl.formatMessage(PlaygroundResources.playgroundChatPromptStressMessage, {
                    name: resolvedAgentName,
                }),
            },
            {
                key: 'audit',
                title: intl.formatMessage(PlaygroundResources.playgroundChatPromptAuditTitle),
                description: intl.formatMessage(PlaygroundResources.playgroundChatPromptAuditDescription),
                prompt: intl.formatMessage(PlaygroundResources.playgroundChatPromptAuditMessage, {
                    name: resolvedAgentName,
                }),
            },
        ],
        [intl, resolvedAgentName]
    );

    const handleSend = (prompt: string) => {
        void onSend(prompt);
    };

    return (
        <div className={styles.root}>
            <div className={styles.hero}>
                <div className={styles.heroBadge}>
                    <Sparkle16Regular aria-hidden="true" />
                    {intl.formatMessage(PlaygroundResources.playgroundChatEmptyBadge)}
                </div>
                <Caption1Strong>{intl.formatMessage(PlaygroundResources.playgroundChatEmptyTitle)}</Caption1Strong>
                <Subtitle2>{intl.formatMessage(PlaygroundResources.playgroundChatEmptySubtitle, { name: resolvedAgentName })}</Subtitle2>
                <Body1>{intl.formatMessage(PlaygroundResources.playgroundChatEmptyDescription)}</Body1>
                {isApplyingChanges && (
                    <Body1 className={styles.syncText}>{intl.formatMessage(PlaygroundResources.playgroundChatEmptySyncing)}</Body1>
                )}
            </div>
            <div className={styles.questList}>
                {quests.map(quest => (
                    <div key={quest.key} className={styles.questCard}>
                        <div className={styles.questTitle}>
                            <ArrowRight16Regular aria-hidden="true" />
                            {quest.title}
                        </div>
                        <Body1 className={styles.questDescription}>{quest.description}</Body1>
                        <Button appearance="secondary" size="small" className={styles.questButton} onClick={() => handleSend(quest.prompt)}>
                            {intl.formatMessage(PlaygroundResources.playgroundChatSendPromptLabel)}
                        </Button>
                    </div>
                ))}
            </div>
        </div>
    );
};

export default PlaygroundChatEmptyState;
