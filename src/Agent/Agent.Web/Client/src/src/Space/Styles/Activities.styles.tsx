import { GriffelStyle, makeStyles, tokens } from '@fluentui/react-components';
import { IButtonStyles } from '@fluentui/react/lib/Button';
import { IStyle, mergeStyleSets } from '@fluentui/react/lib/Styling';
import { useTheme } from '@fluentui/react/lib/Theme';
import { CSSProperties } from 'react';

export const ThreadItemHeightInPx = 18;
export const ThreadItemPaddingTopBottomInPx = 8;
export const ThreadTitleHeight = 50;

// Note: Shared navigation styles (list item hover/selection) are now in Navigation.styles.tsx

export const activitiesStylesRoot: CSSProperties = {
    display: 'flex',
    justifyContent: 'flex-start',
    alignItems: 'stretch',
    overflow: 'hidden',
    borderTop: '1px solid rgba(204,204,204,.8)',
    backgroundColor: tokens.colorNeutralBackground3,
    height: 'calc(100vh - 44px)',
};

export const getExpandCollapseButtonStyles = (position: 'left' | 'right') => {
    const continerMarginLeft = position === 'left' ? undefined : 'auto';
    const buttonMargin = position === 'left' ? 'auto auto auto 10px' : 'auto 0px auto auto';

    return {
        container: {
            marginLeft: continerMarginLeft,
            height: '50px',
            display: 'flex',
        },
        button: {
            maxHeight: 'fit-content',
            maxWidth: 'fit-content',
            margin: buttonMargin,
        },
    };
};

export const ThreadContentStyles = mergeStyleSets({
    root: {
        flex: '1 1 auto',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'stretch',
        paddingLeft: '2px',
        overflowY: 'hidden',
        fontSize: '16px',
        lineHeight: '22px',
        height: '100%',
    },
    titleContainer: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'center',
        gap: '10px',
        height: `${ThreadTitleHeight}px`,
    },
    title: {
        lineHeight: '24px',
        paddingBottom: '-10px',
        marginBlock: '0px 0px',
    },
    todoPlanContainer: {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        borderLeft: `1px solid ${tokens.colorNeutralStroke2}`,
    },
});

export const useChatBoxStyles = makeStyles({
    userBubble: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorNeutralForeground1,
        borderRadius: tokens.borderRadiusXLarge,
        paddingTop: '0px',
        paddingBottom: '0px',
        paddingRight: '0px',
        maxWidth: '90%',
        display: 'inline-block',
    },
    userName: {
        margin: '3px 0px 3px 3px',
    },
    userBubbleMessage: {
        padding: '0px 16px 0px 0px',
        backgroundColor: 'inherit',
    },
    modePill: {
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground1,
        borderRadius: '12px',
        fontSize: '12px',
        padding: '0 6px',
        fontWeight: 500,
    },
});

export const ChatBoxStyles = mergeStyleSets({
    chatBox: {
        height: '100%',
        borderRadius: tokens.borderRadiusXLarge,
        minWidth: '300px',
        marginRight: '4px',
        boxShadow: tokens.shadow4,
        marginBottom: '5px',
    },
    chatBoxInner: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        alignItems: 'stretch',
        height: '100%',
        fontSize: '16px',
        backgroundColor: tokens.colorNeutralForegroundInverted,
        borderRadius: tokens.borderRadiusXLarge,
        boxShadow: tokens.shadow4,
        selectors: {
            // Allegedly styles on the below get copied to anything that portals within it (Dialogs, etc)
            '&[data-portal-node="true"]': {
                height: 'auto',
                width: 'auto',
                padding: 0,
            },
        },
    },
    chatContainer: {
        height: '100%',
        padding: '20px 10px 0px 20px',
        borderRadius: tokens.borderRadiusLarge,
    },
    chat: {
        height: '100%',
        maxWidth: '1000px',
        margin: 'auto',
    },
    userMessage: {
        wordBreak: 'normal',
        overflowWrap: 'anywhere',
        whiteSpace: 'normal',
        fontSize: '14px',
        lineHeight: '20px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-end',
    },
    agentMessage: {
        fontSize: '14px',
        lineHeight: '20px',
        '.fai-CopilotMessage__content': {
            width: '90%',
        },
    },
    hideAgentMessageHeader: {
        '.fai-CopilotMessage__accessibleHeading': {
            display: 'none',
        },
        '.fai-CopilotMessage__avatar': {
            display: 'none',
        },
        '.fai-CopilotMessage__name': {
            display: 'none',
        },
    },
});

export interface ChatBoxSidePanelStyleProps {
    root?: GriffelStyle;
}

