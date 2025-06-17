import { Card, Image, makeStyles, Text } from '@fluentui/react-components';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useChatSuggestionStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '40px',
        justifyContent: 'center',
        alignItems: 'center',
        flex: '1',
    },
    brandContainer: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'center',
        alignItems: 'center',
        gap: '8px',
    },
    cardContainer: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '20px',
        maxWidth: '1000px',
        justifyContent: 'center',
        alignItems: 'center',
    },
    card: {
        width: '185px',
        height: '72px',
    },
});

interface ChatSuggestionsProps {
    sendMessage: (message: string) => void;
}

export const ChatSuggestions = (props: ChatSuggestionsProps) => {
    const { sendMessage } = props;

    const intl = useIntl();
    const chatSuggestionsStyles = useChatSuggestionStyles();

    const chatSuggestionStrings = useMemo<string[]>(
        () => [
            'What can you help me with?',
            'Can you audit best practices for my resource?',
            "Why isn't my application working?",
            "Can you analyze my resource's availability over the last 24 hours?",
        ],
        []
    );

    return (
        <div className={chatSuggestionsStyles.root}>
            <div className={chatSuggestionsStyles.brandContainer}>
                <Image src="./SreAgent.svg" width={32} height={32} alt={intl.formatMessage(SreAgentResources.sreAgent)} />
                <Text size={500} weight="semibold">
                    {intl.formatMessage(SreAgentResources.sreAgent)}
                </Text>
            </div>

            <div className={chatSuggestionsStyles.cardContainer}>
                {chatSuggestionStrings.map(suggestion => (
                    <Card key={suggestion} onClick={() => sendMessage(suggestion)} className={chatSuggestionsStyles.card}>
                        <Text size={200}>{suggestion}</Text>
                    </Card>
                ))}
            </div>
        </div>
    );
};
