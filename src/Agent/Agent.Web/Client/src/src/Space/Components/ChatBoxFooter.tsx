import { ScrollDownButton } from '@fluentui-copilot/react-copilot-chat';
import {
    Button,
    Dialog,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Input,
    makeStyles,
    Menu,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    mergeClasses,
    Overflow,
    OverflowItem,
    Text,
    tokens,
    Tooltip,
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
import { ChatSuggestions } from '../Activities/ChatSuggestions';
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

        return (
            showDeepInvestigationButton && (
                <Tooltip content={<FormattedMessage {...AgentTaskResources.deepInvestigationTooltip} />} relationship="label">
                    {asOverflowItem ? (
                        <OverflowItem id={ChatBoxButtonIds.DeepInvestigation}>
                            <div>
                                <Button
                                    style={{ fontSize: '13px', padding: '2px 8px 2px 4px', whiteSpace: 'nowrap' }}
                                    icon={
                                        <SearchSparkle16Filled
                                            style={{
                                                color: isDeepInvestigationButtonEnabled ? undefined : tokens.colorNeutralForegroundDisabled,
                                            }}
                                        />
                                    }
                                    appearance={isDeepInvestigationTurnedOn ? 'primary' : undefined}
                                    onClick={onClickDeepInvestigationButton}
                                    disabled={!isDeepInvestigationButtonEnabled}
                                >
                                    <FormattedMessage {...AgentTaskResources.deepInvestigation} />
                                </Button>
                            </div>
                        </OverflowItem>
                    ) : (
                        <MenuItem
                            icon={<SearchSparkle16Filled />}
                            style={{
                                color:
                                    isDeepInvestigationTurnedOn && isDeepInvestigationButtonEnabled
                                        ? tokens.colorBrandBackground
                                        : undefined,
                            }}
                            onClick={onClickDeepInvestigationButton}
                            disabled={!isDeepInvestigationButtonEnabled}
                        >
                            <FormattedMessage {...AgentTaskResources.deepInvestigation} />
                        </MenuItem>
                    )}
                </Tooltip>
            )
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
        // Keep these in the signature for compatibility but alias to avoid unused warnings
        messagePromptsUsed: _messagePromptsUsed,
        sendMessage,
        prompts: _prompts,
    }: {
        asOverflowItem: boolean;
        isTyping: boolean;
        disableInputInteraction: boolean;
        messagePromptsUsed: string[];
        sendMessage: (message: string) => Promise<void>;
        prompts: string[];
    }) => {
        const { logAmplitudeControlEvent } = useAzPortalContext();
        const isVisible = useIsOverflowItemVisible(ChatBoxButtonIds.PromptLibrary);
        const [open, setOpen] = useState(false);
        const [query, setQuery] = useState('');
        const intl = useIntl();

        const categories = useMemo<string[]>(
            () => ['Get started', 'Azure App Service', 'Azure Container App', 'Azure Kubernetes Service', 'Azure API Management'],
            []
        );

        const aboutMeQs = useMemo<string[]>(
            () => [
                'How do I get started with SRE Agent?',
                'What can you help me with as an SRE Agent?',
                'What are some common use cases you support?',
                'What are your key capabilities?',
                'Can you explain how you help with incident management?',
                'How do I connect to an Incident Platform?',
                'How does SRE Agent’s billing work?',
                'Which Azure services do you support?',
            ],
            []
        );

        const appServicesQs = useMemo<string[]>(
            () => [
                'List all my web apps',
                'What services or resources is my web app connected to?',
                'Which resource group is my app part of?',
                'Which apps are hosted on Linux vs Windows in my environment?',
                'Are any of my web apps still running on deprecated or unsupported runtime versions?',
                'Show me visualization of memory usage % for my web app for last week',
                'Can you list all environment variables or app settings for this app?',
                'What App Service Plan is this app running on, and who else shares it?',
                'Are there any staging slots configured for this web app?',
                'Which apps are using custom domains?',
                'Do any apps in my subscription have ARR affinity enabled?',
                'Which apps have health checks enabled, and what are their probe paths?',
                'Can you list the auto scale rules configured across all of my App Services?',
                'Which apps have diagnostic logging turned on?',
                'Show me all web apps using .NET 6 runtime',
                'What changed in my web app last week?',
                'What are some best practices I can apply to my web app?',
                'Can you analyze my app’s availability over the last 24 hours?',
                'Give me slow endpoints for my APIs',
                'Why is my web app timed out?',
                'Why is my web app throwing 500s?',
                'My web app is down. Can you analyze it?',
                'My web is stuck and is not loading – please investigate',
            ],
            []
        );

        const containerAppsQs = useMemo<string[]>(
            () => [
                'List all my container apps',
                'What is the ingress configuration for my container app?',
                'Which revision of my container app is currently active?',
                'What changed in my container app in the last week?',
                'Show me visualization of memory utilization % for my container app for last week',
                'Show me visualization of CPU utilization % for my container app for last week',
                'What container images are used in each of my container apps?',
                'Which apps have Dapr enabled?',
                'What secrets or environment variables are defined for my app?',
                'Can you list the CPU and memory allocation for each container app?',
                'Which apps are connected to other services via Dapr pub/sub?',
                'Are any of my apps configured to run on a virtual network?',
                'Which of my container apps has auto scaling enabled?',
                'Show me all apps with public ingress enabled',
                'Which of my container apps use managed identities?',
                'Which apps use multiple revisions at once?',
                'What are some best practices I can apply to my container app?',
                'My container app is stuck in an activation failed state. Please investigate.',
                'Why is my container app timed out?',
                'Why is my web app throwing 500s?',
                'My container app is down. Can you analyze it?',
                'My web is stuck and is not loading – please investigate.',
            ],
            []
        );

        const aksQs = useMemo<string[]>(
            () => [
                'Which node pools are configured for my AKS cluster?',
                'Which workloads are in a crash loop or failed state?',
                'Do I have any pending or unscheduled pods?',
                'Can you change settings on the cluster?',
                'Scale out deployment inside my AKS cluster',
                'What version of Kubernetes is my cluster running?',
                'How many pods are currently running in my cluster?',
                'What are the configured auto scale rules for my deployments?',
                'What resource limits and requests are configured for my app containers?',
                'Can you list all services exposed via LoadBalancer in my cluster?',
                'Which deployments use persistent volumes?',
                'Are there any cluster-wide policies enforced like PodSecurity or NetworkPolicies?',
                'Can you give me all the runtime languages of my AKS clusters?',
                'Can you give me environment variables for my AKS clusters?',
                'Show me visualization of requests and 500 errors (area chart) for my app in AKS cluster for the past week. Please include all data points.',
                'What are some best practices I can apply to my AKS cluster?',
                'Is there an OOM in my deployment?',
                'Analyze requests and limits in my namespace',
                'Why is my deployment down?',
            ],
            []
        );

        const apimQs = useMemo<string[]>(
            () => [
                'Can you show me my API Management instances?',
                'I need details about my specific API Management instance',
                'What backends does my API Management instance have?',
                'Does my API Management instance have any unhealthy backend apps?',
                'What API policies does my API Management instance have?',
                'What Operation policies does my {api-name} API have?',
                'What NSG rules does my API Management instance have?',
                'Why am I getting 500 errors in my API Management?',
                'Can you help me figure out why requests to our API are failing?',
                'Show me recent changes to our API Management instance',
                'Why is my API Management slow?',
                'Can you help me scale my API Management instance',
                'Can you show me the recent failure logs for my API Management?',
                "What's the failure rate for my API operations in my API Management?",
                'Is there anything wrong with my API Managements VNet configuration?',
                'Can you help me inspect the global policy for my API Management?',
                'Is my {name-here} API in my API Management causing any errors?',
                'Can you help me change/delete my {nsg-name} NSG rule on my API Management instance?',
            ],
            []
        );

        const groupedQuestions = useMemo(() => {
            return {
                'Get started': {
                    '': aboutMeQs,
                },
                'Azure App Service': {
                    'Resource discovery': appServicesQs.slice(0, 16),
                    'Diagnostics + troubleshooting': appServicesQs.slice(16),
                },
                'Azure Container App': {
                    'Resource discovery': containerAppsQs.slice(0, 17),
                    'Diagnostics + troubleshooting': containerAppsQs.slice(17),
                },
                'Azure Kubernetes Service': {
                    'Resource discovery': aksQs.slice(0, 16),
                    'Diagnostics + troubleshooting': aksQs.slice(16),
                },
                'Azure API Management': {
                    'Resource discovery': apimQs.slice(0, 8),
                    'Diagnostics + troubleshooting': apimQs.slice(8),
                },
            } as Record<string, Record<string, string[]>>;
        }, [aboutMeQs, appServicesQs, containerAppsQs, aksQs, apimQs]);

        const getCategorySubcategories = useCallback((category: string) => groupedQuestions[category] ?? null, [groupedQuestions]);

        const getQuestionsForCategoryFlat = useCallback(
            (category: string): string[] => {
                // For search fallback (flatten subcategories)
                const subcats = groupedQuestions[category];
                if (subcats) {
                    return Object.values(subcats).flat();
                }
                return [];
            },
            [groupedQuestions]
        );

        // Filtering logic updated to use flattened questions
        const normalizedQuery = query.trim().toLowerCase();
        const filteredCategories = useMemo(() => {
            if (!normalizedQuery) return categories;
            return categories.filter(cat =>
                getQuestionsForCategoryFlat(cat).some((q: string) => q.toLowerCase().includes(normalizedQuery))
            );
        }, [categories, getQuestionsForCategoryFlat, normalizedQuery]);

        const filteredGetQuestionsForCategory = useCallback(
            (category: string) => {
                const all = getQuestionsForCategoryFlat(category);
                if (!normalizedQuery) return all;
                return all.filter((q: string) => q.toLowerCase().includes(normalizedQuery));
            },
            [getQuestionsForCategoryFlat, normalizedQuery]
        );

        // Wrap sendMessage to close the dialog after selection
        const sendAndClose = useCallback(
            async (message: string) => {
                await sendMessage(message);
                setOpen(false);
                logAmplitudeControlEvent({
                    targetType: 'button',
                    targetAction: 'clicked',
                    targetName: 'promptLibrary',
                    targetFriendlyName: 'Prompt library',
                    valueObjectName: message,
                    valueObjectFriendlyName: message,
                });
            },
            [sendMessage, logAmplitudeControlEvent]
        );

        if (!asOverflowItem && isVisible) {
            return null;
        }

        const ButtonComponent = () => (
            <Button
                style={{ fontSize: '13px', padding: '2px 8px 2px 4px', whiteSpace: 'nowrap' }}
                icon={<Lightbulb16Regular />}
                disabled={disableInputInteraction || isTyping}
            >
                <FormattedMessage {...PromptResources.promptLibrary} />
            </Button>
        );

        return (
            <Dialog open={open} onOpenChange={(_, data) => setOpen(!!data.open)}>
                <DialogTrigger disableButtonEnhancement>
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
                </DialogTrigger>
                <DialogSurface style={{ width: '950px', maxWidth: '950px', display: 'flex', flexDirection: 'column', gap: '15px' }}>
                    <DialogBody style={{ display: 'flex', flexDirection: 'column', gap: '15px' }}>
                        <DialogTitle>
                            <FormattedMessage {...PromptResources.promptExamples} />
                        </DialogTitle>
                        <DialogContent>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '15px', maxWidth: '100%' }}>
                                <Input
                                    placeholder={intl.formatMessage(SreAgentResources.search)}
                                    value={query}
                                    onChange={(_, data) => setQuery(data.value)}
                                    disabled={disableInputInteraction || isTyping}
                                    style={{ maxWidth: '470px' }}
                                />
                                {filteredCategories.length > 0 ? (
                                    <ChatSuggestions
                                        sendMessage={sendAndClose}
                                        categories={filteredCategories}
                                        getQuestionsForCategory={filteredGetQuestionsForCategory}
                                        showSreAgentLogo={false}
                                        alignLeft={true}
                                        getCategorySubcategories={getCategorySubcategories}
                                        initialExpandedCategory={'Get started'}
                                    />
                                ) : (
                                    <Text size={200} style={{ opacity: 0.7 }}>
                                        No matches
                                    </Text>
                                )}
                            </div>
                        </DialogContent>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
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
