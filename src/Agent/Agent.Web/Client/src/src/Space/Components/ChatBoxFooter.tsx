import { ChatInput } from '@fluentui-copilot/react-chat-input';
import { BasicFunctionalityPlugin, BasicFunctionalityPluginRef } from '@fluentui-copilot/react-chat-input-plugins';
import { ImperativeControlPlugin, ImperativeControlPluginRef } from '@fluentui-copilot/react-copilot';
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
    Tag,
    Text,
    Tooltip,
    makeStyles,
    mergeClasses,
    tokens,
} from '@fluentui/react-components';
import { Dismiss16Regular, Lightbulb32Regular, SearchSparkle32Regular } from '@fluentui/react-icons';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import React, { memo, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';

import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { ActivitiesResources, AgentTaskResources, PromptResources, SreAgentResources } from '../../Strings/SREAgentResources';

import { IChatBoxFooterProps } from '../Contracts/Activities';
import { AgentContext, StreamingContext } from '../Contracts/Context';
import { ExtendedAgent } from '../Contracts/ExtendedAgentGraph';
import { usePermissionContext } from '../Contracts/PermissionContext';
import { chatInputTextStyles, useChatInputStyles, useDialogStyles } from '../Styles/Activities.styles';

import { ChatSuggestions } from '../Activities/ChatSuggestions';
import AgentModeSelector from './AgentModeSelector';
import KnowledgeGraphBuildStatus from './KnowledgeGraphBuildStatus';
import { SlashCommandMenu } from './SlashCommandMenu';

enum ChatBoxButtonIds {
    DeepInvestigation = 'deep-investigation',
    AgentMode = 'agent-mode',
    PromptLibrary = 'prompt-library',
}

enum SlashCommandIds {
    ExtendedAgents = 'extended-agents',
    ClearThread = 'clear-thread',
    CompactThread = 'compact-thread',
}

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

const useSlashRowStyles = makeStyles({
    selectedRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        position: 'absolute',
        bottom: '100%',
        left: 0,
        marginBottom: tokens.spacingVerticalXS,
        zIndex: 3,
    },
});

const DownButton = ({ downButtonState, onClick }: { downButtonState: { visible: boolean; flash: boolean }; onClick: () => void }) => {
    const { root, hidden } = useDownButtonStyles();
    const buttonStyles = mergeClasses(root, downButtonState.visible ? undefined : hidden);
    return <ScrollDownButton onClick={onClick} className={buttonStyles} isGenerating={downButtonState.flash} />;
};

