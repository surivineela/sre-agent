import { Button, makeStyles, mergeClasses, Popover, PopoverSurface, PopoverTrigger, Text, tokens } from '@fluentui/react-components';
import { ArrowCircleDownFilled, Lightbulb16Regular, RecordStopFilled, SendFilled } from '@fluentui/react-icons';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import { TextField } from '@fluentui/react/lib/TextField';
import { memo, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { ActivitiesResources, PromptResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IChatBoxFooterV2Props } from '../Contracts/Activities';
import { chatInputTextStyles, sendButtonStyles, useChatInputStyles } from '../Styles/Activities.styles';
import KnowledgeGraphBuildStatus from './KnowledgeGraphBuildStatus';

const useDownButtonStyles = makeStyles({
    root: {
        opacity: '1',
        transition: 'opacity 0.3s ease',
        pointerEvents: 'auto',
        position: 'absolute',
        right: '50%',
        bottom: '100%',
    },
    hidden: {
        opacity: '0',
        pointerEvents: 'none',
    },
});

const DownButton = ({ isVisible, onClick }: { isVisible?: boolean; onClick: () => void }) => {
    const { root, hidden } = useDownButtonStyles();
    const buttonStyles = mergeClasses(root, isVisible ? undefined : hidden);

    return (
        <Button appearance={'transparent'} icon={<ArrowCircleDownFilled />} onClick={onClick} shape={'circular'} className={buttonStyles} />
    );
};

const ChatBoxFooterV2 = ({
    sendMessage,
    disableInput,
    onClickDownButton,
    isDownButtonVisible,
    prompts,
    messagePromptsUsed,
    cancelStreaming,
    isTyping,
}: IChatBoxFooterV2Props) => {
    const intl = useIntl();

    const [input, setInput] = useState<string>();

    const { root, footer, chatStatement, sectionHeader, promptItem, lightbulbIcon, sectionDivider, popoverSurface } = useChatInputStyles();
    const [open, setOpen] = useState(false);

    const chatInputHandleSendClick = useCallback(() => {
        const messageToSend = input?.trim() ?? '';

        if (messageToSend) {
            setInput('');
            sendMessage(messageToSend);
        }
    }, [input, sendMessage]);

    const handlePromptClick = useCallback(
        (prompt: string) => {
            sendMessage(prompt);
            setOpen(false);
        },
        [sendMessage]
    );

    const PromptSection = useCallback(
        ({ title, prompts }: { title: string; prompts: string[] }) => (
            <div>
                <div className={sectionHeader}>{title}</div>
                {prompts.map((prompt, i) => (
                    <div key={i} className={promptItem} onClick={() => handlePromptClick(prompt)}>
                        <Lightbulb16Regular className={lightbulbIcon} />
                        {prompt}
                    </div>
                ))}
            </div>
        ),
        [handlePromptClick, lightbulbIcon, promptItem, sectionHeader]
    );

    return (
        <div className={root}>
            <KnowledgeGraphBuildStatus />
            <div className={mergeStyles(chatInputTextStyles.textFieldContainer as IStyle)}>
                {<DownButton isVisible={isDownButtonVisible} onClick={onClickDownButton} />}
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
                    <Popover positioning={'after-top'} open={open} onOpenChange={(_e, data) => setOpen(data.open)}>
                        <PopoverTrigger>
                            <Button
                                style={{ fontSize: '13px', padding: '2px 4px', paddingRight: '8px' }}
                                appearance="outline"
                                icon={<Lightbulb16Regular />}
                                onClick={() => setOpen(!open)}
                            >
                                {intl.formatMessage(PromptResources.promptLibrary)}
                            </Button>
                        </PopoverTrigger>
                        <PopoverSurface className={popoverSurface}>
                            {messagePromptsUsed.length > 0 && (
                                <>
                                    <PromptSection title={intl.formatMessage(PromptResources.latestPrompts)} prompts={messagePromptsUsed} />
                                    <div className={sectionDivider} />
                                </>
                            )}
                            <PromptSection title={intl.formatMessage(PromptResources.popularPrompts)} prompts={prompts} />
                        </PopoverSurface>
                    </Popover>
                    <Button
                        icon={
                            isTyping ? (
                                <RecordStopFilled style={{ color: tokens.colorBrandForeground1 }} />
                            ) : (
                                <SendFilled style={{ color: disableInput ? 'undefined' : tokens.colorBrandForeground1 }} />
                            )
                        }
                        disabled={disableInput}
                        onClick={() => {
                            if (isTyping) {
                                cancelStreaming();
                            } else {
                                chatInputHandleSendClick();
                            }
                        }}
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

export default memo(ChatBoxFooterV2);
