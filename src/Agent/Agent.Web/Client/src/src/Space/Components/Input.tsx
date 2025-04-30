import { Button, tokens } from '@fluentui/react-components';
import { SendRegular } from '@fluentui/react-icons';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import { TextField } from '@fluentui/react/lib/TextField';
import { memo, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { ActivitiesResources } from '../../Strings/SREAgentResources';
import { IInputProps } from '../Contracts/Activities';
import { useChatInputStyles, useChatInputTextStyles } from '../Styles/Activities.styles';

const Input = ({ sendMessage, disableInput }: IInputProps) => {
    const intl = useIntl();

    const [input, setInput] = useState<string>();

    const { root, footer } = useChatInputStyles();
    const { textFieldContainer, textField } = useChatInputTextStyles();

    const chatInputHandleSendClick = useCallback(() => {
        const messageToSend = input?.trim() ?? '';

        if (messageToSend) {
            setInput('');
            sendMessage(messageToSend);
        }
    }, [input, sendMessage]);

    return (
        <div className={root}>
            <div className={mergeStyles(textFieldContainer as IStyle)}>
                <TextField
                    placeholder={intl.formatMessage(ActivitiesResources.chatInputPlaceholder)}
                    multiline={true}
                    autoAdjustHeight={true}
                    borderless={true}
                    resizable={false}
                    type="text"
                    autoFocus={true}
                    autoComplete="off"
                    styles={textField}
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
                        icon={<SendRegular style={{ color: tokens.colorBrandForeground1 }} />}
                        disabled={disableInput}
                        onClick={chatInputHandleSendClick}
                        shape="circular"
                        appearance="subtle"
                    />
                </div>
            </div>
        </div>
    );
};

export default memo(Input);