export const getChatBoxSidePanelStyles = (override?: ChatBoxSidePanelStyleProps) =>
    mergeStyleSets({
        root: {
            backgroundColor: tokens.colorNeutralBackground1,
            height: '100%',
            flex: '1 0 auto',
            borderTopRightRadius: tokens.borderRadiusXLarge,
            borderBottomRightRadius: tokens.borderRadiusXLarge,
            position: 'relative',
            ...override?.root,
        },
        resizer: {
            width: '2px',
            height: '100%',
            position: 'absolute',
            top: 0,
            left: 0,
            bottom: 0,
            cursor: 'col-resize',
            border: 'none',
            minWidth: '0px',

            '&:before': {
                width: '2px',
                content: '""',
                position: 'absolute',
                borderLeft: `1px solid ${tokens.colorNeutralBackground5}`,
                height: '100%',
            },
            ':hover': {
                cursor: 'col-resize',
            },
            ':hover:active': {
                cursor: 'col-resize',
                userSelect: 'none',
            },
        },
    });

export interface ChatBoxStyleProps {
    rootStyle?: CSSProperties;
    chatBoxAndAgentTask?: GriffelStyle;
    chatBox?: GriffelStyle;
    chatBoxInner?: GriffelStyle;
    chatContainer?: GriffelStyle;
    chat?: GriffelStyle;
    userMessage?: GriffelStyle;
    agentMessage?: GriffelStyle;
    hideAgentMessageHeader?: GriffelStyle;
}

export const getChatBoxStyles = (sidePanelVisible?: boolean, overrides?: ChatBoxStyleProps) =>
    mergeStyleSets({
        chatBoxAndAgentTask: {
            display: 'flex',
            width: 'calc(100% - 5px)',
            height: '100%',
            boxShadow: tokens.shadow4,
            borderRadius: tokens.borderRadiusXLarge,
            marginBottom: '5px',
            ...overrides?.chatBoxAndAgentTask,
        },
        chatBox: {
            height: '100%',
            minWidth: '300px',
            width: '100%',
            ...overrides?.chatBox,
        },
        chatBoxInner: {
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'space-between',
            alignItems: 'stretch',
            height: '100%',
            fontSize: '16px',
            backgroundColor: `${tokens.colorNeutralForegroundInverted} !important`,
            borderTopLeftRadius: tokens.borderRadiusXLarge,
            borderBottomLeftRadius: tokens.borderRadiusXLarge,
            borderTopRightRadius: sidePanelVisible ? 0 : tokens.borderRadiusXLarge,
            borderBottomRightRadius: sidePanelVisible ? 0 : tokens.borderRadiusXLarge,
            selectors: {
                // Allegedly styles on the below get copied to anything that portals within it (Dialogs, etc)
                '&[data-portal-node="true"]': {
                    height: 'auto',
                    width: 'auto',
                    padding: 0,
                },
            },
            ...overrides?.chatBoxInner,
        },
        chatContainer: {
            height: '100%',
            padding: '20px 10px 0px 20px',
            borderRadius: tokens.borderRadiusLarge,
            ...overrides?.chatContainer,
        },
        chat: {
            height: '100%',
            maxWidth: '1000px',
            margin: 'auto',
            ...overrides?.chat,
        },
        userMessage: {
            wordBreak: 'normal',
            overflowWrap: 'anywhere',
            whiteSpace: 'normal',
            fontSize: '14px',
            lineHeight: '240x',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'flex-end',
            ...overrides?.userMessage,
        },
        agentMessage: {
            fontSize: '14px',
            lineHeight: '20px',
            '.fai-CopilotMessage__content': {
                width: '90%',
                gap: `${tokens.spacingVerticalS}`,
            },
            ...overrides?.agentMessage,
        },
        hideAgentMessageHeader: {
            '.fai-CopilotMessage__accessibleHeading': {
                display: 'none',
            },
            '.fai-CopilotMessage__avatar': {
                display: 'none',
            },
            '.fai-CopilotMessage__name': {
                display: 'none',
            },
            ...overrides?.hideAgentMessageHeader,
        },
    });

const textFieldMaxWidth = '1000px';

export const useChatInputStyles = makeStyles({
    root: {
        flex: '0 0 auto',
        margin: '5px 0px',
        padding: '0px 20px',
    },
    chatStatement: {
        color: tokens.colorNeutralForeground3,
        maxWidth: textFieldMaxWidth,
        margin: 'auto',
    },
    selectedCommandRow: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        margin: `${tokens.spacingVerticalXS} auto`,
        maxWidth: textFieldMaxWidth,
    },
    promptMenuPopover: {
        width: '280px',
        padding: '10px',
    },
    promptItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        cursor: 'pointer',
        padding: '4px 0',
        fontSize: '13px',
    },
    sectionHeader: {
        fontWeight: 500,
        fontSize: '13px',
        marginBottom: '4px',
        paddingLeft: '2px',
    },
    lightbulbIcon: {
        fontSize: '16px',
        flexShrink: 0,
    },
});

export const chatInputTextStyles = {
    textFieldContainer: {
        maxWidth: textFieldMaxWidth,
        margin: 'auto',
        marginBottom: '8px',
        padding: '12px 8px 6px 8px',
        position: 'relative',
    },
};