// List of valid slash commands for validation
const VALID_COMMANDS = ['/extended-agents', '/clear', '/compact'];

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
    threadSource,
    showDeepInvestigationButton,
    isDeepInvestigationButtonEnabled,
    isDeepInvestigationTurnedOn,
    onClickDeepInvestigationButton,
}: IChatBoxFooterProps) => {
    const intl = useIntl();

    const [historyIndex, setHistoryIndex] = useState<number>(-1);
    const [originalInput, setOriginalInput] = useState<string>('');

    // slash commands state
    const [inputValue, setInputValue] = useState<string>('');
    const [showSlashMenu, setShowSlashMenu] = useState(false);
    const [activeCommandId, setActiveCommandId] = useState<string | null>(null);
    const [selectedAgentName, setSelectedAgentName] = useState<string | null>(null);

    // Track if we're in the process of updating input
    const isUpdatingInputRef = useRef(false);

    // lets the SlashCommandMenu own arrow/enter navigation before we do anything else
    const slashCommandKeyHandlerRef = useRef<((event: React.KeyboardEvent) => boolean) | null>(null);

    const showAgentModeSelector = useConfigSetting(SettingNames.ShowAgentModeForThread);
    const { root, chatStatement } = useChatInputStyles();
    const { selectedRow } = useSlashRowStyles();

    const { selectThread } = useContext(AgentContext);
    const { isConnected } = useContext(StreamingContext);
    const { canWriteThreads } = usePermissionContext();
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const imperativeControlPluginRef = useRef<ImperativeControlPluginRef>(null);
    const basicFunctionalityPluginRef = useRef<BasicFunctionalityPluginRef>(null);

    const disableInputInteraction = useMemo(
        () => isLoading || !isConnected || isCancellingStreaming || !canWriteThreads,
        [isLoading, isConnected, isCancellingStreaming, canWriteThreads]
    );

    // keep native shortcuts/undo/etc. intact, but disable plugin-level helpers when interaction is blocked
    useEffect(() => {
        basicFunctionalityPluginRef.current?.setIsDisabled?.(disableInputInteraction);
    }, [disableInputInteraction]);

    // Function to check if a string starts with any valid command
    const startsWithValidCommand = useCallback((text: string): boolean => {
        const normalized = text.trim().toLowerCase();
        return VALID_COMMANDS.some(cmd => normalized.startsWith(cmd));
    }, []);

    // Update slash menu visibility based on input
    const updateSlashMenuVisibility = useCallback(
        (value: string) => {
            const trimmed = value.trim();

            if (!trimmed) {
                // Empty input - close menu
                setShowSlashMenu(false);
                setActiveCommandId(selectedAgentName ? SlashCommandIds.ExtendedAgents : null);
                return;
            }

            if (trimmed === '/') {
                // Just slash - show menu
                setShowSlashMenu(true);
                setActiveCommandId(SlashCommandIds.ExtendedAgents);
                return;
            }

            if (trimmed.startsWith('/')) {
                // Check if it matches any valid command
                if (startsWithValidCommand(trimmed)) {
                    setShowSlashMenu(true);

                    // Update active command
                    const normalized = trimmed.toLowerCase();
                    if (normalized.startsWith('/extended-agents')) {
                        setActiveCommandId(SlashCommandIds.ExtendedAgents);
                    } else if (normalized.startsWith('/clear')) {
                        setActiveCommandId(SlashCommandIds.ClearThread);
                    } else if (normalized.startsWith('/compact')) {
                        setActiveCommandId(SlashCommandIds.CompactThread);
                    }
                } else {
                    // Starts with slash but doesn't match any command - close menu
                    setShowSlashMenu(false);
                    setActiveCommandId(null);
                }
            } else {
                // Doesn't start with slash - close menu
                setShowSlashMenu(false);
                setActiveCommandId(null);
            }
        },
        [selectedAgentName, startsWithValidCommand]
    );

    // helpers to read/write the ChatInput value through the imperative plugin
    const setInputText = useCallback(
        (value: string) => {
            isUpdatingInputRef.current = true;
            imperativeControlPluginRef.current?.setInputText(value);
            setInputValue(value);
            updateSlashMenuVisibility(value);
            // Reset the flag after a microtask to ensure the input has been updated
            Promise.resolve().then(() => {
                isUpdatingInputRef.current = false;
            });
        },
        [updateSlashMenuVisibility]
    );

    const getInputText = useCallback(() => {
        return imperativeControlPluginRef.current?.getInputText() ?? '';
    }, []);

    const handleEditorContentChange = useCallback(
        (value: string) => {
            if (isUpdatingInputRef.current) {
                return;
            }

            setInputValue(value);
            updateSlashMenuVisibility(value);
        },
        [updateSlashMenuVisibility]
    );

    // SEND
    const chatInputHandleSendClick = useCallback(
        (valueFromSubmit?: string) => {
            const raw = valueFromSubmit ?? getInputText();
            const messageToSend = raw.trim();

            if (!messageToSend || disableInputInteraction || isTyping) return;

            // Reset local input state
            setInputText('');
            setHistoryIndex(-1);
            setOriginalInput('');
            setShowSlashMenu(false);
            setActiveCommandId(null);

            // For now we can encode selected agent choice inline if needed
            const finalMessage = selectedAgentName ? `@${selectedAgentName}: ${messageToSend}` : messageToSend;

            void sendMessage(finalMessage, selectedAgentName ? { starterAgentName: selectedAgentName } : undefined);

            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'sendMessage',
                targetFriendlyName: 'Send message',
                valueObjectName: SpecialControlValue.CustomerSuppliedData,
                valueObjectFriendlyName: SpecialControlValue.CustomerSuppliedData,
                metadata: {
                    threadId,
                    threadType: threadSource,
                    selectedAgent: selectedAgentName ?? undefined,
                    commandId: activeCommandId ?? undefined,
                },
            });
        },
        [
            activeCommandId,
            disableInputInteraction,
            getInputText,
            isTyping,
            logAmplitudeControlEvent,
            selectedAgentName,
            sendMessage,
            setInputText,
            threadId,
            threadSource,
        ]
    );

    // KEY HANDLER (centralized) — run this in capture phase so plugins can't swallow keys first
    const onKeyDown = useCallback(
        (event: React.KeyboardEvent<HTMLDivElement>) => {
            // 1) Let the slash-menu intercept navigation keys first
            if (showSlashMenu) {
                const handledBySlash = slashCommandKeyHandlerRef.current?.(event) ?? false;
                if (handledBySlash) {
                    event.preventDefault();
                    event.stopPropagation();
                    return;
                }

                // Handle Escape to close menu
                if (event.key === 'Escape') {
                    event.preventDefault();
                    event.stopPropagation();
                    setShowSlashMenu(false);
                    setActiveCommandId(null);
                    return;
                }

                // Prevent arrow keys from moving cursor when menu is open
                if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
                    event.preventDefault();
                    event.stopPropagation();
                    return;
                }
            }

            // 2) Global shortcuts interference guard
            if (event.key.toLowerCase() === 'g') {
                event.stopPropagation();
                return;
            }

            // 3) Enter to send (without Shift)
            if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                event.stopPropagation();
                chatInputHandleSendClick();
                return;
            }

            // 4) History navigation (only when slash menu is not active)
            if (!showSlashMenu && event.key === 'ArrowUp' && messagePromptsUsed.length > 0) {
                event.preventDefault();
                event.stopPropagation();

                if (historyIndex === -1) {
                    setOriginalInput(getInputText());
                    setHistoryIndex(0);
                    const v = messagePromptsUsed[0];
                    setInputText(v);
                } else if (historyIndex < messagePromptsUsed.length - 1) {
                    const newIndex = historyIndex + 1;
                    setHistoryIndex(newIndex);
                    setInputText(messagePromptsUsed[newIndex]);
                }
                return;
            }

            if (!showSlashMenu && event.key === 'ArrowDown' && historyIndex >= 0) {
                event.preventDefault();
                event.stopPropagation();

                if (historyIndex > 0) {
                    const newIndex = historyIndex - 1;
                    setHistoryIndex(newIndex);
                    setInputText(messagePromptsUsed[newIndex]);
                } else {
                    setHistoryIndex(-1);
                    setInputText(originalInput);
                    setOriginalInput('');
                }
                return;
            }
        },
        [chatInputHandleSendClick, getInputText, historyIndex, messagePromptsUsed, originalInput, setInputText, showSlashMenu]
    );

    // Slash-menu callbacks
    const handleInsertCommand = useCallback(
        (commandText: string) => {
            setInputText(commandText);
            updateSlashMenuVisibility(commandText);
        },
        [setInputText, updateSlashMenuVisibility]
    );

    const handleSlashCommandInvoked = useCallback(
        (commandId: string) => {
            setActiveCommandId(commandId);

            if (commandId === SlashCommandIds.ExtendedAgents) {
                // keep menu open to select an agent
                return;
            }

            if (commandId === SlashCommandIds.ClearThread) {
                selectThread(null);
            }

            if (commandId === SlashCommandIds.CompactThread) {
                chatInputHandleSendClick('/compact');
            }

            // clear/compact are immediate actions
            setSelectedAgentName(null);
            setInputText('');
            setHistoryIndex(-1);
            setOriginalInput('');
            setShowSlashMenu(false);
            setActiveCommandId(null);
        },
        [setInputText, selectThread, chatInputHandleSendClick]
    );

    const handleSelectAgent = useCallback(
        (agent: ExtendedAgent) => {
            setSelectedAgentName(agent.name);
            setInputText(''); // clear input for user to type message
            setShowSlashMenu(false);
            setActiveCommandId(null);
        },
        [setInputText]
    );

    const handleClearSelectedAgent = useCallback(() => {
        if (!selectedAgentName) return;
        setSelectedAgentName(null);
        setActiveCommandId(null);
    }, [selectedAgentName]);

    return (
        <div className={root}>
            <KnowledgeGraphBuildStatus />
            <div className={mergeStyles(chatInputTextStyles.textFieldContainer as IStyle)} style={{ position: 'relative' }}>
                {/* Selected agent tag row */}
                {selectedAgentName && (
                    <div className={selectedRow}>
                        <Tag appearance="brand">
                            <Text weight="semibold">
                                <FormattedMessage {...ActivitiesResources.slashCommandExtendedAgentTagLabel} />: {selectedAgentName}
                            </Text>
                        </Tag>
                        <Button
                            appearance="subtle"
                            size="small"
                            icon={<Dismiss16Regular />}
                            onClick={handleClearSelectedAgent}
                            aria-label={intl.formatMessage(ActivitiesResources.slashCommandExtendedAgentRemoveButtonLabel)}
                        />
                    </div>
                )}

                <DownButton downButtonState={downButtonState} onClick={onClickDownButton} />

                <ChatInput
                    aria-label={intl.formatMessage(ActivitiesResources.chatInputAriaLabel)}
                    placeholderValue={<FormattedMessage {...ActivitiesResources.chatInputPlaceholder} />}
                    contentBefore={
                        <ContentBefore
                            showDeepInvestigationButton={showDeepInvestigationButton}
                            isDeepInvestigationButtonEnabled={isDeepInvestigationButtonEnabled}
                            isDeepInvestigationTurnedOn={isDeepInvestigationTurnedOn}
                            onClickDeepInvestigationButton={onClickDeepInvestigationButton}
                            showAgentModeSelector={showAgentModeSelector}
                            threadId={threadId}
                            isTyping={isTyping}
                            disableInputInteraction={disableInputInteraction}
                            messagePromptsUsed={messagePromptsUsed}
                            sendMessage={sendMessage}
                            prompts={prompts}
                            threadSource={threadSource}
                        />
                    }
                    maxLength={1000000000}
                    charactersRemainingMessage={undefined}
                    autoFocus={true}
                    onKeyDownCapture={onKeyDown}
                    listbox={
                        showSlashMenu ? (
                            <SlashCommandMenu
                                isOpen={showSlashMenu}
                                onClose={() => {
                                    setShowSlashMenu(false);
                                    setActiveCommandId(null);
                                }}
                                onSelectAgent={handleSelectAgent}
                                inputValue={inputValue}
                                onInsertCommand={handleInsertCommand}
                                onCommandInvoked={handleSlashCommandInvoked}
                                activeCommandId={activeCommandId}
                                selectedAgentName={selectedAgentName}
                                onKeyHandlerChange={handler => {
                                    slashCommandKeyHandlerRef.current = handler;
                                }}
                            />
                        ) : undefined
                    }
                    isInSelectionMode={showSlashMenu}
                    disableSend={!canWriteThreads || disableInputInteraction}
                    isSending={isTyping}
                    onSubmit={(_, data) => chatInputHandleSendClick(data.value)}
                    onStop={cancelStreaming}
                    expandButtonLineVisibilityThreshold={3}
                >
                    <ImperativeControlPlugin ref={imperativeControlPluginRef} />
                    <BasicFunctionalityPlugin ref={basicFunctionalityPluginRef} onContentChange={handleEditorContentChange} />
                </ChatInput>
            </div>

            <Text block size={200} align="center" className={mergeStyles(chatStatement)}>
                {intl.formatMessage(SreAgentResources.chatAiContentAndPrivacyMessageStatement)}
            </Text>
        </div>
    );
};

