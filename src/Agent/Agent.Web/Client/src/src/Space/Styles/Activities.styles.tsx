import { makeStyles, tokens } from '@fluentui/react-components';
import { IButtonStyles } from '@fluentui/react/lib/Button';
import { IStyle, mergeStyleSets } from '@fluentui/react/lib/Styling';
import { useTheme } from '@fluentui/react/lib/Theme';
import { CSSProperties } from 'react';

export const ThreadItemHeightInPx = 40;
export const ThreadItemPaddingTopBottomInPx = 10;

export const activitiesStylesRoot: CSSProperties = {
    display: 'flex',
    justifyContent: 'flex-start',
    alignItems: 'flex-start',
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
    },
    titleContainer: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'center',
        gap: '10px',
        height: '50px',
    },
    title: {
        fontWeight: 600,
        lineHeight: '24px',
        paddingBottom: '-10px',
        fontSize: '18px',
        marginBlock: '0px 0px',
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
        height: 'calc(100vh - 25px)',
        borderRadius: tokens.borderRadiusXLarge,
        minWidth: '300px',
        marginRight: '4px',
        boxShadow: tokens.shadow4,
    },
    chatBoxInner: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        alignItems: 'stretch',
        height: 'calc(95% - 15px)',
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

export const ChatBoxV2Styles = mergeStyleSets({
    chatBox: {
        height: 'calc(100vh - 25px)',
        borderRadius: tokens.borderRadiusXLarge,
        minWidth: '300px',
        marginRight: '4px',
        boxShadow: tokens.shadow4,
    },
    chatBoxInner: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        alignItems: 'stretch',
        height: 'calc(95% - 15px)',
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
        lineHeight: '240x',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'flex-end',
    },
    agentMessage: {
        fontSize: '14px',
        lineHeight: '20px',
        '.fai-CopilotMessage__content': {
            width: '90%',
            gap: `${tokens.spacingVerticalL}`,
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

const textFieldMaxWidth = '1000px';

export const useChatInputStyles = makeStyles({
    root: {
        flex: '0 0 auto',
        marginTop: '20px',
        marginBottom: '20px',
        padding: '0px 20px',
    },
    footer: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    subFooter: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    chatStatement: {
        color: tokens.colorNeutralForeground3,
        maxWidth: textFieldMaxWidth,
        margin: 'auto',
    },
    popoverSurface: {
        width: '280px',
        padding: '10px',
        display: 'flex',
        flexDirection: 'column',
        boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
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
    sectionDivider: {
        margin: '8px 0px',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    lightbulbIcon: {
        fontSize: '16px',
        flexShrink: 0,
    },
});

export const chatInputTextStyles = {
    textFieldContainer: {
        borderColor: tokens.colorNeutralStroke1,
        borderStyle: 'solid',
        borderRadius: 6,
        borderWidth: '1px',
        maxWidth: textFieldMaxWidth,
        margin: 'auto',
        marginBottom: '8px',
        padding: '12px 8px 6px 8px',
        position: 'relative',
    },
    textField: {
        fieldGroup: {
            minHeight: 'auto',
        },
        field: {
            maxHeight: '60px',
            overflowX: 'hidden',
            overflowY: 'auto',
            padding: 0,
            marginBottom: '8px',
            fontSize: '14px',
            '::placeholder, :-ms-input-placeholder, ::-ms-input-placeholder': {
                color: `${tokens.colorNeutralForeground4} !important`,
                opacity: '1 !important', // Firefox adds a lower opacity to the placeholder, so we use opacity: 1 to fix this.,
                fontStyle: 'normal !important',
            },
        },
    },
};

export const sendButtonStyles = {
    borderRadius: '4px',
    padding: '6px',
};

export const useThreadMenuStyle = (collapsed?: boolean) => {
    const root: IStyle = {
        flex: collapsed ? '0 0 0%' : '0 0 20%',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'stretch',
        gap: '10px',
        backgroundColor: tokens.colorNeutralBackground3,
        paddingLeft: '10px',
    };

    const threadListContainer: IStyle = {
        maxWidth: '100%',
        padding: '0px 10px',
    };

    const threadList: IStyle = {
        position: 'absolute',
        height: 'calc(100vh - 278px)',
        left: '0px',
        right: '0px',
    };

    const threadItem: IStyle = {
        padding: `${ThreadItemPaddingTopBottomInPx}px 0px`,
        paddingLeft: '10px',
        cursor: 'pointer',
        height: `${ThreadItemHeightInPx}px`,
    };

    const activeThreadItem: IStyle = {
        backgroundColor: tokens.colorNeutralBackground1Selected,
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '6px',
        borderRadius: '3px',
    };

    const hoveredThreadItem: IStyle = {
        backgroundColor: tokens.colorNeutralBackground1Selected,
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '6px',
        borderRadius: '3px',
    };

    const borderIndicator = {
        marginLeft: '-10px',
        height: '32px',
        width: '4px',
        backgroundColor: tokens.colorBrandForeground1,
        borderRadius: '6px',
        flexShrink: 0,
    };
    const content = {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
    };

    return mergeStyleSets({
        root,
        threadListContainer,
        threadList,
        threadItem,
        activeThreadItem,
        hoveredThreadItem,
        borderIndicator,
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

export const searchBoxStyle: CSSProperties = {
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
    paddingLeft: '10px',
};

export const nameAndTimestampContainerStyle: CSSProperties = {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
};
