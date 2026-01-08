// ...existing imports...
import { ChatInput } from '@fluentui-copilot/react-chat-input';
import {
    $createGhostTextNode,
    $createTextNode,
    $getSelection,
    $isElementNode,
    $isGhostTextNode,
    $isRangeSelection,
    $isTextNode,
    $nodesOfType,
    $setSelection,
    Attachment,
    AttachmentList,
    AttachmentOverflowMenuButton,
    COMMAND_PRIORITY_LOW,
    DecoratorNode,
    ElementNode,
    GhostTextNode,
    GroundingMenuItemSkeleton,
    ImperativeControlPlugin,
    ImperativeControlPluginRef,
    Klass,
    LexicalEditor,
    LexicalEditorRefPlugin,
    SELECTION_CHANGE_COMMAND,
} from '@fluentui-copilot/react-copilot';
import { ScrollDownButton } from '@fluentui-copilot/react-copilot-chat';
import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    makeStyles,
    MenuItem,
    MenuList,
    mergeClasses,
    Popover,
    PopoverSurface,
    PositioningImperativeRef,
    PositioningProps,
    Spinner,
    Table,
    TableBody,
    TableCell,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    tokens,
    Tooltip,
    useRestoreFocusTarget,
} from '@fluentui/react-components';
import { ChartMultiple24Regular, ChatWarningRegular, Lightbulb32Regular, SearchSparkle32Regular } from '@fluentui/react-icons';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import debounce from 'lodash/debounce';
import React, { memo, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../Common/Clients/ExtendedAgentClient';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { SearchBoxWithDebounce } from '../../Common/Components/SearchBox/SearchBoxWithDebounce';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { Guid } from '../../Common/Helpers/Guid';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { getResourceTypeFriendlyName } from '../../Common/Helpers/Resources';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { useFeatureFlags } from '../../Common/Hooks/useFeatureFlags';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import {
    ActivitiesResources,
    AgentTaskResources,
    IncidentManagementResources,
    PromptResources,
    SreAgentResources,
} from '../../Strings/SREAgentResources';
import { ChatSuggestions } from '../Activities/ChatSuggestions';
import { IChatBoxFooterProps, Shortcut } from '../Contracts/Activities';
import { SreAgentSpaceContext, StreamingContext } from '../Contracts/Context';
import { ExtendedAgent } from '../Contracts/ExtendedAgentGraph';
import { ResourceSearchResult } from '../Contracts/Graph';
import { usePermissionContext } from '../Contracts/PermissionContext';
import { useResourceSearching } from '../Hooks/useResourceSearching';
import { useThreadList } from '../Hooks/useThreadList';
import { chatInputTextStyles, useChatInputStyles, useDialogStyles } from '../Styles/Activities.styles';
import AgentModeSelector from './AgentModeSelector';
import { $createResourceNode, ResourceNode } from './Chat/ResourceNode';
import { $createShortcutNode, $getShortcutValuefromShortcutNode, $isShortcutNode, ShortcutNode } from './Chat/ShortcutNode';
import KnowledgeGraphBuildStatus from './KnowledgeGraphBuildStatus';

const GenerateInsightsButton = memo(
    ({
        threadId,
        isTyping,
        disableInputInteraction,
    }: {
        threadId?: string | null;
        isTyping: boolean;
        disableInputInteraction: boolean;
    }) => {
        const { sreAgentEndpoint } = useContext(EnvironmentContext);
        const { canWriteThreads } = usePermissionContext();
        const intl = useIntl();
        const [isGenerating, setIsGenerating] = useState(false);
        const { iconWrapper } = useFooterButtonIconStyles();
        const { features } = useFeatureFlags();

        const handleGenerateInsights = useCallback(async () => {
            if (!threadId) return;
            setIsGenerating(true);
            try {
                const response = await fetch(`${sreAgentEndpoint}/api/v1/threads/${threadId}/insights`, {
                    method: 'POST',
                    headers: getAgentHeaders(),
                });
                if (!response.ok) {
                    const errorData = await response.json();
                    console.error('Failed to generate insights:', errorData.errorMessage);
                }
            } catch (error) {
                console.error('Error generating insights:', error);
            } finally {
                setIsGenerating(false);
            }
        }, [threadId, sreAgentEndpoint]);

        // Don't render if session insights is disabled (after all hooks)
        if (!features.sessionInsights) {
            return null;
        }

        return (
            <Tooltip content={intl.formatMessage(SreAgentResources.generateInsights)} relationship="label">
                <Button
                    icon={<span className={iconWrapper}>{isGenerating ? <Spinner size="tiny" /> : <ChartMultiple24Regular />}</span>}
                    appearance="subtle"
                    shape="rounded"
                    disabled={!canWriteThreads || isGenerating || !threadId || isTyping || disableInputInteraction}
                    onClick={handleGenerateInsights}
                    style={{ height: '100%' }}
                />
            </Tooltip>
        );
    }
);

enum ChatBoxButtonIds {
    DeepInvestigation = 'deep-investigation',
    AgentMode = 'agent-mode',
    PromptLibrary = 'prompt-library',
}

const useFooterButtonGroupStyles = makeStyles({
    container: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
    trailingGroup: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
});

const useFooterButtonIconStyles = makeStyles({
    iconWrapper: {
        width: '24px',
        height: '24px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        '& svg': {
            width: '24px',
            height: '24px',
        },
    },
});

const useDownButtonStyles = makeStyles({
    root: {
        opacity: '1',
        transition: 'opacity 0.3s ease',
        pointerEvents: 'auto',
        position: 'absolute',
        right: '50%',
        bottom: '105%',
    },
    hidden: {
        opacity: '0',
        pointerEvents: 'none',
    },
});

const useShortcutStyles = makeStyles({
    incidentOrResourcePopoverSurface: {
        minHeight: '300px',
        maxHeight: '500px',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    incidentOrResourceContainer: {
        minWidth: '510px',
        maxWidth: '950px',
    },
});

const useNoSearchResultWarningStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalM,
    },
    extendedAgent: {
        padding: '20px',
        minWidth: '100px',
    },
    incident: {
        position: 'fixed',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
    },
});