const ContentBefore = (props: {
    showDeepInvestigationButton: boolean;
    isDeepInvestigationButtonEnabled: boolean;
    isDeepInvestigationTurnedOn: boolean;
    onClickDeepInvestigationButton: () => void;
    showAgentModeSelector: boolean;
    threadId?: string | null;
    isTyping: boolean;
    disableInputInteraction: boolean;
    messagePromptsUsed: string[];
    sendMessage: (message: string) => Promise<void>;
    prompts: string[];
    threadSource?: string;
}) => {
    return (
        <div>
            <DeepInvestigationButton
                showDeepInvestigationButton={props.showDeepInvestigationButton}
                isDeepInvestigationButtonEnabled={props.isDeepInvestigationButtonEnabled}
                isDeepInvestigationTurnedOn={props.isDeepInvestigationTurnedOn}
                onClickDeepInvestigationButton={props.onClickDeepInvestigationButton}
            />
            {props.showAgentModeSelector && props.threadId && (
                <AgentModeSelector id={ChatBoxButtonIds.AgentMode} threadId={props.threadId} disabled={props.isTyping} />
            )}
            <PromptLibraryButton
                isTyping={props.isTyping}
                disableInputInteraction={props.disableInputInteraction}
                messagePromptsUsed={props.messagePromptsUsed}
                sendMessage={props.sendMessage}
                prompts={props.prompts}
                threadId={props.threadId}
                threadSource={props.threadSource}
            />
        </div>
    );
};

