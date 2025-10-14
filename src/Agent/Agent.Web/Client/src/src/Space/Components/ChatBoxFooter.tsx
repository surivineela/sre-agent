import { ChatInput } from '@fluentui-copilot/react-chat-input';
import {
    $createTextNode,
    $getSelection,
    $insertNodes,
    $isElementNode,
    $isRangeSelection,
    $isTextNode,
    $setSelection,
    Attachment,
    AttachmentList,
    AttachmentOverflowMenuButton,
    COMMAND_PRIORITY_LOW,
    ElementNode,
    GroundingMenuItemSkeleton,
    ImperativeControlPlugin,
    ImperativeControlPluginRef,
    LexicalEditor,
    LexicalEditorRefPlugin,
    SELECTION_CHANGE_COMMAND,
} from '@fluentui-copilot/react-copilot';
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
    MenuItem,
    MenuList,
    mergeClasses,
    Popover,
    PopoverSurface,
    PositioningImperativeRef,
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
import { Lightbulb32Regular, SearchSparkle32Regular } from '@fluentui/react-icons';
import { IStyle, mergeStyles } from '@fluentui/react/lib/Styling';
import React, { memo, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../Common/Clients/ExtendedAgentClient';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
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
import { AgentContext, StreamingContext } from '../Contracts/Context';
import { ExtendedAgent } from '../Contracts/ExtendedAgentGraph';
import { usePermissionContext } from '../Contracts/PermissionContext';
import { useThreadList } from '../Hooks/useThreadList';
import { chatInputTextStyles, useChatInputStyles, useDialogStyles } from '../Styles/Activities.styles';
import AgentModeSelector from './AgentModeSelector';
import { $createShortcutNode, $getShortcutValuefromShortcutNode, $isShortcutNode, ShortcutNode } from './Chat/ShortcutNode';
import KnowledgeGraphBuildStatus from './KnowledgeGraphBuildStatus';

enum ChatBoxButtonIds {
    DeepInvestigation = 'deep-investigation',
    AgentMode = 'agent-mode',
    PromptLibrary = 'prompt-library',
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
    showDeepInvestigationButton,
    isDeepInvestigationButtonEnabled,
    isDeepInvestigationTurnedOn,
    onClickDeepInvestigationButton,
}: IChatBoxFooterProps) => {
    const intl = useIntl();

    const [historyIndex, setHistoryIndex] = useState<number>(-1);
    const [originalInput, setOriginalInput] = useState<string>('');

    const [showShortcutLists, setShowShortcutLists] = useState<boolean>(false);
    const [selectedShortcut, setSelectedShortcut] = useState<Shortcut | null>(null);
    const [focusedShortcut, setFocusedShortcut] = useState<Shortcut | null>(null);
    const [focusedIncident, setFocusedIncident] = useState<Thread | null>(null);
    const [focusedExtendedAgent, setFocusedExtendedAgent] = useState<string | null>(null);
    const [searchText, setSearchText] = useState<string>('');
    const [matchedShortcutString, setMatchedShortcutString] = useState<string | null>(null);
    const [selectedAgentName, setSelectedAgentName] = useState<string | null>(null);
    const [extendedAgents, setExtendedAgents] = useState<ExtendedAgent[]>([]);

    const showAgentModeSelector = useConfigSetting(SettingNames.ShowAgentModeForThread);
    const { root, chatStatement } = useChatInputStyles();

    const { selectThread } = useContext(AgentContext);
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
    const incidentPopoverPositionRef = useRef<PositioningImperativeRef>(null);
    const focusedShortcutRef = useRef<Shortcut | null>(null);
    const focusedExtendedAgentRef = useRef<string | null>(null);
    const focusedIncidentRef = useRef<Thread | null>(null);

    focusedShortcutRef.current = focusedShortcut;
    focusedExtendedAgentRef.current = focusedExtendedAgent;
    focusedIncidentRef.current = focusedIncident;

    const restoreFocusTargetAttribute = useRestoreFocusTarget();
    const { scrollable } = useScrollableComponentStyles();

    const disableInputInteraction = useMemo(
        () => isLoading || !isConnected || isCancellingStreaming || !canWriteThreads,
        [isLoading, isConnected, isCancellingStreaming, canWriteThreads]
    );

    const matchedShortcuts = useMemo(() => {
        return Object.values(Shortcut).filter(
            shortcut => !matchedShortcutString || shortcut.toLowerCase().startsWith(matchedShortcutString.toLowerCase())
        );
    }, [matchedShortcutString]);

    const openShortcutList = (matchedString: string) => {
        setShowShortcutLists(true);
        setMatchedShortcutString(matchedString);
        closeShortcutResourcePopover();
    };

    const openShortcutResourcePopover = (shortcut?: Shortcut | null) => {
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
        setFocusedExtendedAgent(null);
        setSearchText('');
    };

    const onSelectIncident = useCallback((incident: Thread) => {
        setFocusedIncident(null);
        setSelectedShortcut(null);

        editorRef.current?.update(() => {
            const selection = $getSelection();
            if ($isRangeSelection(selection)) {
                const { anchor } = selection;
                const node: ElementNode = anchor.getNode();
                const prev = node.getPreviousSibling();
                const offset = anchor.offset ?? -1;

                if (!$isTextNode(node) || !$isShortcutNode(prev)) {
                    return;
                }

                prev?.remove();

                const rangeSelection = selection.clone();
                rangeSelection.setTextNodeRange(node, 0, node, offset);
                $setSelection(rangeSelection);
                $insertNodes([$createTextNode(incident.title)]);
            }
        });
    }, []);

    const onSelectExtendedAgent = useCallback((agentName: string) => {
        setFocusedExtendedAgent(null);
        setSelectedShortcut(null);
        setSelectedAgentName(agentName);

        editorRef.current?.update(() => {
            const selection = $getSelection();
            if ($isRangeSelection(selection)) {
                const { anchor } = selection;
                const node: ElementNode = anchor.getNode();
                const prev = node.getPreviousSibling();
                const offset = anchor.offset ?? -1;

                if (!$isTextNode(node) || !$isShortcutNode(prev)) {
                    return;
                }

                prev?.remove();

                const rangeSelection = selection.clone();
                rangeSelection.setTextNodeRange(node, 0, node, offset);
                $setSelection(rangeSelection);
                $insertNodes([$createTextNode('')]);
            }
        });
    }, []);

    const includedSourcesForQueryingIncidents = useMemo(() => [ThreadSource.incident], []);

    const { threads, moreThreadsToLoad, threadListDivRef, intersectionObserverRef, onScroll } = useThreadList(
        undefined,
        [],
        includedSourcesForQueryingIncidents,
        undefined,
        undefined,
        searchText.trim(),
        'modifiedTimestamp'
    );

    const chatInputHandleSendClick = useCallback(
        (input?: string) => {
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
        [disableInputInteraction, isTyping, logAmplitudeControlEvent, selectedAgentName, sendMessage, threadId, threadSource]
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
                case Shortcut.ExtendedAgents:
                case Shortcut.Incident:
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

                            const shortcutNode = $createShortcutNode(`/${shortcut}`, shortcut);
                            const whitespaceNode = $createTextNode(' ');
                            $insertNodes([shortcutNode, whitespaceNode]);
                            whitespaceNode.select(0, 0);
                        }
                    });
                    return;
            }
        },
        [chatInputHandleSendClick, selectThread]
    );

    //ToDo: replace this with server side filtering
    const filteredExtendedAgents = useMemo(() => {
        if (!searchText) return extendedAgents;
        return extendedAgents.filter(agent => agent.name.toLowerCase().includes(searchText.toLowerCase()));
    }, [searchText, extendedAgents]);

    const onKeyDown = useCallback(
        (event: React.KeyboardEvent<HTMLSpanElement>) => {
            const focusShortcut = (arrowUp: boolean) => {
                setFocusedShortcut(prev => {
                    const currentIndex = matchedShortcuts.findIndex(s => s === prev);
                    if (currentIndex === -1) {
                        return arrowUp ? matchedShortcuts[matchedShortcuts.length - 1] : matchedShortcuts[0];
                    } else {
                        const newIndex = (currentIndex + (arrowUp ? -1 : 1) + matchedShortcuts.length) % matchedShortcuts.length;
                        return matchedShortcuts[newIndex];
                    }
                });
            };

            const focusIncident = (arrowUp: boolean) => {
                setFocusedIncident(prev => {
                    if (threads.length === 0) {
                        return null;
                    }

                    const currentIndex = threads.findIndex(t => t.id === prev?.id);
                    if (currentIndex === -1) {
                        return arrowUp ? threads[threads.length - 1] : threads[0];
                    } else {
                        const newIndex = (currentIndex + (arrowUp ? -1 : 1) + threads.length) % threads.length;
                        return threads[newIndex];
                    }
                });
            };

            const focusExtendedAgent = (arrowUp: boolean) => {
                setFocusedExtendedAgent(prev => {
                    if (filteredExtendedAgents.length === 0) {
                        return null;
                    }

                    const currentIndex = filteredExtendedAgents.findIndex(a => a.name === prev);
                    if (currentIndex === -1) {
                        return arrowUp ? filteredExtendedAgents[filteredExtendedAgents.length - 1].name : filteredExtendedAgents[0].name;
                    } else {
                        const newIndex =
                            (currentIndex + (arrowUp ? -1 : 1) + filteredExtendedAgents.length) % filteredExtendedAgents.length;
                        return filteredExtendedAgents[newIndex].name;
                    }
                });
            };

            if (event.key.toLowerCase() === 'g') {
                // Stop the event from propagating to the global shortcuts
                event.stopPropagation();
            } else if (event.key.toLowerCase() === 'enter' && (!event.shiftKey || showShortcutLists || selectedShortcut)) {
                if (selectedShortcut) {
                    if (selectedShortcut === Shortcut.Incident && focusedIncidentRef.current) {
                        onSelectIncident(focusedIncidentRef.current);
                    }

                    if (selectedShortcut === Shortcut.ExtendedAgents && focusedExtendedAgentRef.current) {
                        onSelectExtendedAgent(focusedExtendedAgentRef.current);
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
                } else if (selectedShortcut === Shortcut.ExtendedAgents) {
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
                } else if (selectedShortcut === Shortcut.ExtendedAgents) {
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
            filteredExtendedAgents,
            onSelectExtendedAgent,
        ]
    );
    const columns = useMemo(() => {
        return [
            { columnKey: 'id', label: intl.formatMessage(IncidentManagementResources.alertId) },
            { columnKey: 'title', label: intl.formatMessage(IncidentManagementResources.alertTitle) },
        ];
    }, [intl]);

    const getShortcutSubtext = (shortcut: Shortcut) => {
        switch (shortcut) {
            case Shortcut.ExtendedAgents:
                return intl.formatMessage(ActivitiesResources.extendedAgentShortcutDescription);
            case Shortcut.Clear:
                return intl.formatMessage(ActivitiesResources.clearShortcutDescription);
            case Shortcut.Compact:
                return intl.formatMessage(ActivitiesResources.compactShortcutDescription);
            default:
                return intl.formatMessage(ActivitiesResources.incidentsShortcutDescription);
        }
    };

    const handleClearSelectedAgent = useCallback(() => {
        setSelectedAgentName(null);
    }, []);

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
            incidentPopoverPositionRef.current?.setTarget(chatInputRef.current);
        }
    }, [chatInputRef, shortcutMenuPositionRef, extendedAgentMenuPositionRef, incidentPopoverPositionRef]);

    useEffect(() => {
        const unregister = editorRef.current?.registerCommand(
            SELECTION_CHANGE_COMMAND,
            (_, _editor) => {
                // editor.update(() => {
                const selection = $getSelection();

                if (!$isRangeSelection(selection) || !selection.isCollapsed()) {
                    closeShortcutList();
                    closeShortcutResourcePopover();
                    return false;
                }

                const { anchor } = selection;

                if ($isElementNode(anchor.getNode())) {
                    // If the anchor is in between two decorate nodes, we need to check the previous node
                    // on parent level to see if the anchor is right after a shortcut node
                    const element = anchor.getNode();
                    const shortcutNode = element.getChildAtIndex(anchor.offset - 1);

                    if ($isShortcutNode(shortcutNode)) {
                        // If the anchor is right after a shortcut node, we open the resource popover
                        openShortcutResourcePopover($getShortcutValuefromShortcutNode(shortcutNode));
                    } else {
                        // Otherwise close all popovers
                        closeShortcutList();
                        closeShortcutResourcePopover();
                    }
                    return true;
                }

                if ($isTextNode(anchor.getNode())) {
                    //If the anchor is inside of a text node, then check if there is any text right after
                    // the decorate node that does not start with an empty space
                    const textNode = anchor.getNode();
                    const shortcutNode = textNode.getPreviousSibling();

                    const text: string = textNode?.getTextContent() ?? '';
                    const offset = anchor.offset ?? -1;

                    if (offset === -1) {
                        closeShortcutList();
                        closeShortcutResourcePopover();
                        return true;
                    }

                    const textBeforeAnchor = text.substring(0, offset);
                    const isShortcutNode = $isShortcutNode(shortcutNode);
                    const textBeforeAnchorHasLeadingEmptySpace = textBeforeAnchor.length !== 0 && textBeforeAnchor.startsWith(' ');

                    if (isShortcutNode && !textBeforeAnchorHasLeadingEmptySpace) {
                        // We take the string starting after the shortcut node to the first white space as the search string for searching
                        // incidents or external agents
                        openShortcutResourcePopover($getShortcutValuefromShortcutNode(shortcutNode));
                        const endingSpaceIndex = text.indexOf(' ', offset);
                        const searchString = text.substring(0, endingSpaceIndex === -1 ? text.length : endingSpaceIndex);
                        const cleanedSearchString = searchString.replace(/[\u200b\u200c']/g, '').trim();
                        setSearchText(cleanedSearchString);
                        return true;
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

                    return true;
                }

                return false;
            },
            COMMAND_PRIORITY_LOW
        );

        return () => {
            unregister?.();
        };
    }, []);

    return (
        <div className={root}>
            <KnowledgeGraphBuildStatus />
            <div className={mergeStyles(chatInputTextStyles.textFieldContainer as IStyle)} style={{ position: 'relative' }}>
                <DownButton downButtonState={downButtonState} onClick={onClickDownButton} />
                <Popover
                    unstable_disableAutoFocus={true}
                    open={showShortcutLists}
                    positioning={{ positioningRef: shortcutMenuPositionRef, position: 'above', align: 'start', offset: 8 }}
                >
                    <PopoverSurface style={{ padding: '5px' }}>
                        <MenuList>
                            {matchedShortcuts.map(shortcut => {
                                return (
                                    <MenuItem
                                        key={shortcut}
                                        onMouseDown={e => {
                                            e.preventDefault();
                                            onSelectShortcut(shortcut);
                                        }}
                                        aria-selected={shortcut === focusedShortcutRef.current}
                                        subText={getShortcutSubtext(shortcut)}
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
                    open={selectedShortcut === Shortcut.ExtendedAgents}
                    positioning={{ positioningRef: extendedAgentMenuPositionRef, position: 'above', align: 'start', offset: 8 }}
                >
                    {filteredExtendedAgents.length > 0 ? (
                        <PopoverSurface style={{ padding: '5px' }}>
                            <MenuList>
                                {filteredExtendedAgents.map(agent => {
                                    return (
                                        <ExtendedAgentMenuItem
                                            key={agent.name}
                                            agent={agent}
                                            onSelectExtendedAgent={onSelectExtendedAgent}
                                            isFocused={agent.name === focusedExtendedAgent}
                                        />
                                    );
                                })}
                            </MenuList>
                        </PopoverSurface>
                    ) : (
                        <PopoverSurface style={{ padding: '10px 20px' }}>
                            <Text weight={'semibold'} italic>
                                <FormattedMessage {...ActivitiesResources.emptyExtendedAgentMessages} />
                            </Text>
                        </PopoverSurface>
                    )}
                </Popover>
                <Popover
                    inline={true}
                    unstable_disableAutoFocus={true}
                    open={selectedShortcut === Shortcut.Incident}
                    positioning={{ positioningRef: incidentPopoverPositionRef, position: 'above', align: 'start', offset: 8 }}
                >
                    <PopoverSurface
                        style={{
                            minHeight: '300px',
                            maxHeight: '500px',
                            display: 'flex',
                            flexDirection: 'column',
                            gap: tokens.spacingVerticalL,
                        }}
                    >
                        <div className={mergeClasses(scrollable)} ref={threadListDivRef} onScroll={onScroll}>
                            <Table style={{ minWidth: '510px' }}>
                                <TableHeader>
                                    <TableRow>
                                        {columns.map(column => (
                                            <TableHeaderCell key={column.columnKey}>
                                                <Text weight={'semibold'}>{column.label}</Text>
                                            </TableHeaderCell>
                                        ))}
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {threads.map(thread => (
                                        <ThreadRow
                                            key={thread.id}
                                            thread={thread}
                                            onSelectIncident={onSelectIncident}
                                            isFocused={focusedIncident?.id === thread.id}
                                        />
                                    ))}
                                </TableBody>
                            </Table>
                            {moreThreadsToLoad && (
                                <div ref={intersectionObserverRef}>
                                    <GroundingMenuItemSkeleton />
                                    <GroundingMenuItemSkeleton />
                                    <GroundingMenuItemSkeleton />
                                </div>
                            )}
                        </div>
                    </PopoverSurface>
                </Popover>
                <ChatInput
                    {...restoreFocusTargetAttribute}
                    root={{ ref: chatInputRef }}
                    aria-label={intl.formatMessage(ActivitiesResources.chatInputAriaLabel)}
                    placeholderValue={<FormattedMessage {...ActivitiesResources.chatInputPlaceholder} />}
                    customNodes={[ShortcutNode]}
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
                    attachments={
                        selectedAgentName ? (
                            <Attachments selectedAgentName={selectedAgentName} handleClearSelectedAgent={handleClearSelectedAgent} />
                        ) : undefined
                    }
                    maxLength={1000000000}
                    charactersRemainingMessage={undefined}
                    autoFocus={true}
                    disableSend={!canWriteThreads || disableInputInteraction}
                    isSending={isTyping}
                    onSubmit={(_, data) => chatInputHandleSendClick(data.value)}
                    onStop={cancelStreaming}
                    expandButtonLineVisibilityThreshold={3}
                    onKeyDown={onKeyDown}
                    aria-activedescendant={focusedShortcut ?? focusedIncident?.id ?? undefined}
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

const ThreadRow = memo((props: { thread: Thread; onSelectIncident: (incident: Thread) => void; isFocused: boolean }) => {
    const rowRef = useRef<HTMLTableRowElement>(null);

    useEffect(() => {
        if (props.isFocused) {
            rowRef.current?.scrollIntoView({ block: 'nearest' });
        }
    }, [props.isFocused]);

    return (
        <TableRow
            ref={rowRef}
            key={props.thread.id}
            onClick={() => props.onSelectIncident(props.thread)}
            aria-selected={props.isFocused}
            style={props.isFocused ? { border: `2px ${tokens.colorNeutralForeground1Selected} solid` } : undefined}
        >
            <TableCell>{props.thread.id}</TableCell>
            <TableCell>{props.thread.title}</TableCell>
        </TableRow>
    );
});

const Attachments = memo((props: { selectedAgentName: string; handleClearSelectedAgent: () => void }) => {
    const intl = useIntl();

    return (
        <AttachmentList
            maxVisibleAttachments={3}
            overflowMenuButton={
                <AttachmentOverflowMenuButton aria-label={intl.formatMessage(ActivitiesResources.removeAttachmentButtonAriaLabel)} />
            }
        >
            <Attachment
                key={props.selectedAgentName}
                dismissButton={{
                    'aria-label': intl.formatMessage(ActivitiesResources.removeExtendedAgentAriaLabel, {
                        agentName: props.selectedAgentName,
                    }),
                    onClick: () => props.handleClearSelectedAgent(),
                }}
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
        </AttachmentList>
    );
});

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
        <>
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
        </>
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
                    style={{ marginRight: tokens.spacingHorizontalS, height: '100%' }}
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