export const useDialogStyles = makeStyles({
    dialogSurface: {
        width: '950px',
        maxWidth: '95vw',
        display: 'flex',
        flexDirection: 'column',
        gap: '15px',
        maxHeight: '750px',
        overflowX: 'auto',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        gap: '15px',
        overflow: 'hidden',
        minWidth: '0',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '15px',
        maxWidth: '100%',
        overflowX: 'auto',
    },
});

export const sendButtonStyles = {
    borderRadius: '4px',
    padding: '6px',
    flex: '0 0 auto',
    minWidth: '10px',
    paddingRight: '0px',
};

export const useThreadMenuStyle = () => {
    const root: IStyle = {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        maxWidth: '100%',
        position: 'absolute',
        left: '0',
        right: '0',
    };

    const threadListContainer: IStyle = {
        padding: '0px 10px',
        flex: 1,
    };

    const threadItem: IStyle = {
        padding: `${ThreadItemPaddingTopBottomInPx}px`,
        paddingLeft: `${ThreadItemPaddingTopBottomInPx + 10}px`, // Additional 10px left padding for text
        cursor: 'pointer',
        height: `${ThreadItemHeightInPx}px`,
        willChange: 'transform',
        marginTop: '5px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        position: 'relative',
    };

    const activeThreadItem: IStyle = {
        backgroundColor: tokens.colorNeutralBackground1, // Lightest - selected state
        borderRadius: '16px', // Match hover style rounded corners
        border: `1px solid ${tokens.colorNeutralStroke2}`, // Match hover border color
    };

    const newItemButtonAndSearchBox: IStyle = {
        display: 'flex',
        flexDirection: 'row',
        gap: '10px',
        padding: '0px 15px',
    };

    const hoveredThreadItem: IStyle = {
        backgroundColor: tokens.colorNeutralBackground2, // Lighter than base background
        borderRadius: '16px', // Very round corners to match accordion headers
        border: `1px solid ${tokens.colorNeutralStroke2}`, // Subtle border for depth
    };

    const borderIndicator = {
        marginLeft: '-10px',
        height: '20px',
        width: '3px',
        backgroundColor: tokens.colorBrandForeground1,
        borderRadius: '6px',
        flexShrink: 0,
        position: 'absolute',
    };

    const content = {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        width: '100%',
    };

    return mergeStyleSets({
        root,
        threadListContainer,
        threadItem,
        activeThreadItem,
        hoveredThreadItem,
        borderIndicator,
        newItemButtonAndSearchBox,
        content,
    });
};

export const useCommandButtonStyles = (): IButtonStyles => {
    const colors = useTheme().semanticColors;

    return {
        root: {
            backgroundColor: colors.buttonBackgroundHovered,
        },
        rootHovered: {
            backgroundColor: colors.buttonBackgroundChecked,
        },
        rootChecked: {
            backgroundColor: colors.buttonBackgroundChecked,
        },
    };
};

export const useThreadActionsStyles = makeStyles({
    root: {
        display: 'contents',
    },
    content: {
        display: 'contents',
        paddingLeft: '5px',
        paddingRight: '10px',
    },
    actionsList: {
        height: 'calc(100vh - 220px)',
        overflowX: 'hidden',
        overflowY: 'auto',
    },
    title: {
        lineHeight: '22px',
        marginLeft: '10px',
        paddingBottom: '-10px',
        fontWeight: 600,
    },
    card: {
        minWidth: '265px',
        margin: '10px 5px',
    },
    cardHeader: {
        fontWeight: '550px',
        wordBreak: 'break-word',
    },
    pendingIcon: {
        backgroundColor: tokens.colorPaletteBlueBorderActive,
        borderRadius: tokens.borderRadiusCircular,
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        width: '16px',
        height: '16px',
    },
    completedIcon: {
        backgroundColor: tokens.colorPaletteLightGreenForeground1,
        borderRadius: tokens.borderRadiusCircular,
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        width: '16px',
    },
    errorIcon: {
        backgroundColor: tokens.colorPaletteRedBackground3,
        borderRadius: tokens.borderRadiusCircular,
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        width: '16px',
    },
    iconStatusRow: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'center',
        gap: '4px',
    },
});

export const actionSearchBoxStyle: CSSProperties = {
    margin: '0px 10px',
    borderRadius: tokens.borderRadiusLarge,
    maxWidth: '100%',
};

export const shimmerStyle: CSSProperties = {
    maxWidth: '100%',
    paddingLeft: '10px',
    paddingRight: '10px',
};

export const skeletonStyle: CSSProperties = {
    paddingLeft: '15px',
    height: `${ThreadItemHeightInPx}px`,
    display: 'flex',
    flexDirection: 'column',
    overflow: 'hidden',
    gap: '5px',
    margin: '20px 0px',
};

export const nameAndTimestampContainerStyle: CSSProperties = {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
};