const DeepInvestigationButton = memo(
    ({
        showDeepInvestigationButton,
        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        onClickDeepInvestigationButton,
    }: {
        showDeepInvestigationButton: boolean;
        isDeepInvestigationButtonEnabled: boolean;
        isDeepInvestigationTurnedOn: boolean;
        onClickDeepInvestigationButton: () => void;
    }) => {
        const { canWriteThreads } = usePermissionContext();
        const intl = useIntl();

        const tooltipContentWhenNoPermission = isDeepInvestigationTurnedOn
            ? intl.formatMessage(AgentTaskResources.deepInvestigationNoPermissionTurnedOnMessage)
            : intl.formatMessage(AgentTaskResources.deepInvestigationNoPermissionTurnedOffMessage);
        const stateTooltip = isDeepInvestigationTurnedOn
            ? intl.formatMessage(AgentTaskResources.deepInvestigationTurnedOnMessage)
            : intl.formatMessage(AgentTaskResources.deepInvestigationTurnedOffMessage);

        return (
            showDeepInvestigationButton && (
                <PermissionedButton
                    canPerform={canWriteThreads}
                    noPermissionTooltip={canWriteThreads ? '' : tooltipContentWhenNoPermission}
                    allowedTooltip={stateTooltip}
                    icon={<SearchSparkle32Regular />}
                    disabledReason={!isDeepInvestigationButtonEnabled}
                    appearance={isDeepInvestigationTurnedOn ? 'primary' : 'subtle'}
                    shape={'rounded'}
                    onClick={() => onClickDeepInvestigationButton()}
                    style={{ marginRight: tokens.spacingHorizontalS }}
                />
            )
        );
    }
);