const DownButton = ({ downButtonState, onClick }: { downButtonState: { visible: boolean; flash: boolean }; onClick: () => void }) => {
    const { root, hidden } = useDownButtonStyles();
    const buttonStyles = mergeClasses(root, downButtonState.visible ? undefined : hidden);
    return <ScrollDownButton onClick={onClick} className={buttonStyles} isGenerating={downButtonState.flash} />;
};

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
    isDeepInvestigationButtonEnabled,
    isDeepInvestigationTurnedOn,
    onClickDeepInvestigationButton,
    postSystemMessage = () => undefined,
    forcedAgentName,
    lockAgentSelection,
    inputDisabledMessage,
}: IChatBoxFooterProps) => {
    const intl = useIntl();

    // Preserve reference for shared interface until downstream usage is implemented in the playground.
    void postSystemMessage;

    const [historyIndex, setHistoryIndex] = useState<number>(-1);
    const [originalInput, setOriginalInput] = useState<string>('');

    const [showShortcutLists, setShowShortcutLists] = useState<boolean>(false);
    const [selectedShortcut, setSelectedShortcut] = useState<Shortcut | null>(null);
    const [focusedShortcut, setFocusedShortcut] = useState<Shortcut | null>(null);
    const [focusedIncident, setFocusedIncident] = useState<Thread | null>(null);
    const [focusedResource, setFocusedResource] = useState<ResourceSearchResult | null>(null);
    const [focusedExtendedAgent, setFocusedExtendedAgent] = useState<ExtendedAgent | null>(null);
    const [searchText, setSearchText] = useState<string>('');
    const [matchedShortcutString, setMatchedShortcutString] = useState<string | null>(null);
    const [selectedAgentName, setSelectedAgentName] = useState<string | null>(forcedAgentName ?? null);
    const [extendedAgents, setExtendedAgents] = useState<ExtendedAgent[]>([]);

    const showAgentModeSelector = useConfigSetting(SettingNames.ShowAgentModeForThread);
    const { root, chatStatement } = useChatInputStyles();

    const { selectThread } = useContext(SreAgentSpaceContext);
    const { isConnected } = useContext(StreamingContext);
    const { canWriteThreads } = usePermissionContext();
    const { logAmplitudeControlEvent } = useAzPortalContext();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);
    const imperativeControlPluginRef = useRef<ImperativeControlPluginRef>(null);
    const editorRef = useRef<LexicalEditor | null>(null);
    const chatInputRef = useRef<HTMLDivElement | null>(null);
    const shortcutMenuPositionRef = useRef<PositioningImperativeRef>(null);
    const extendedAgentMenuPositionRef = useRef<PositioningImperativeRef>(null);
    const incidentOrResourcePopoverPositionRef = useRef<PositioningImperativeRef>(null);
    const focusedShortcutRef = useRef<Shortcut | null>(null);
    const focusedExtendedAgentRef = useRef<ExtendedAgent | null>(null);
    const focusedIncidentRef = useRef<Thread | null>(null);
    const focusedResourceRef = useRef<ResourceSearchResult | null>(null);

    focusedShortcutRef.current = focusedShortcut;
    focusedExtendedAgentRef.current = focusedExtendedAgent;
    focusedIncidentRef.current = focusedIncident;
    focusedResourceRef.current = focusedResource;

    const [showInputDisabledDialog, setShowInputDisabledDialog] = useState<boolean>(false);

    const restoreFocusTargetAttribute = useRestoreFocusTarget();
    const { scrollable } = useScrollableComponentStyles();
    const shortcutStyles = useShortcutStyles();

    const disableInputInteractionUnforced = useMemo(
        () => isLoading || !isConnected || isCancellingStreaming || !canWriteThreads,
        [isLoading, isConnected, isCancellingStreaming, canWriteThreads]
    );

    const disableInputInteraction = useMemo(
        () => disableInputInteractionUnforced || !!inputDisabledMessage,
        [disableInputInteractionUnforced, inputDisabledMessage]
    );

    const matchedShortcuts = useMemo(() => {
        return Object.values(Shortcut)
            .filter(shortcut => !lockAgentSelection || shortcut !== Shortcut.Agent)
            .filter(shortcut => !matchedShortcutString || shortcut.toLowerCase().startsWith(matchedShortcutString.toLowerCase()));
    }, [lockAgentSelection, matchedShortcutString]);

    useEffect(() => {
        if (forcedAgentName) {
            setSelectedAgentName(prev => (prev === forcedAgentName ? prev : forcedAgentName));
        } else if (lockAgentSelection && selectedAgentName) {
            setSelectedAgentName(null);
        }

        if (lockAgentSelection && selectedShortcut === Shortcut.Agent) {
            setSelectedShortcut(null);
        }
    }, [forcedAgentName, lockAgentSelection, selectedAgentName, selectedShortcut]);

    const openShortcutList = (matchedString: string) => {
        setShowShortcutLists(true);
        setMatchedShortcutString(matchedString);
        closeShortcutResourcePopover();
    };

    const openShortcutResourcePopover = (shortcut?: Shortcut | null) => {
        if (lockAgentSelection && shortcut === Shortcut.Agent) {
            closeShortcutList();
            return;
        }

        // Remember/Retrieve don't need a resource popover - user just types content after the pill
        if (shortcut === Shortcut.Remember || shortcut === Shortcut.Retrieve) {
            closeShortcutList();
            return;
        }

        if (shortcut) {
            setSelectedShortcut(shortcut);
        }
        closeShortcutList();
    };

    const closeShortcutList = () => {
        setShowShortcutLists(false);
        setFocusedShortcut(null);
        setMatchedShortcutString(null);
    };

    const closeShortcutResourcePopover = () => {
        setSelectedShortcut(null);
        setFocusedIncident(null);
        setFocusedResource(null);
        setFocusedExtendedAgent(null);
        setSearchText('');
    };

    const convertShortcutNodeToTextNode = () => {
        const nodes = $nodesOfType(ShortcutNode);
        if (nodes) {
            nodes.forEach(node => {
                if (node.getParent()) {
                    const shortcut = $getShortcutValuefromShortcutNode(node);
                    // Use # prefix for remember/retrieve commands (backend expects this format)
                    const prefix = shortcut === Shortcut.Remember || shortcut === Shortcut.Retrieve ? '#' : '/';
                    node.replace($createTextNode(`${prefix}${shortcut} `));
                }
            });
        }
    };

    const removeDecorateNode = useCallback((nodeType: Klass<DecoratorNode<JSX.Element | null>>) => {
        const nodes = $nodesOfType(nodeType);
        if (nodes) {
            nodes.forEach(node => {
                if (node.getParent()) {
                    node.remove();
                }
            });
        }
    }, []);

    const removeShortcutNode = useCallback(() => {
        removeDecorateNode(ShortcutNode);
    }, [removeDecorateNode]);

    const removeGhostTextNode = useCallback(() => {
        removeDecorateNode(GhostTextNode);
    }, [removeDecorateNode]);

    const replaceShortcutWithSelectedValue = useCallback(
        (selectedValue: string, displayValue: string) => {
            setSelectedShortcut(null);

            editorRef.current?.update(() => {
                const selection = $getSelection();
                if ($isRangeSelection(selection)) {
                    const { anchor } = selection;
                    const offset = anchor.offset ?? -1;

                    if ($isElementNode(anchor.getNode())) {
                        const shortcutNode = anchor.getNode()?.getChildAtIndex(anchor.offset - 1);

                        if ($isShortcutNode(shortcutNode)) {
                            removeShortcutNode();

                            const resourceNode = $createResourceNode(selectedValue, displayValue);
                            selection.insertNodes([resourceNode]);
                        }
                    } else if ($isTextNode(anchor.getNode()) && offset !== -1) {
                        const shortcutNode = anchor.getNode()?.getPreviousSibling();
                        if ($isShortcutNode(shortcutNode)) {
                            removeShortcutNode();

                            const node = anchor.getNode();
                            const rangeSelection = selection.clone();
                            rangeSelection.setTextNodeRange(node, 0, node, offset);
                            $setSelection(rangeSelection);
                            rangeSelection.insertNodes([$createResourceNode(selectedValue, displayValue)]);
                        }
                    }
                }
            });
        },
        [removeShortcutNode]
    );

    const onSelectIncident = useCallback(
        (incident: Thread) => {
            setFocusedIncident(null);
            replaceShortcutWithSelectedValue(`$${incident.title} (ID: ${incident.id})`, incident.title);
        },
        [replaceShortcutWithSelectedValue]
    );

    const onSelectResource = useCallback(
        (resource: ResourceSearchResult) => {
            setFocusedResource(null);
            replaceShortcutWithSelectedValue(
                resource.resource_id,
                resource.name || resource.resource_group || resource.subscription_id || resource.resource_id
            );
        },
        [replaceShortcutWithSelectedValue]
    );

    const onSelectExtendedAgent = useCallback(
        (agentName: string) => {
            if (lockAgentSelection) {
                closeShortcutResourcePopover();
                return;
            }

            setFocusedExtendedAgent(null);
            setSelectedShortcut(null);
            setSelectedAgentName(agentName);

            editorRef.current?.update(() => {
                const selection = $getSelection();
                if ($isRangeSelection(selection)) {
                    const { anchor } = selection;
                    const offset = anchor.offset ?? -1;
                    const node = anchor.getNode();

                    const shortcutNode = $isElementNode(node)
                        ? node.getChildAtIndex(anchor.offset - 1)
                        : $isTextNode(node) && offset !== -1
                          ? node.getPreviousSibling()
                          : null;

                    if ($isShortcutNode(shortcutNode)) {
                        removeShortcutNode();
                        selection.insertNodes([$createTextNode('')]);
                    }
                }
            });
        },
        [closeShortcutResourcePopover, lockAgentSelection]
    );

    const includedSourcesForQueryingIncidents = useMemo(() => [ThreadSource.incident], []);

    const {
        threads,
        moreThreadsToLoad,
        isLoadingInitialThreads,
        threadListDivRef,
        intersectionObserverRef: threadsListIntersectionObserverRef,
        onScroll: onScrollThreadsList,
    } = useThreadList(undefined, [], includedSourcesForQueryingIncidents, undefined, undefined, searchText.trim(), 'modifiedTimestamp');

    const {
        resourcesSearchResult,
        moreResourcesSearchResultToLoad,
        isLoadingInitialResults,
        resourcesSearchResultDivRef,
        intersectionObserverRef: resourceSearchResultPopoverIntersectionObserverRef,
        onScroll: onScrollResourceSearchResultDiv,
    } = useResourceSearching(searchText.trim());

    const chatInputHandleSendClick = useCallback(
        (input?: string) => {
            if (inputDisabledMessage) {
                setShowInputDisabledDialog(true);
                return;
            }

            const messageToSend = input?.trim() ?? '';

            if (messageToSend && !disableInputInteraction && !isTyping) {
                imperativeControlPluginRef.current?.setInputText('');
                setHistoryIndex(-1);
                setOriginalInput('');

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
                    },
                });
            }
        },
        [
            disableInputInteraction,
            isTyping,
            logAmplitudeControlEvent,
            selectedAgentName,
            sendMessage,
            threadId,
            threadSource,
            inputDisabledMessage,
        ]
    );

    const onSelectShortcut = useCallback(
        (shortcut: Shortcut) => {
            switch (shortcut) {
                case Shortcut.Clear:
                    selectThread(null);
                    return;
                case Shortcut.Compact:
                    closeShortcutList();
                    chatInputHandleSendClick('/compact');
                    return;
                case Shortcut.Remember:
                case Shortcut.Retrieve:
                    // Create a pill for remember/retrieve, user types content after
                    closeShortcutList();
                    editorRef.current?.update(() => {
                        const selection = $getSelection();
                        if (!$isRangeSelection(selection)) {
                            return;
                        }
                        const { anchor } = selection;
                        const node: ElementNode = anchor.getNode();

                        if (!$isTextNode(node)) {
                            return;
                        }

                        const text = node?.getTextContent() ?? '';
                        const offset = anchor.offset ?? -1;
                        const currentText = text.substring(0, offset);
                        const lastSlashIndex = currentText.lastIndexOf('/');
                        if (lastSlashIndex !== -1) {
                            const rangeSelection = selection.clone();
                            rangeSelection.setTextNodeRange(node, lastSlashIndex, node, offset);
                            $setSelection(rangeSelection);

                            const shortcutNode = $createShortcutNode(shortcut);
                            const emptySpaceNode = $createTextNode(' ');
                            rangeSelection.insertNodes([shortcutNode, emptySpaceNode]);
                            emptySpaceNode.select(1, 1);
                        }
                    });
                    return;
                case Shortcut.Agent:
                case Shortcut.Incident:
                case Shortcut.Resource:
                    if (shortcut === Shortcut.Agent && lockAgentSelection) {
                        return;
                    }
                    openShortcutResourcePopover(shortcut);
                    editorRef.current?.update(() => {
                        const selection = $getSelection();
                        if (!$isRangeSelection(selection)) {
                            return;
                        }
                        const { anchor } = selection;
                        const node: ElementNode = anchor.getNode();

                        if (!$isTextNode(node)) {
                            return;
                        }

                        const text = node?.getTextContent() ?? '';
                        const offset = anchor.offset ?? -1;
                        const currentText = text.substring(0, offset);
                        const lastSlashIndex = currentText.lastIndexOf('/');
                        if (lastSlashIndex !== -1) {
                            const rangeSelection = selection.clone();
                            rangeSelection.setTextNodeRange(node, lastSlashIndex, node, offset);
                            $setSelection(rangeSelection);

                            const shortcutNode = $createShortcutNode(shortcut);
                            const emptySpaceNode = $createTextNode('');
                            rangeSelection.insertNodes([shortcutNode, emptySpaceNode]);
                            emptySpaceNode.select(0, 0);
                        }
                    });
                    return;
            }
        },
        [chatInputHandleSendClick, lockAgentSelection, selectThread]
    );

    //ToDo: replace this with server side filtering
    const filteredExtendedAgents = useMemo(() => {
        if (!searchText) return extendedAgents;
        return extendedAgents.filter(agent => agent.name.toLowerCase().includes(searchText.toLowerCase()));
    }, [searchText, extendedAgents]);

    const onKeyDown = useCallback(
        (event: React.KeyboardEvent<HTMLSpanElement>) => {
            const focus = <T,>(items: T[], prev: T | null, compare: (item: T, prev: T | null) => boolean, arrowUp: boolean) => {
                if (items.length === 0) {
                    return null;
                }

                const currentIndex = items.findIndex(t => compare(t, prev));
                if (currentIndex === -1) {
                    return arrowUp ? items[items.length - 1] : items[0];
                } else {
                    const newIndex = (currentIndex + (arrowUp ? -1 : 1) + items.length) % items.length;
                    return items[newIndex];
                }
            };
            const focusShortcut = (arrowUp: boolean) => {
                setFocusedShortcut(prev => focus<Shortcut>(matchedShortcuts, prev, (item, prev) => item === prev, arrowUp));
            };

            const focusIncident = (arrowUp: boolean) => {
                setFocusedIncident(prev => focus<Thread>(threads, prev, (item, prev) => item.id === prev?.id, arrowUp));
            };

            const focusResource = (arrowUp: boolean) => {
                setFocusedResource(prev =>
                    focus<ResourceSearchResult>(
                        resourcesSearchResult,
                        prev,
                        (item, prev) => item.resource_id === prev?.resource_id,
                        arrowUp
                    )
                );
            };

            const focusExtendedAgent = (arrowUp: boolean) => {
                setFocusedExtendedAgent(prev =>
                    focus<ExtendedAgent>(filteredExtendedAgents, prev, (item, prev) => item.name === prev?.name, arrowUp)
                );
            };

            if (event.key.toLowerCase() === 'g') {
                // Stop the event from propagating to the global shortcuts
                event.stopPropagation();
            } else if (event.key.toLowerCase() === 'enter' && (!event.shiftKey || showShortcutLists || selectedShortcut)) {
                if (selectedShortcut) {
                    if (selectedShortcut === Shortcut.Incident && focusedIncidentRef.current) {
                        onSelectIncident(focusedIncidentRef.current);
                    }

                    if (selectedShortcut === Shortcut.Resource && focusedResourceRef.current) {
                        onSelectResource(focusedResourceRef.current);
                    }

                    if (selectedShortcut === Shortcut.Agent && focusedExtendedAgentRef.current) {
                        onSelectExtendedAgent(focusedExtendedAgentRef.current.name);
                    }
                } else if (showShortcutLists && focusedShortcutRef.current) {
                    onSelectShortcut(focusedShortcutRef.current);
                } else {
                    chatInputHandleSendClick(imperativeControlPluginRef.current?.getInputText());
                }

                event.preventDefault();
                event.stopPropagation();
            } else if (event.key === 'ArrowUp' && (messagePromptsUsed.length > 0 || showShortcutLists || selectedShortcut)) {
                if (selectedShortcut === Shortcut.Incident) {
                    focusIncident(true);
                } else if (selectedShortcut === Shortcut.Resource) {
                    focusResource(true);
                } else if (selectedShortcut === Shortcut.Agent) {
                    focusExtendedAgent(true);
                } else if (showShortcutLists) {
                    focusShortcut(true);
                } else {
                    if (historyIndex === -1) {
                        setOriginalInput(imperativeControlPluginRef.current?.getInputText() || '');
                        setHistoryIndex(0);
                        imperativeControlPluginRef.current?.setInputText(messagePromptsUsed[0]);
                    } else if (historyIndex < messagePromptsUsed.length - 1) {
                        const newIndex = historyIndex + 1;
                        setHistoryIndex(newIndex);
                        imperativeControlPluginRef.current?.setInputText(messagePromptsUsed[newIndex]);
                    }
                }

                event.preventDefault();
                event.stopPropagation();
            } else if (event.key === 'ArrowDown' && (historyIndex >= 0 || showShortcutLists || selectedShortcut)) {
                if (selectedShortcut === Shortcut.Incident) {
                    focusIncident(false);
                } else if (selectedShortcut === Shortcut.Resource) {
                    focusResource(false);
                } else if (selectedShortcut === Shortcut.Agent) {
                    focusExtendedAgent(false);
                } else if (showShortcutLists) {
                    focusShortcut(false);
                } else {
                    if (historyIndex > 0) {
                        const newIndex = historyIndex - 1;
                        setHistoryIndex(newIndex);
                        imperativeControlPluginRef.current?.setInputText(messagePromptsUsed[newIndex]);
                    } else {
                        setHistoryIndex(-1);
                        imperativeControlPluginRef.current?.setInputText(originalInput);
                        setOriginalInput('');
                    }
                }

                event.preventDefault();
                event.stopPropagation();
            }
        },
        [
            chatInputHandleSendClick,
            historyIndex,
            messagePromptsUsed,
            originalInput,
            showShortcutLists,
            onSelectShortcut,
            matchedShortcuts,
            selectedShortcut,
            threads,
            onSelectIncident,
            resourcesSearchResult,
            onSelectResource,
            filteredExtendedAgents,
            onSelectExtendedAgent,
        ]
    );

    const incidentColumns = useMemo(() => {
        return [
            { columnKey: 'id', label: intl.formatMessage(IncidentManagementResources.incidentId) },
            { columnKey: 'title', label: intl.formatMessage(IncidentManagementResources.incidentTitle) },
        ];
    }, [intl]);

    const resourceColumns = useMemo(() => {
        return [
            { columnKey: 'name', label: intl.formatMessage(SreAgentResources.name) },
            { columnKey: 'type', label: intl.formatMessage(SreAgentResources.resourceType) },
            { columnKey: 'resource_group', label: intl.formatMessage(SreAgentResources.resourceGroup) },
            { columnKey: 'subscription_id', label: intl.formatMessage(SreAgentResources.subscriptionId) },
        ];
    }, [intl]);

    const getShortcutDescription = (shortcut: Shortcut) => {
        switch (shortcut) {
            case Shortcut.Agent:
                return intl.formatMessage(ActivitiesResources.extendedAgentShortcutDescription);
            case Shortcut.Clear:
                return intl.formatMessage(ActivitiesResources.clearShortcutDescription);
            case Shortcut.Compact:
                return intl.formatMessage(ActivitiesResources.compactShortcutDescription);
            case Shortcut.Incident:
                return intl.formatMessage(ActivitiesResources.incidentsShortcutDescription);
            case Shortcut.Resource:
                return intl.formatMessage(ActivitiesResources.resourceShortcutDescription);
            case Shortcut.Remember:
                return intl.formatMessage(ActivitiesResources.rememberShortcutDescription);
            case Shortcut.Retrieve:
                return intl.formatMessage(ActivitiesResources.retrieveShortcutDescription);
            default:
                return '';
        }
    };

    const getShortcutPlaceholderText = (shortcut: Shortcut) => {
        switch (shortcut) {
            case Shortcut.Agent:
                return intl.formatMessage(ActivitiesResources.extendedAgentShortcutPlaceholder);
            case Shortcut.Incident:
                return intl.formatMessage(ActivitiesResources.incidentsShortcutPlaceholer);
            case Shortcut.Resource:
                return intl.formatMessage(ActivitiesResources.resourceShortcutPlaceholder);
            case Shortcut.Remember:
                return intl.formatMessage(ActivitiesResources.rememberShortcutPlaceholder);
            case Shortcut.Retrieve:
                return intl.formatMessage(ActivitiesResources.retrieveShortcutPlaceholder);
            default:
                return '';
        }
    };

    const handleClearSelectedAgent = useCallback(() => {
        if (lockAgentSelection) {
            return;
        }

        setSelectedAgentName(null);
    }, [lockAgentSelection]);

    const updateSearchText = debounce((text: string) => {
        setSearchText(text.trim());
    }, 300);

    useEffect(() => {
        extendedAgentClient.getExtendedAgents().then(response => {
            if (response.isSuccessful) {
                setExtendedAgents(response.content?.data ?? []);
            }
        });
    }, [sreAgentEndpoint]);

    useEffect(() => {
        if (chatInputRef.current) {
            shortcutMenuPositionRef.current?.setTarget(chatInputRef.current);
            extendedAgentMenuPositionRef.current?.setTarget(chatInputRef.current);
            incidentOrResourcePopoverPositionRef.current?.setTarget(chatInputRef.current);
        }
    }, [chatInputRef, shortcutMenuPositionRef, extendedAgentMenuPositionRef, incidentOrResourcePopoverPositionRef]);

    useEffect(() => {
        const unregister = editorRef.current?.registerCommand(
            SELECTION_CHANGE_COMMAND,
            (_, editor) => {
                editor.update(() => {
                    const selection = $getSelection();

                    if (!$isRangeSelection(selection) || !selection.isCollapsed()) {
                        closeShortcutList();
                        closeShortcutResourcePopover();
                        return;
                    }

                    const { anchor } = selection;

                    if ($isElementNode(anchor.getNode())) {
                        // If the anchor is in between two decorate nodes, we need to check the previous node
                        // on parent level to see if the anchor is right after a shortcut node and the next node to see
                        // if there is a ghost text node right after the anchor
                        const element = anchor.getNode();
                        const shortcutNode = element.getChildAtIndex(anchor.offset - 1);
                        const nextNode = element.getChildAtIndex(anchor.offset + 1);

                        if ($isShortcutNode(shortcutNode)) {
                            // If the anchor is right after a shortcut node, we open the resource popover, remove the placeholder at other places and add a placeholder after the cursor if it is not there already
                            openShortcutResourcePopover($getShortcutValuefromShortcutNode(shortcutNode));
                            if (!$isGhostTextNode(nextNode)) {
                                removeGhostTextNode();
                                // insert an empty space to create a text node and then insert the placeholder after the empty space.
                                // Reset the focus on the empty space
                                const emptySpaceTextNode = $createTextNode('');
                                const shortcut = $getShortcutValuefromShortcutNode(shortcutNode);
                                const shortcutPlaceholder = shortcut ? getShortcutPlaceholderText(shortcut) : '';
                                if (shortcutPlaceholder) {
                                    const ghostTextNode = $createGhostTextNode(Guid.newGuid(), ` ${shortcutPlaceholder}`);
                                    selection.insertNodes([emptySpaceTextNode, ghostTextNode]);
                                    emptySpaceTextNode.select(0, 0);
                                }
                            }
                        } else {
                            // Otherwise close all popovers, convert all short cut nodes to text nodes and remove all placeholder nodes
                            closeShortcutList();
                            closeShortcutResourcePopover();
                            convertShortcutNodeToTextNode();
                            removeGhostTextNode();
                        }
                        return;
                    }

                    if ($isTextNode(anchor.getNode())) {
                        //If the anchor is inside of a text node, then check if there is any text right after
                        // the decorate node that does not start with an empty space
                        const textNode = anchor.getNode();
                        const shortcutNode = textNode.getPreviousSibling();
                        const nextNode = textNode.getNextSibling();

                        const text: string = textNode?.getTextContent() ?? '';
                        const offset = anchor.offset ?? -1;

                        if (offset === -1) {
                            closeShortcutList();
                            closeShortcutResourcePopover();
                            convertShortcutNodeToTextNode();
                            removeGhostTextNode();
                            return;
                        }

                        const textBeforeAnchor = text.substring(0, offset);
                        const isShortcutNode = $isShortcutNode(shortcutNode);
                        const textBeforeAnchorHasLeadingEmptySpace = textBeforeAnchor.length !== 0 && textBeforeAnchor.startsWith(' ');

                        if (isShortcutNode && !textBeforeAnchorHasLeadingEmptySpace) {
                            // We take the string starting after the shortcut node to the first white space as the search string for searching
                            // incidents, resources or external agents
                            openShortcutResourcePopover($getShortcutValuefromShortcutNode(shortcutNode));
                            const endingSpaceIndex = text.indexOf(' ', offset);
                            const searchString = text.substring(0, endingSpaceIndex === -1 ? text.length : endingSpaceIndex);
                            const cleanedSearchString = searchString.replace(/[\u200b\u200c']/g, '');

                            if (cleanedSearchString.length === 0) {
                                // If the search string is empty, then put a placeholder after the shortcut node, remove placeholders from other places if any.
                                if (!$isGhostTextNode(nextNode)) {
                                    removeGhostTextNode();
                                    const emptySpaceTextNode = $createTextNode('');
                                    const ghostTextNode = $createGhostTextNode(Guid.newGuid(), 'Place holder');
                                    selection.insertNodes([emptySpaceTextNode, ghostTextNode]);
                                    emptySpaceTextNode.select(0, 0);
                                }
                            } else {
                                // If the search string after the short cut is not empty, then remove the current placeholder after the cursor if any.
                                removeGhostTextNode();
                            }
                            updateSearchText(cleanedSearchString);
                            return;
                        }

                        const currentText = text.substring(0, offset);
                        const lastSlashIndex = currentText.lastIndexOf('/');
                        const followedBySpace = offset >= text.length || text[offset] === ' ';
                        if (lastSlashIndex !== -1 && followedBySpace) {
                            const textToExamine = currentText.substring(lastSlashIndex + 1);
                            if (
                                textToExamine === '' ||
                                Object.values(Shortcut).some(shortcut => shortcut.toLowerCase().startsWith(textToExamine.toLowerCase()))
                            ) {
                                openShortcutList(textToExamine);
                                closeShortcutResourcePopover();
                            } else {
                                closeShortcutList();
                                closeShortcutResourcePopover();
                            }
                        } else {
                            closeShortcutList();
                            closeShortcutResourcePopover();
                        }

                        convertShortcutNodeToTextNode();
                        removeGhostTextNode();
                    }
                });

                return false;
            },
            COMMAND_PRIORITY_LOW
        );

        return () => {
            unregister?.();
        };
    }, []);

    const popoverPositioningShorthand = useMemo<PositioningProps>(() => ({ position: 'above', align: 'start', offset: 8 }), []);

    return (
        <div className={root}>
            <KnowledgeGraphBuildStatus />
            <div className={mergeStyles(chatInputTextStyles.textFieldContainer as IStyle)} style={{ position: 'relative' }}>
                <DownButton downButtonState={downButtonState} onClick={onClickDownButton} />
                <Popover
                    unstable_disableAutoFocus={true}
                    open={showShortcutLists}
                    positioning={{ positioningRef: shortcutMenuPositionRef, ...popoverPositioningShorthand }}
                >
                    <PopoverSurface style={{ padding: '5px' }}>
                        <MenuList>
                            {matchedShortcuts.map(shortcut => {
                                return (
                                    <MenuItem
                                        id={shortcut}
                                        key={shortcut}
                                        onMouseDown={e => {
                                            e.preventDefault();
                                            onSelectShortcut(shortcut);
                                        }}
                                        aria-selected={shortcut === focusedShortcutRef.current}
                                        subText={getShortcutDescription(shortcut)}
                                        style={
                                            shortcut === focusedShortcutRef.current
                                                ? { border: `2px ${tokens.colorNeutralForeground1Selected} solid` }
                                                : undefined
                                        }
                                    >
                                        <Text weight={'semibold'}>{'/' + shortcut}</Text>
                                    </MenuItem>
                                );
                            })}
                        </MenuList>
                    </PopoverSurface>
                </Popover>
                <Popover
                    unstable_disableAutoFocus={true}
                    open={selectedShortcut === Shortcut.Agent}
                    positioning={{ positioningRef: extendedAgentMenuPositionRef, ...popoverPositioningShorthand }}
                >
                    <PopoverSurface style={{ padding: '5px' }}>
                        {filteredExtendedAgents.length > 0 ? (
                            <MenuList>
                                {filteredExtendedAgents.map(agent => {
                                    return (
                                        <ExtendedAgentMenuItem
                                            key={agent.name}
                                            agent={agent}
                                            onSelectExtendedAgent={onSelectExtendedAgent}
                                            isFocused={agent.name === focusedExtendedAgent?.name}
                                        />
                                    );
                                })}
                            </MenuList>
                        ) : (
                            <NoSearchResultWarning searchText={searchText} isExtendedAgent={true} />
                        )}
                    </PopoverSurface>
                </Popover>
                <Popover
                    inline={true}
                    unstable_disableAutoFocus={true}
                    open={selectedShortcut === Shortcut.Incident || selectedShortcut === Shortcut.Resource}
                    positioning={{ positioningRef: incidentOrResourcePopoverPositionRef, ...popoverPositioningShorthand }}
                >
                    <PopoverSurface className={shortcutStyles.incidentOrResourcePopoverSurface}>
                        {selectedShortcut === Shortcut.Incident ? (
                            <div className={mergeClasses(scrollable)} ref={threadListDivRef} onScroll={onScrollThreadsList}>
                                <Table className={shortcutStyles.incidentOrResourceContainer}>
                                    <IncidentOrResourceTableHeader columns={incidentColumns} />
                                    <TableBody>
                                        {threads.map(thread => (
                                            <IncidentOrResourceRow
                                                key={thread.id}
                                                id={thread.id}
                                                cells={[thread.id, thread.title]}
                                                onClick={() => onSelectIncident(thread)}
                                                isFocused={focusedIncident?.id === thread.id}
                                            />
                                        ))}
                                    </TableBody>
                                </Table>
                                {moreThreadsToLoad && (
                                    <div ref={threadsListIntersectionObserverRef}>
                                        <IncidentOrResourcePopoverLoader />
                                    </div>
                                )}
                                {!isLoadingInitialThreads && !moreThreadsToLoad && threads.length === 0 && (
                                    <NoSearchResultWarning searchText={searchText} isExtendedAgent={false} />
                                )}
                            </div>
                        ) : (
                            <div
                                className={mergeClasses(scrollable)}
                                ref={resourcesSearchResultDivRef}
                                onScroll={onScrollResourceSearchResultDiv}
                            >
                                <Table className={shortcutStyles.incidentOrResourceContainer}>
                                    <IncidentOrResourceTableHeader columns={resourceColumns} />
                                    <TableBody>
                                        {resourcesSearchResult.map(resource => (
                                            <IncidentOrResourceRow
                                                key={resource.resource_id}
                                                id={resource.resource_id}
                                                cells={[
                                                    resource.name || '-',
                                                    getResourceTypeFriendlyName(resource.type) || '-',
                                                    resource.resource_group || '-',
                                                    resource.subscription_id || '-',
                                                ]}
                                                onClick={() => onSelectResource(resource)}
                                                isFocused={focusedResource?.resource_id === resource.resource_id}
                                            />
                                        ))}
                                    </TableBody>
                                </Table>
                                {moreResourcesSearchResultToLoad && (
                                    <div ref={resourceSearchResultPopoverIntersectionObserverRef}>
                                        <IncidentOrResourcePopoverLoader />
                                    </div>
                                )}
                                {!isLoadingInitialResults && !moreResourcesSearchResultToLoad && resourcesSearchResult.length === 0 && (
                                    <NoSearchResultWarning searchText={searchText} isExtendedAgent={false} />
                                )}
                            </div>
                        )}
                    </PopoverSurface>
                </Popover>
                <Dialog
                    modalType="alert"
                    open={showInputDisabledDialog}
                    onOpenChange={(_, data) => {
                        if (!data.open) {
                            setShowInputDisabledDialog(false);
                        }
                    }}
                >
                    <DialogSurface>
                        {inputDisabledMessage}
                        <DialogActions>
                            <Button appearance="primary" style={{ marginLeft: 'auto' }} onClick={() => setShowInputDisabledDialog(false)}>
                                {intl.formatMessage(SreAgentResources.close)}
                            </Button>
                        </DialogActions>
                    </DialogSurface>
                </Dialog>
                <ChatInput
                    {...restoreFocusTargetAttribute}
                    root={{ ref: chatInputRef }}
                    aria-label={intl.formatMessage(ActivitiesResources.chatInputAriaLabel)}
                    placeholderValue={<FormattedMessage {...ActivitiesResources.chatInputPlaceholder} />}
                    customNodes={[ShortcutNode, ResourceNode, GhostTextNode]}
                    contentBefore={
                        <ContentBefore
                            isDeepInvestigationButtonEnabled={isDeepInvestigationButtonEnabled && !disableInputInteraction}
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
                    attachments={
                        <Attachments
                            selectedAgentName={selectedAgentName}
                            isDeepInvestigationTurnedOn={isDeepInvestigationTurnedOn}
                            isDeepInvestigationEnabled={isDeepInvestigationButtonEnabled}
                            handleClearSelectedAgent={handleClearSelectedAgent}
                            handleDisableDeepInvestigation={onClickDeepInvestigationButton}
                            lockAgentSelection={lockAgentSelection}
                        />
                    }
                    maxLength={1000000000}
                    charactersRemainingMessage={undefined}
                    autoFocus={true}
                    disableSend={!canWriteThreads || disableInputInteractionUnforced}
                    isSending={isTyping}
                    onSubmit={(_, data) => chatInputHandleSendClick(data.value)}
                    onStop={cancelStreaming}
                    expandButtonLineVisibilityThreshold={3}
                    onKeyDown={onKeyDown}
                    aria-activedescendant={
                        focusedShortcut ?? focusedIncident?.id ?? focusedResource?.resource_id ?? focusedExtendedAgent?.name ?? undefined
                    }
                >
                    <ImperativeControlPlugin ref={imperativeControlPluginRef} />
                    <LexicalEditorRefPlugin editorRef={editorRef} />
                </ChatInput>
            </div>

            <Text block size={200} align="center" className={mergeStyles(chatStatement)}>
                {intl.formatMessage(SreAgentResources.chatAiContentAndPrivacyMessageStatement)}
            </Text>
        </div>
    );
};

const NoSearchResultWarning = memo((props: { searchText?: string | null; isExtendedAgent: boolean }) => {
    const { root, extendedAgent, incident } = useNoSearchResultWarningStyles();

    return (
        <div className={mergeClasses(root, props.isExtendedAgent ? extendedAgent : incident)}>
            <ChatWarningRegular fontSize={'50px'} />
            <Text weight={'semibold'}>
                {props.searchText ? (
                    <FormattedMessage {...ActivitiesResources.noSearchResultWithSearchText} values={{ searchText: props.searchText }} />
                ) : (
                    <FormattedMessage {...ActivitiesResources.noSearchResult} />
                )}
            </Text>
        </div>
    );
});

const ExtendedAgentMenuItem = memo(
    (props: { agent: ExtendedAgent; onSelectExtendedAgent: (agentName: string) => void; isFocused: boolean }) => {
        const rowRef = useRef<HTMLDivElement>(null);

        useEffect(() => {
            if (props.isFocused) {
                rowRef.current?.scrollIntoView({ block: 'nearest' });
            }
        }, [props.isFocused]);

        return (
            <MenuItem
                id={props.agent.name}
                key={props.agent.name}
                onMouseDown={e => {
                    e.preventDefault();
                    props.onSelectExtendedAgent(props.agent.name);
                }}
                aria-selected={props.isFocused}
                subText={{
                    children: <>{props.agent.instructions}</>,
                    style: {
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        display: '-webkit-box',
                        WebkitLineClamp: 3,
                        WebkitBoxOrient: 'vertical',
                    },
                }}
                style={props.isFocused ? { border: `2px ${tokens.colorNeutralForeground1Selected} solid` } : undefined}
            >
                <Text weight={'semibold'}>{props.agent.name}</Text>
            </MenuItem>
        );
    }
);

const IncidentOrResourceTableHeader = memo(({ columns }: { columns: { columnKey: string; label: string }[] }) => {
    return (
        <TableHeader>
            <TableRow>
                {columns.map(column => (
                    <TableHeaderCell key={column.columnKey}>
                        <Text weight={'semibold'}>{column.label}</Text>
                    </TableHeaderCell>
                ))}
            </TableRow>
        </TableHeader>
    );
});

const IncidentOrResourceRow = memo((props: { id: string; cells: string[]; onClick: () => void; isFocused: boolean }) => {
    const rowRef = useRef<HTMLTableRowElement>(null);

    useEffect(() => {
        if (props.isFocused) {
            rowRef.current?.scrollIntoView({ block: 'nearest' });
        }
    }, [props.isFocused]);

    return (
        <TableRow
            ref={rowRef}
            id={props.id}
            onClick={props.onClick}
            aria-selected={props.isFocused}
            style={props.isFocused ? { border: `2px ${tokens.colorNeutralForeground1Selected} solid` } : undefined}
        >
            {props.cells.map((cell, index) => (
                <TableCell
                    key={index}
                    style={{ wordBreak: 'break-word', padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalS}` }}
                >
                    {cell}
                </TableCell>
            ))}
        </TableRow>
    );
});

const IncidentOrResourcePopoverLoader = () => {
    return (
        <>
            <GroundingMenuItemSkeleton />
            <GroundingMenuItemSkeleton />
            <GroundingMenuItemSkeleton />
        </>
    );
};

const Attachments = memo(
    (props: {
        selectedAgentName?: string | null;
        lockAgentSelection?: boolean;
        isDeepInvestigationTurnedOn: boolean;
        isDeepInvestigationEnabled: boolean;
        handleClearSelectedAgent: () => void;
        handleDisableDeepInvestigation: () => void;
    }) => {
        const intl = useIntl();

        const showAttachmentList = !!props.selectedAgentName || props.isDeepInvestigationTurnedOn;

        return showAttachmentList ? (
            <AttachmentList
                maxVisibleAttachments={3}
                overflowMenuButton={
                    <AttachmentOverflowMenuButton aria-label={intl.formatMessage(ActivitiesResources.removeAttachmentButtonAriaLabel)} />
                }
            >
                {props.selectedAgentName && (
                    <Attachment
                        id={props.selectedAgentName}
                        key={props.selectedAgentName}
                        dismissButton={
                            props.lockAgentSelection
                                ? undefined
                                : {
                                      'aria-label': intl.formatMessage(ActivitiesResources.removeExtendedAgentAriaLabel, {
                                          agentName: props.selectedAgentName,
                                      }),
                                      onClick: () => props.handleClearSelectedAgent(),
                                  }
                        }
                    >
                        <Tooltip
                            content={
                                <FormattedMessage
                                    {...ActivitiesResources.slashCommandExtendedAgentTagLabel}
                                    values={{ agentName: props.selectedAgentName }}
                                />
                            }
                            relationship="label"
                        >
                            <Text weight="semibold" wrap={false}>
                                <FormattedMessage
                                    {...ActivitiesResources.slashCommandExtendedAgentTagLabel}
                                    values={{ agentName: props.selectedAgentName }}
                                />
                            </Text>
                        </Tooltip>
                    </Attachment>
                )}
                {props.isDeepInvestigationTurnedOn && (
                    <Attachment
                        id={'deep-investigation-attachment'}
                        key={'deep-investigation-attachment'}
                        dismissButton={{
                            disabled: !props.isDeepInvestigationEnabled,
                            'aria-label': intl.formatMessage(AgentTaskResources.deepInvestigationNoPermissionTurnedOffMessage),
                            onClick: () => {
                                if (props.isDeepInvestigationEnabled) {
                                    props.handleDisableDeepInvestigation();
                                }
                            },
                            style: props.isDeepInvestigationEnabled ? undefined : { cursor: 'not-allowed', opacity: 0.5 },
                        }}
                    >
                        <Tooltip content={<FormattedMessage {...AgentTaskResources.deepInvestigation} />} relationship="label">
                            <Text weight="semibold" wrap={false}>
                                <FormattedMessage {...AgentTaskResources.deepInvestigation} />
                            </Text>
                        </Tooltip>
                    </Attachment>
                )}
            </AttachmentList>
        ) : null;
    }
);

const ContentBefore = (props: {
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
    const { container, trailingGroup } = useFooterButtonGroupStyles();
    return (
        <div className={container}>
            <DeepInvestigationButton
                isDeepInvestigationButtonEnabled={props.isDeepInvestigationButtonEnabled}
                isDeepInvestigationTurnedOn={props.isDeepInvestigationTurnedOn}
                onClickDeepInvestigationButton={props.onClickDeepInvestigationButton}
            />
            {props.showAgentModeSelector && props.threadId && (
                <AgentModeSelector
                    id={ChatBoxButtonIds.AgentMode}
                    threadId={props.threadId}
                    disabled={props.isTyping || props.disableInputInteraction}
                />
            )}
            <div className={trailingGroup}>
                <PromptLibraryButton
                    isTyping={props.isTyping}
                    disableInputInteraction={props.disableInputInteraction}
                    messagePromptsUsed={props.messagePromptsUsed}
                    sendMessage={props.sendMessage}
                    prompts={props.prompts}
                    threadId={props.threadId}
                    threadSource={props.threadSource}
                />
                <GenerateInsightsButton
                    threadId={props.threadId}
                    isTyping={props.isTyping}
                    disableInputInteraction={props.disableInputInteraction}
                />
            </div>
        </div>
    );
};

const DeepInvestigationButton = memo(
    ({
        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        onClickDeepInvestigationButton,
    }: {
        isDeepInvestigationButtonEnabled: boolean;
        isDeepInvestigationTurnedOn: boolean;
        onClickDeepInvestigationButton: () => void;
    }) => {
        const { canWriteThreads } = usePermissionContext();
        const intl = useIntl();
        const { iconWrapper } = useFooterButtonIconStyles();

        const tooltipContentWhenNoPermission = isDeepInvestigationTurnedOn
            ? intl.formatMessage(AgentTaskResources.deepInvestigationNoPermissionTurnedOnMessage)
            : intl.formatMessage(AgentTaskResources.deepInvestigationNoPermissionTurnedOffMessage);

        return (
            <PermissionedButton
                canPerform={canWriteThreads}
                noPermissionTooltip={canWriteThreads ? '' : tooltipContentWhenNoPermission}
                allowedTooltip={intl.formatMessage(AgentTaskResources.deepInvestigation)}
                icon={
                    <span className={iconWrapper}>
                        <SearchSparkle32Regular />
                    </span>
                }
                disabledReason={!isDeepInvestigationButtonEnabled}
                appearance={'subtle'}
                shape={'rounded'}
                onClick={() => onClickDeepInvestigationButton()}
                style={{ height: '100%' }}
            />
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
        const { iconWrapper } = useFooterButtonIconStyles();

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
                            icon={
                                <span className={iconWrapper}>
                                    <Lightbulb32Regular />
                                </span>
                            }
                            disabled={disableInputInteraction || isTyping}
                            shape="rounded"
                            appearance="subtle"
                            style={{ height: '100%' }}
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
                                <SearchBoxWithDebounce
                                    setSearchTerm={setQuery}
                                    disabled={disableInputInteraction || isTyping}
                                    style={{ maxWidth: 470 }}
                                    textToAnnounce={
                                        filteredCategories.length === 0 ? intl.formatMessage(SreAgentResources.noMatches) : undefined
                                    }
                                    isDirectlyUnderDocumentBody={true}
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
