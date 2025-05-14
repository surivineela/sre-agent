import { Button, Text, tokens } from '@fluentui/react-components';
import { SendFilled } from '@fluentui/react-icons';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import { TextField } from '@fluentui/react/lib/TextField';
import { memo, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IChatBoxFooterProps } from '../Contracts/Activities';
import { chatInputTextStyles, sendButtonStyles, useChatInputStyles } from '../Styles/Activities.styles';
import KnowledgeGraphBuildStatus from './KnowledgeGraphBuildStatus';
import NewMessageButton from './NewMessageButton';

const ChatBoxFooter = ({ sendMessage, disableInput, isNewMessageButtonVisible, onClickNewMessageButton }: IChatBoxFooterProps) => {
    const intl = useIntl();

    const [input, setInput] = useState<string>();

    const { root, footer, chatStatement } = useChatInputStyles();

    const chatInputHandleSendClick = useCallback(() => {
        const messageToSend = input?.trim() ?? '';

        if (messageToSend) {
            setInput('');
            sendMessage(messageToSend);
        }
    }, [input, sendMessage]);

    return (
        <div className={root}>
            <KnowledgeGraphBuildStatus />
            <div className={mergeStyles(chatInputTextStyles.textFieldContainer as IStyle)}>
                <NewMessageButton isVisible={isNewMessageButtonVisible} onClick={onClickNewMessageButton} />
                <TextField
                    placeholder={intl.formatMessage(ActivitiesResources.chatInputPlaceholder)}
                    multiline={true}
                    autoAdjustHeight={true}
                    borderless={true}
                    resizable={false}
                    type="text"
                    autoFocus={true}
                    autoComplete="off"
                    styles={chatInputTextStyles.textField}
                    rows={1}
                    value={input}
                    onChange={(_, value?: string) => setInput(value)}
                    onKeyDown={event => {
                        if (event.key.toLowerCase() === 'g') {
                            // Stop the event from propagating to the global shortcuts
                            event.stopPropagation();
                        } else if (event.key.toLowerCase() === 'enter' && !event.shiftKey && !disableInput) {
                            chatInputHandleSendClick();
                            event.preventDefault();
                            event.stopPropagation();
                        }
                    }}
                />
                <div className={footer}>
                    <Button
                        icon={<SendFilled style={{ color: disableInput ? undefined : tokens.colorBrandForeground1 }} />}
                        disabled={disableInput}
                        onClick={chatInputHandleSendClick}
                        shape="square"
                        appearance="subtle"
                        style={sendButtonStyles}
                    />
                </div>
            </div>

            <Text block size={200} align="center" className={mergeStyles(chatStatement)}>
                {intl.formatMessage(SreAgentResources.chatAiContentAndPrivacyMessageStatement)}
            </Text>
        </div>
    );
};

export default memo(ChatBoxFooter);
