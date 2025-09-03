import { ScrollDownButton } from '@fluentui-copilot/react-copilot-chat';
import {
    Button,
    makeStyles,
    Menu,
    MenuDivider,
    MenuGroup,
    MenuGroupHeader,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    mergeClasses,
    Overflow,
    OverflowItem,
    Text,
    tokens,
    useIsOverflowItemVisible,
    useOverflowMenu,
} from '@fluentui/react-components';
import { Lightbulb16Regular, MoreHorizontal20Filled, RecordStopFilled, SearchSparkle16Filled, SendFilled } from '@fluentui/react-icons';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import { TextField } from '@fluentui/react/lib/TextField';
import { memo, useCallback, useContext, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { ActivitiesResources, AgentTaskResources, PromptResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IChatBoxFooterProps } from '../Contracts/Activities';
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

enum ChatBoxButtonIds {
    DeepInvestigation = 'deep-investigation',
    AgentMode = 'agent-mode',
    PromptLibrary = 'prompt-library',
}

const ChatBoxFooter = ({
    sendMessage,
    isLoading,
    onClickDownButton,
    downButtonState,
    prompts,
    messagePromptsUsed,
    cancelStreaming,
    isTyping,
    isCancellingStreaming,
    threadId,
    showDeepInvestigationButton,
    isDeepInvestigationButtonEnabled,
    isDeepInvestigationTurnedOn,
    onClickDeepInvestigationButton,
}: IChatBoxFooterProps) => {
    const intl = useIntl();

    const [input, setInput] = useState<string>();
    const [historyIndex, setHistoryIndex] = useState<number>(-1);
    const [originalInput, setOriginalInput] = useState<string>('');

    const showAgentModeSelector = useConfigSetting(SettingNames.ShowAgentModeForThread);

    const { root, footer, subFooter, chatStatement } = useChatInputStyles();

    const { isConnected } = useContext(StreamingContext);
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const disableInputInteraction = useMemo(() => {
        return isLoading || !isConnected || isCancellingStreaming;
    }, [isLoading, isConnected, isCancellingStreaming]);

    const SendOrCancelButtonIcon = () => {
        const color = disableInputInteraction ? 'undefined' : tokens.colorBrandForeground1;
        return isTyping ? <RecordStopFilled style={{ color }} /> : <SendFilled style={{ color }} />;
    };

    const chatInputHandleSendClick = useCallback(() => {
        const messageToSend = input?.trim() ?? '';

        if (messageToSend && !disableInputInteraction && !isTyping) {
            setInput('');
            setHistoryIndex(-1);
            setOriginalInput('');
            sendMessage(messageToSend);

            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'sendMessage',
                targetFriendlyName: 'Send message',
                valueObjectName: SpecialControlValue.CustomerSuppliedData,
                valueObjectFriendlyName: SpecialControlValue.CustomerSuppliedData,
            });
        }
    }, [input, sendMessage, disableInputInteraction, isTyping, logAmplitudeControlEvent]);

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
                        } else if (event.key.toLowerCase() === 'enter' && !event.shiftKey) {
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
                    <Overflow>
                        <div className={subFooter}>
                            <DeepInvestigationButton
                                asOverflowItem={true}
                                showDeepInvestigationButton={showDeepInvestigationButton}
                                isDeepInvestigationButtonEnabled={isDeepInvestigationButtonEnabled}
                                isDeepInvestigationTurnedOn={isDeepInvestigationTurnedOn}
                                onClickDeepInvestigationButton={onClickDeepInvestigationButton}
                            />
                            <AgentModeSelectorButton
                                asOverflowItem={true}
                                isTyping={isTyping}
                                showAgentModeSelector={showAgentModeSelector}
                                threadId={threadId}
                            />
                            <PromptLibraryButton
                                asOverflowItem={true}
                                isTyping={isTyping}
                                disableInputInteraction={disableInputInteraction}
                                messagePromptsUsed={messagePromptsUsed}
                                sendMessage={sendMessage}
                                prompts={prompts}
                            />
                            <OverflowMenu
                                isTyping={isTyping}
                                showDeepInvestigationButton={showDeepInvestigationButton}
                                isDeepInvestigationButtonEnabled={isDeepInvestigationButtonEnabled}
                                isDeepInvestigationTurnedOn={isDeepInvestigationTurnedOn}
                                onClickDeepInvestigationButton={onClickDeepInvestigationButton}
                                threadId={threadId}
                                disableInputInteraction={disableInputInteraction}
                                prompts={prompts}
                                messagePromptsUsed={messagePromptsUsed}
                                sendMessage={sendMessage}
                                showAgentModeSelector={showAgentModeSelector}
                            />
                        </div>
                    </Overflow>
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

const DeepInvestigationButton = memo(
    ({
        asOverflowItem,
        showDeepInvestigationButton,
        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        onClickDeepInvestigationButton,
    }: {
        asOverflowItem: boolean;
        showDeepInvestigationButton: boolean;
        isDeepInvestigationButtonEnabled: boolean;
        isDeepInvestigationTurnedOn: boolean;
        onClickDeepInvestigationButton: () => void;
    }) => {
        const isVisible = useIsOverflowItemVisible(ChatBoxButtonIds.DeepInvestigation);

        if (!asOverflowItem && isVisible) {
            return null;
        }

        const ButtonComponent = () => {
            return (
                <Button
                    style={{ fontSize: '13px', padding: '2px 8px 2px 4px', whiteSpace: 'nowrap' }}
                    icon={
                        <SearchSparkle16Filled
                            style={{ color: isDeepInvestigationButtonEnabled ? undefined : tokens.colorNeutralForegroundDisabled }}
                        />
                    }
                    appearance={isDeepInvestigationTurnedOn ? 'primary' : 'secondary'}
                    onClick={onClickDeepInvestigationButton}
                    disabled={!isDeepInvestigationButtonEnabled}
                >
                    <FormattedMessage {...AgentTaskResources.deepInvestigation} />
                </Button>
            );
        };

        return (
            showDeepInvestigationButton &&
            (asOverflowItem ? (
                <OverflowItem id={ChatBoxButtonIds.DeepInvestigation}>
                    <div>
                        <ButtonComponent />
                    </div>
                </OverflowItem>
            ) : (
                <ButtonComponent />
            ))
        );
    }
);

const AgentModeSelectorButton = memo(
    ({
        asOverflowItem,
        showAgentModeSelector,
        threadId,
        isTyping,
    }: {
        asOverflowItem: boolean;
        showAgentModeSelector: boolean;
        threadId?: string | null;
        isTyping: boolean;
    }) => {
        const isVisible = useIsOverflowItemVisible(ChatBoxButtonIds.AgentMode);

        if (!asOverflowItem && isVisible) {
            return null;
        }

        return (
            showAgentModeSelector &&
            threadId && (
                <AgentModeSelector
                    asOverflowItem={asOverflowItem}
                    id={ChatBoxButtonIds.AgentMode}
                    threadId={threadId}
                    disabled={isTyping}
                />
            )
        );
    }
);

const PromptLibraryButton = memo(
    ({
        asOverflowItem,
        isTyping,
        disableInputInteraction,
        messagePromptsUsed,
        sendMessage,
        prompts,
    }: {
        asOverflowItem: boolean;
        isTyping: boolean;
        disableInputInteraction: boolean;
        messagePromptsUsed: string[];
        sendMessage: (message: string) => Promise<void>;
        prompts: string[];
    }) => {
        const { sectionHeader, promptMenuPopover, promptItem, lightbulbIcon } = useChatInputStyles();

        const intl = useIntl();
        const { logAmplitudeControlEvent } = useAzPortalContext();

        const isVisible = useIsOverflowItemVisible(ChatBoxButtonIds.PromptLibrary);

        const handlePromptClick = useCallback(
            (prompt: string, isUserPrompt: boolean) => {
                if (!disableInputInteraction && !isTyping) {
                    sendMessage(prompt);

                    logAmplitudeControlEvent({
                        targetType: 'button',
                        targetAction: 'clicked',
                        targetName: 'promptLibrary',
                        targetFriendlyName: 'Prompt library',
                        valueObjectName: !isUserPrompt ? prompt : SpecialControlValue.CustomerSuppliedData,
                        valueObjectFriendlyName: !isUserPrompt ? prompt : SpecialControlValue.CustomerSuppliedData,
                    });
                }
            },
            [sendMessage, disableInputInteraction, isTyping, logAmplitudeControlEvent]
        );

        const PromptSection = ({
            title,
            prompts,
            isUserRecentPrompts,
        }: {
            title: string;
            prompts: string[];
            isUserRecentPrompts: boolean;
        }) => (
            <MenuGroup>
                <MenuGroupHeader className={sectionHeader}>{title}</MenuGroupHeader>
                {prompts.map((prompt, i) => (
                    <div key={i} className={promptItem} onClick={() => handlePromptClick(prompt, isUserRecentPrompts)}>
                        <Lightbulb16Regular className={lightbulbIcon} />
                        {prompt}
                    </div>
                ))}
            </MenuGroup>
        );

        const ButtonComponent = () => {
            return (
                <Button
                    style={{ fontSize: '13px', padding: '2px 8px 2px 4px', whiteSpace: 'nowrap' }}
                    icon={<Lightbulb16Regular />}
                    disabled={disableInputInteraction || isTyping}
                >
                    <FormattedMessage {...PromptResources.promptLibrary} />
                </Button>
            );
        };

        if (!asOverflowItem && isVisible) {
            return null;
        }

        return (
            <Menu positioning={'after-top'}>
                <MenuTrigger>
                    {asOverflowItem ? (
                        <OverflowItem id={ChatBoxButtonIds.PromptLibrary}>
                            <div>
                                <ButtonComponent />
                            </div>
                        </OverflowItem>
                    ) : (
                        <MenuItem icon={<Lightbulb16Regular />} disabled={disableInputInteraction || isTyping}>
                            <FormattedMessage {...PromptResources.promptLibrary} />
                        </MenuItem>
                    )}
                </MenuTrigger>
                <MenuPopover className={promptMenuPopover}>
                    <MenuList>
                        {messagePromptsUsed.length > 0 && (
                            <PromptSection
                                title={intl.formatMessage(PromptResources.myRecentPrompts)}
                                prompts={messagePromptsUsed}
                                isUserRecentPrompts
                            />
                        )}
                        {messagePromptsUsed.length > 0 && <MenuDivider />}
                        <PromptSection
                            title={intl.formatMessage(PromptResources.suggestedPrompts)}
                            prompts={prompts}
                            isUserRecentPrompts={false}
                        />
                    </MenuList>
                </MenuPopover>
            </Menu>
        );
    }
);

const OverflowMenu = memo(
    ({
        isTyping,
        showDeepInvestigationButton,
        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        onClickDeepInvestigationButton,
        threadId,
        disableInputInteraction,
        prompts,
        messagePromptsUsed,
        sendMessage,
        showAgentModeSelector,
    }: {
        isTyping: boolean;
        showDeepInvestigationButton: boolean;
        isDeepInvestigationButtonEnabled: boolean;
        isDeepInvestigationTurnedOn: boolean;
        onClickDeepInvestigationButton: () => void;
        threadId?: string | null;
        disableInputInteraction: boolean;
        prompts: string[];
        messagePromptsUsed: string[];
        sendMessage: (message: string) => Promise<void>;
        showAgentModeSelector: boolean;
    }) => {
        const { ref, isOverflowing } = useOverflowMenu<HTMLButtonElement>();

        if (!isOverflowing) {
            return null;
        }

        return (
            <Menu>
                <MenuTrigger disableButtonEnhancement>
                    <Button ref={ref} icon={<MoreHorizontal20Filled />} aria-label="More items" appearance="subtle" />
                </MenuTrigger>

                <MenuPopover>
                    <MenuList>
                        <DeepInvestigationButton
                            asOverflowItem={false}
                            showDeepInvestigationButton={showDeepInvestigationButton}
                            isDeepInvestigationButtonEnabled={isDeepInvestigationButtonEnabled}
                            isDeepInvestigationTurnedOn={isDeepInvestigationTurnedOn}
                            onClickDeepInvestigationButton={onClickDeepInvestigationButton}
                        />
                        <AgentModeSelectorButton
                            asOverflowItem={false}
                            isTyping={isTyping}
                            showAgentModeSelector={showAgentModeSelector}
                            threadId={threadId}
                        />
                        <PromptLibraryButton
                            asOverflowItem={false}
                            isTyping={isTyping}
                            disableInputInteraction={disableInputInteraction}
                            messagePromptsUsed={messagePromptsUsed}
                            sendMessage={sendMessage}
                            prompts={prompts}
                        />
                    </MenuList>
                </MenuPopover>
            </Menu>
        );
    }
);

export default memo(ChatBoxFooter);
