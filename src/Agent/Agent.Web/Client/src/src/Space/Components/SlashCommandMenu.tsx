import { makeStyles, MenuItem, MenuList, mergeClasses, Text, tokens } from '@fluentui/react-components';
import React, { ForwardedRef, forwardRef, useCallback, useContext, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, PaginatedResponse } from '../Contracts/ExtendedAgentGraph';

const useStyles = makeStyles({
    menu: {
        position: 'absolute',
        bottom: '100%',
        left: '0',
        marginBottom: tokens.spacingVerticalXS,
        minWidth: '220px',
        maxWidth: '300px',
        maxHeight: '240px',
        overflowY: 'auto',
        backgroundColor: tokens.colorNeutralBackground1,
        boxShadow: tokens.shadow16,
        borderRadius: tokens.borderRadiusXLarge,
        zIndex: 1000,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: tokens.spacingVerticalS,
        backdropFilter: 'blur(12px)',
    },
    menuList: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        padding: 0,
    },
    menuItem: {
        cursor: 'pointer',
        display: 'flex',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalS,
        padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalM}`,
        minHeight: '40px',
        borderRadius: tokens.borderRadiusMedium,
        transition: 'background-color 120ms ease, transform 120ms ease',
        color: tokens.colorNeutralForeground1,
    },
    menuItemHighlighted: {
        backgroundColor: tokens.colorNeutralBackground3,
        transform: 'translateX(2px)',
        outline: `1px solid ${tokens.colorNeutralStroke1}`,
        boxShadow: tokens.shadow4,
    },
    menuItemActive: {
        backgroundColor: tokens.colorNeutralBackground2,
        color: tokens.colorNeutralForeground1,
        outline: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    commandText: {
        fontFamily: tokens.fontFamilyMonospace,
        color: tokens.colorNeutralForeground1,
        fontSize: tokens.fontSizeBase200,
    },
    commandTextActive: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
    },
    commandTextHighlighted: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
    },
    description: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase100,
    },
    agentItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        width: '100%',
    },
    agentItemActive: {
        color: tokens.colorNeutralForeground1,
    },
    agentName: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase200,
    },
    agentDescription: {
        fontSize: tokens.fontSizeBase100,
        color: tokens.colorNeutralForeground3,
        lineHeight: tokens.lineHeightBase200,
    },
    agentDescriptionActive: {
        color: tokens.colorNeutralForeground2,
    },
    backButton: {
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
});

interface SlashCommand {
    id: string;
    slashCommand: string;
    action?: () => void;
}

export interface SlashCommandMenuProps {
    isOpen: boolean;
    onClose: () => void;
    onSelectAgent: (agent: ExtendedAgent) => void;
    inputValue: string;
    onInsertCommand?: (command: string) => void;
    onCommandInvoked?: (commandId: string) => void;
    activeCommandId?: string | null;
    selectedAgentName?: string | null;
    onKeyHandlerChange?: (handler: ((event: React.KeyboardEvent) => boolean) | null) => void;
}

export type SlashCommandMenuHandle = {
    handleKeyDown: (event: React.KeyboardEvent) => boolean;
};

type NavigableKeyEvent = Pick<KeyboardEvent, 'key' | 'preventDefault' | 'stopPropagation'>;

export const SlashCommandMenu = forwardRef(
    (
        {
            isOpen,
            onClose,
            onSelectAgent,
            inputValue,
            onInsertCommand,
            onCommandInvoked,
            activeCommandId,
            selectedAgentName,
            onKeyHandlerChange,
        }: SlashCommandMenuProps,
        ref: ForwardedRef<SlashCommandMenuHandle>
    ) => {
        const styles = useStyles();
        const { sreAgentEndpoint } = useContext(EnvironmentContext);
        const intl = useIntl();

        const [selectedIndex, setSelectedIndex] = useState(0);
        const [showAgentsList, setShowAgentsList] = useState(false);
        const [agents, setAgents] = useState<ExtendedAgent[]>([]);
        const [loadingAgents, setLoadingAgents] = useState(false);
        const [hasLoadedAgents, setHasLoadedAgents] = useState(false);
        const menuRef = useRef<HTMLDivElement | null>(null);

        const loadAgents = useCallback(async () => {
            setLoadingAgents(true);
            try {
                const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/agents?page=1&limit=200`, {
                    headers: getAgentHeaders(),
                });

                if (!response.ok) {
                    throw new Error(`Failed to fetch agents: ${response.status}`);
                }

                const data: PaginatedResponse<ExtendedAgent> = await response.json();
                setAgents(data.data);
            } catch (err) {
                console.error('Error fetching agents:', err);
                setAgents([]);
            } finally {
                setLoadingAgents(false);
                setHasLoadedAgents(true);
            }
        }, [sreAgentEndpoint]);

        const ensureAgentsLoaded = useCallback(() => {
            if (!hasLoadedAgents && !loadingAgents) {
                loadAgents();
            }
        }, [hasLoadedAgents, loadingAgents, loadAgents]);

        const enterExtendedAgentsMode = useCallback(() => {
            setShowAgentsList(true);
            setSelectedIndex(1);
            ensureAgentsLoaded();
        }, [ensureAgentsLoaded]);

        const commands = useMemo<SlashCommand[]>(
            () => [
                {
                    id: 'extended-agents',
                    slashCommand: '/extended-agents',
                    action: () => {
                        onInsertCommand?.('/extended-agents ');
                        enterExtendedAgentsMode();
                    },
                },
                {
                    id: 'clear-thread',
                    slashCommand: '/clear',
                    action: () => {
                        setShowAgentsList(false);
                        setSelectedIndex(0);
                        onClose();
                        onInsertCommand?.('');
                    },
                },
                {
                    id: 'compact-thread',
                    slashCommand: '/compact',
                    action: () => {
                        setShowAgentsList(false);
                        setSelectedIndex(0);
                        onClose();
                        onInsertCommand?.('');
                    },
                },
            ],
            [enterExtendedAgentsMode, onClose, onInsertCommand]
        );

        const filteredCommands = useMemo(() => {
            const normalizedInput = inputValue.trim().toLowerCase();
            const commandToken = normalizedInput.startsWith('/') ? normalizedInput.slice(1).split(' ')[0] : normalizedInput;

            return commands.filter(cmd => {
                if (!commandToken) return true;
                const normalizedLabel = cmd.slashCommand.toLowerCase().replace('/', '');
                return normalizedLabel.startsWith(commandToken);
            });
        }, [commands, inputValue]);

        // Anchor the prefix removal; keep what's after "/extended-agents"
        const filteredAgents = useMemo(() => {
            const searchTerm = inputValue
                .toLowerCase()
                .replace(/^\/extended-agents\s*/i, '')
                .trim();

            if (!searchTerm) return agents;
            return agents.filter(agent => agent.name.toLowerCase().includes(searchTerm));
        }, [agents, inputValue]);

        // Detect "/extended-agents" mode (no trim; allow with/without trailing space)
        useEffect(() => {
            const raw = inputValue.toLowerCase();
            // Match "/extended-agents" with optional space and any text after
            const isExtendedAgents = /^\/extended-agents(\s.*)?$/.test(raw);

            if (isExtendedAgents) {
                if (!showAgentsList) {
                    enterExtendedAgentsMode();
                    onCommandInvoked?.('extended-agents');
                }
            } else if (showAgentsList) {
                setShowAgentsList(false);
                setSelectedIndex(0);
            }
        }, [enterExtendedAgentsMode, inputValue, showAgentsList, onCommandInvoked]);

        // Load agents when entering the list view (robust against timing)
        useEffect(() => {
            if (showAgentsList) {
                ensureAgentsLoaded();
            }
        }, [ensureAgentsLoaded, showAgentsList]);

        const agentItemCount = useMemo(
            () => (showAgentsList ? Math.max(1, filteredAgents.length + 1) : 0),
            [showAgentsList, filteredAgents.length]
        );

        const handleKeyNavigation = useCallback(
            (event: NavigableKeyEvent) => {
                if (!isOpen) return false;

                const commandCount = filteredCommands.length;
                const itemCount = showAgentsList ? agentItemCount : commandCount;
                if (itemCount === 0) return false;

                if (event.key === 'ArrowDown') {
                    event.preventDefault();
                    event.stopPropagation();
                    setSelectedIndex(prev => (prev + 1) % itemCount);
                    return true;
                }

                if (event.key === 'ArrowUp') {
                    event.preventDefault();
                    event.stopPropagation();
                    setSelectedIndex(prev => (prev - 1 + itemCount) % itemCount);
                    return true;
                }

                if (event.key === 'Enter') {
                    event.preventDefault();
                    event.stopPropagation();

                    if (showAgentsList) {
                        if (selectedIndex === 0) {
                            onInsertCommand?.('/');
                            setShowAgentsList(false);
                            setSelectedIndex(0);
                        } else {
                            const agent = filteredAgents[selectedIndex - 1];
                            if (agent) {
                                onSelectAgent(agent);
                                onCommandInvoked?.('extended-agents');
                                onClose();
                            }
                        }
                    } else {
                        const command = filteredCommands[selectedIndex];
                        if (command) {
                            command.action?.();
                            onCommandInvoked?.(command.id);
                        }
                    }

                    return true;
                }

                if (event.key === 'Escape') {
                    event.preventDefault();
                    event.stopPropagation();

                    if (showAgentsList) {
                        onInsertCommand?.('/');
                        setShowAgentsList(false);
                        setSelectedIndex(0);
                    } else {
                        onClose();
                    }

                    return true;
                }

                return false;
            },
            [
                agentItemCount,
                filteredAgents,
                filteredCommands,
                isOpen,
                onClose,
                onCommandInvoked,
                onInsertCommand,
                onSelectAgent,
                selectedIndex,
                showAgentsList,
            ]
        );

        const imperativeKeyHandler = useCallback(
            (event: React.KeyboardEvent) => handleKeyNavigation(event as unknown as NavigableKeyEvent),
            [handleKeyNavigation]
        );

        useImperativeHandle(ref, () => ({ handleKeyDown: imperativeKeyHandler }), [imperativeKeyHandler]);

        useEffect(() => {
            if (!onKeyHandlerChange) {
                return;
            }

            onKeyHandlerChange(imperativeKeyHandler);
            return () => {
                onKeyHandlerChange(null);
            };
        }, [imperativeKeyHandler, onKeyHandlerChange]);

        // Reset state when menu is closed
        useEffect(() => {
            if (!isOpen) {
                setShowAgentsList(false);
                setSelectedIndex(0);
            }
        }, [isOpen]);

        // Clamp selectedIndex when filtered items change
        useEffect(() => {
            if (!showAgentsList) {
                if (filteredCommands.length > 0 && selectedIndex >= filteredCommands.length) {
                    setSelectedIndex(Math.max(0, filteredCommands.length - 1));
                }
                return;
            }
            const maxSelectableIndex = filteredAgents.length; // 0 = back button; agents are 1..N
            if (selectedIndex > maxSelectableIndex) {
                setSelectedIndex(maxSelectableIndex);
            }
        }, [filteredAgents.length, filteredCommands.length, showAgentsList, selectedIndex]);

        useEffect(() => {
            if (!showAgentsList) {
                return;
            }

            if (filteredAgents.length > 0 && selectedIndex === 0) {
                setSelectedIndex(1);
            }
        }, [filteredAgents.length, showAgentsList, selectedIndex]);

        useEffect(() => {
            const menuElement = menuRef.current;
            if (!menuElement) {
                return;
            }

            const selector = showAgentsList ? `[data-agent-index="${selectedIndex}"]` : `[data-command-index="${selectedIndex}"]`;
            const activeItem = menuElement.querySelector(selector) as HTMLElement | null;
            activeItem?.scrollIntoView({ block: 'nearest' });
        }, [selectedIndex, showAgentsList, filteredAgents.length, filteredCommands.length]);

        if (!isOpen || (!showAgentsList && filteredCommands.length === 0)) {
            return null;
        }

        const handleCommandClick = (command: SlashCommand) => {
            command.action?.();
            onCommandInvoked?.(command.id);
        };

        const handleAgentClick = (agent: ExtendedAgent) => {
            onSelectAgent(agent);
            onCommandInvoked?.('extended-agents');
            onClose();
        };

        const handleBackClick = () => {
            onInsertCommand?.('/');
            setShowAgentsList(false);
            setSelectedIndex(0);
        };

        if (showAgentsList) {
            return (
                <div className={styles.menu} ref={menuRef}>
                    <MenuList className={styles.menuList} role="listbox" aria-label={intl.formatMessage(SreAgentResources.extendedAgents)}>
                        <MenuItem
                            data-agent-index={0}
                            className={`${styles.menuItem} ${styles.backButton} ${selectedIndex === 0 ? styles.menuItemHighlighted : ''}`}
                            role="option"
                            aria-selected={selectedIndex === 0}
                            style={
                                selectedIndex === 0
                                    ? {
                                          backgroundColor: tokens.colorNeutralBackground3,
                                          color: tokens.colorNeutralForeground1,
                                          outline: `1px solid ${tokens.colorNeutralStroke1}`,
                                          boxShadow: tokens.shadow4,
                                      }
                                    : undefined
                            }
                            onClick={handleBackClick}
                        >
                            <Text>{intl.formatMessage(SreAgentResources.backToCommands)}</Text>
                        </MenuItem>

                        {loadingAgents && (
                            <MenuItem className={styles.menuItem}>
                                <Text className={styles.description}>{intl.formatMessage(SreAgentResources.loadingAgents)}</Text>
                            </MenuItem>
                        )}

                        {!loadingAgents && filteredAgents.length === 0 && (
                            <MenuItem className={styles.menuItem}>
                                <Text className={styles.description}>{intl.formatMessage(SreAgentResources.noAgentsFound)}</Text>
                            </MenuItem>
                        )}

                        {!loadingAgents &&
                            filteredAgents.map((agent, index) => {
                                const isHighlighted = selectedIndex === index + 1;
                                const isActive = selectedAgentName === agent.name;
                                const menuItemClass = [
                                    styles.menuItem,
                                    isHighlighted ? styles.menuItemHighlighted : '',
                                    isActive ? styles.menuItemActive : '',
                                ]
                                    .filter(Boolean)
                                    .join(' ');

                                return (
                                    <MenuItem
                                        key={agent.name}
                                        data-agent-index={index + 1}
                                        className={menuItemClass}
                                        role="option"
                                        aria-selected={isHighlighted || isActive}
                                        style={
                                            isHighlighted || isActive
                                                ? {
                                                      backgroundColor: isHighlighted
                                                          ? tokens.colorNeutralBackground3
                                                          : tokens.colorNeutralBackground2,
                                                      color: tokens.colorNeutralForeground1,
                                                      outline: `1px solid ${
                                                          isHighlighted ? tokens.colorNeutralStroke1 : tokens.colorNeutralStroke2
                                                      }`,
                                                      boxShadow: isHighlighted ? tokens.shadow4 : undefined,
                                                  }
                                                : undefined
                                        }
                                        onClick={() => handleAgentClick(agent)}
                                    >
                                        <div style={{ width: '100%' }}>
                                            <div className={`${styles.agentItem} ${isActive ? styles.agentItemActive : ''}`}>
                                                <Text className={styles.agentName}>{agent.name}</Text>
                                                {agent.instructions && (
                                                    <Text
                                                        className={`${styles.agentDescription} ${isActive ? styles.agentDescriptionActive : ''}`}
                                                    >
                                                        {agent.instructions.substring(0, 120)}
                                                        {agent.instructions.length > 120 ? '…' : ''}
                                                    </Text>
                                                )}
                                            </div>
                                        </div>
                                    </MenuItem>
                                );
                            })}
                    </MenuList>
                </div>
            );
        }

        return (
            <div className={styles.menu} ref={menuRef}>
                <MenuList className={styles.menuList} role="listbox" aria-label={intl.formatMessage(SreAgentResources.slashCommands)}>
                    {filteredCommands.map((command, index) => {
                        const isHighlighted = selectedIndex === index;
                        const isActive = activeCommandId === command.id;
                        const commandClass = mergeClasses(styles.menuItem, isActive && styles.menuItemActive);

                        const commandTextClassName = mergeClasses(
                            styles.commandText,
                            (isHighlighted || isActive) && styles.commandTextHighlighted
                        );

                        const commandStyle = isHighlighted
                            ? {
                                  backgroundColor: tokens.colorNeutralBackground3,
                                  color: tokens.colorNeutralForeground1,
                                  outline: `1px solid ${tokens.colorNeutralStroke1}`,
                                  boxShadow: tokens.shadow4,
                              }
                            : isActive
                              ? {
                                    backgroundColor: tokens.colorNeutralBackground2,
                                    color: tokens.colorNeutralForeground1,
                                    outline: `1px solid ${tokens.colorNeutralStroke2}`,
                                }
                              : undefined;

                        return (
                            <MenuItem
                                key={command.id}
                                data-command-index={index}
                                className={commandClass}
                                role="option"
                                aria-selected={isHighlighted}
                                style={commandStyle}
                                onClick={() => handleCommandClick(command)}
                            >
                                <Text className={commandTextClassName}>{command.slashCommand}</Text>
                            </MenuItem>
                        );
                    })}
                </MenuList>
            </div>
        );
    }
);

SlashCommandMenu.displayName = 'SlashCommandMenu';
