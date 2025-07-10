import { ScrollDownButton } from '@fluentui-copilot/react-copilot-chat';
import { Button, makeStyles, mergeClasses, Popover, PopoverSurface, PopoverTrigger, Text, tokens } from '@fluentui/react-components';
import { Lightbulb16Regular, RecordStopFilled, SendFilled } from '@fluentui/react-icons';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import { TextField } from '@fluentui/react/lib/TextField';
import { memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { showAgentMode } from '../../Common/Constants/FeatureFlags';
import { useFeatureFlag } from '../../Common/Hooks/useFeatureFlag';
import { ActivitiesResources, PromptResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IChatBoxFooterV2Props } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { chatInputTextStyles, sendButtonStyles, useChatInputStyles } from '../Styles/Activities.styles';
import AgentModeSelector from './AgentModeSelector';
import KnowledgeGraphBuildStatus from './KnowledgeGraphBuildStatus';

const useDownButtonStyles = makeStyles({
    root: {
        opacity: '1',
        transition: 'opacity 0.3s ease',
        pointerEvents: 'auto',
        position: 'absolute',
        right: '50%',
        bottom: '110%',
    },
    hidden: {
        opacity: '0',
        pointerEvents: 'none',
    },
});

const DownButton = ({ downButtonState, onClick }: { downButtonState: { visible: boolean; flash: boolean }; onClick: () => void }) => {
    const { root, hidden } = useDownButtonStyles();
    const buttonStyles = mergeClasses(root, downButtonState.visible ? undefined : hidden);

    return <ScrollDownButton onClick={onClick} className={buttonStyles} isGenerating={downButtonState.flash} />;
};

const ChatBoxFooterV2 = ({
    sendMessage,
    disableInput,
    onClickDownButton,
    downButtonState,
    prompts,
    messagePromptsUsed,
    cancelStreaming,
    isTyping,
    isCancellingStreaming,
    threadId,
}: IChatBoxFooterV2Props) => {
    const intl = useIntl();

    const [input, setInput] = useState<string>();
    const [historyIndex, setHistoryIndex] = useState<number>(-1);
    const [originalInput, setOriginalInput] = useState<string>('');

    const showAgentModeSelector = useFeatureFlag(showAgentMode);

    const { root, footer, subFooter, chatStatement, sectionHeader, promptItem, lightbulbIcon, sectionDivider, popoverSurface } =
        useChatInputStyles();
    const [open, setOpen] = useState(false);

    const { isConnected } = useContext(StreamingContext);

    const disableInputInteraction = useMemo(() => {
        return disableInput || !isConnected || isCancellingStreaming;
    }, [disableInput, isConnected, isCancellingStreaming]);

    const SendOrCancelButtonIcon = () => {
        const color = disableInputInteraction ? 'undefined' : tokens.colorBrandForeground1;
        return isTyping ? <RecordStopFilled style={{ color }} /> : <SendFilled style={{ color }} />;
    };

    const chatInputHandleSendClick = useCallback(() => {
        const messageToSend = input?.trim() ?? '';

        if (messageToSend) {
            setInput('');
            setHistoryIndex(-1);
            setOriginalInput('');
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
                <DownButton downButtonState={downButtonState} onClick={onClickDownButton} />
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
                    onChange={(_, value?: string) => {
                        setInput(value);
                        if (historyIndex >= 0) {
                            setHistoryIndex(-1);
                            setOriginalInput('');
                        }
                    }}
                    onKeyDown={event => {
                        if (event.key.toLowerCase() === 'g') {
                            // Stop the event from propagating to the global shortcuts
                            event.stopPropagation();
                        } else if (event.key.toLowerCase() === 'enter' && !event.shiftKey && !disableInputInteraction) {
                            chatInputHandleSendClick();
                            event.preventDefault();
                            event.stopPropagation();
                        } else if (event.key === 'ArrowUp' && messagePromptsUsed.length > 0) {
                            event.preventDefault();
                            event.stopPropagation();

                            if (historyIndex === -1) {
                                setOriginalInput(input || '');
                                setHistoryIndex(0);
                                setInput(messagePromptsUsed[0]);
                            } else if (historyIndex < messagePromptsUsed.length - 1) {
                                const newIndex = historyIndex + 1;
                                setHistoryIndex(newIndex);
                                setInput(messagePromptsUsed[newIndex]);
                            }
                        } else if (event.key === 'ArrowDown' && historyIndex >= 0) {
                            event.preventDefault();
                            event.stopPropagation();

                            if (historyIndex > 0) {
                                const newIndex = historyIndex - 1;
                                setHistoryIndex(newIndex);
                                setInput(messagePromptsUsed[newIndex]);
                            } else {
                                setHistoryIndex(-1);
                                setInput(originalInput);
                                setOriginalInput('');
                            }
                        }
                    }}
                />
                <div className={footer}>
                    <div className={subFooter}>
                        {showAgentModeSelector && threadId && <AgentModeSelector threadId={threadId} disabled={isTyping} />}
                        <Popover positioning={'after-top'} open={open} onOpenChange={(_e, data) => setOpen(data.open)}>
                            <PopoverTrigger>
                                <Button
                                    style={{ fontSize: '13px', padding: '2px 4px', paddingRight: '8px' }}
                                    appearance="outline"
                                    icon={<Lightbulb16Regular />}
                                    onClick={() => setOpen(!open)}
                                    disabled={disableInputInteraction}
                                >
                                    {intl.formatMessage(PromptResources.promptLibrary)}
                                </Button>
                            </PopoverTrigger>
                            <PopoverSurface className={popoverSurface}>
                                {messagePromptsUsed.length > 0 && (
                                    <>
                                        <PromptSection
                                            title={intl.formatMessage(PromptResources.myRecentPrompts)}
                                            prompts={messagePromptsUsed}
                                        />
                                        <div className={sectionDivider} />
                                    </>
                                )}
                                <PromptSection title={intl.formatMessage(PromptResources.suggestedPrompts)} prompts={prompts} />
                            </PopoverSurface>
                        </Popover>
                    </div>
                    <Button
                        icon={<SendOrCancelButtonIcon />}
                        disabled={disableInputInteraction}
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