const PromptLibraryButton = memo(
    ({
        isTyping,
        disableInputInteraction,
        messagePromptsUsed: _messagePromptsUsed,
        sendMessage,
        prompts: _prompts,
        threadId,
        threadSource,
    }: {
        isTyping: boolean;
        disableInputInteraction: boolean;
        messagePromptsUsed: string[];
        sendMessage: (message: string) => Promise<void>;
        prompts: string[];
        threadId?: string | null;
        threadSource?: string;
    }) => {
        const { logAmplitudeControlEvent } = useAzPortalContext();
        const [open, setOpen] = useState(false);
        const [query, setQuery] = useState('');
        const intl = useIntl();
        const { dialogSurface, dialogBody, dialogContent } = useDialogStyles();

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
                'How does SRE Agents billing work?',
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
                'Can you analyze my apps availability over the last 24 hours?',
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
                'Get started': { '': aboutMeQs },
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
                const subcats = groupedQuestions[category];
                return subcats ? Object.values(subcats).flat() : [];
            },
            [groupedQuestions]
        );

        const normalizedQuery = query.trim().toLowerCase();
        const filteredCategories = useMemo(() => {
            if (!normalizedQuery) return categories;
            return categories.filter(cat => getQuestionsForCategoryFlat(cat).some(q => q.toLowerCase().includes(normalizedQuery)));
        }, [categories, getQuestionsForCategoryFlat, normalizedQuery]);

        const filteredGetQuestionsForCategory = useCallback(
            (category: string) => {
                const all = getQuestionsForCategoryFlat(category);
                return !normalizedQuery ? all : all.filter(q => q.toLowerCase().includes(normalizedQuery));
            },
            [getQuestionsForCategoryFlat, normalizedQuery]
        );

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
                    metadata: { threadId, threadType: threadSource },
                });
            },
            [sendMessage, logAmplitudeControlEvent, threadId, threadSource]
        );

        return (
            <Dialog open={open} onOpenChange={(_, data) => setOpen(!!data.open)}>
                <DialogTrigger disableButtonEnhancement>
                    <Tooltip content={intl.formatMessage(PromptResources.promptExamples)} relationship="label">
                        <Button
                            icon={<Lightbulb32Regular />}
                            disabled={disableInputInteraction || isTyping}
                            shape="rounded"
                            appearance="subtle"
                        />
                    </Tooltip>
                </DialogTrigger>
                <DialogSurface className={dialogSurface}>
                    <DialogBody className={dialogBody}>
                        <DialogTitle>
                            <FormattedMessage {...PromptResources.promptExamples} />
                        </DialogTitle>
                        <DialogContent>
                            <div className={dialogContent}>
                                <Input
                                    placeholder={intl.formatMessage(SreAgentResources.search)}
                                    value={query}
                                    onChange={(_, data) => setQuery(data.value)}
                                    disabled={disableInputInteraction || isTyping}
                                    style={{ maxWidth: 470 }}
                                />
                                {filteredCategories.length > 0 ? (
                                    <ChatSuggestions
                                        sendMessage={sendAndClose}
                                        categories={filteredCategories}
                                        getQuestionsForCategory={filteredGetQuestionsForCategory}
                                        showSreAgentLogo={false}
                                        alignLeft={true}
                                        getCategorySubcategories={getCategorySubcategories}
                                        initialExpandedCategory="Get started"
                                    />
                                ) : (
                                    <Text size={200} style={{ opacity: 0.7 }}>
                                        {intl.formatMessage(SreAgentResources.noMatches)}
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

export default memo(ChatBoxFooter);
