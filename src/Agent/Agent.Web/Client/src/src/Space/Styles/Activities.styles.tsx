import { makeStyles, tokens } from '@fluentui/react-components';
import { IButtonStyles } from '@fluentui/react/lib/Button';
import { getTheme, IStyle, mergeStyleSets } from '@fluentui/react/lib/Styling';
import { useTheme } from '@fluentui/react/lib/Theme';
import { CSSProperties } from 'react';

export const activitiesStylesRoot: CSSProperties = {
    display: 'flex',
    justifyContent: 'flex-start',
    alignItems: 'flex-start',
    overflow: 'hidden',
    borderTop: '1px solid rgba(204,204,204,.8)',
    backgroundColor: tokens.colorNeutralBackground3,
    height: 'calc(100vh - 44px)',
};

export const ThreadContentStyles = mergeStyleSets({
    root: {
        flex: '1 1 auto',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'stretch',
        paddingLeft: '20px',
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
    },
    title: {
        fontWeight: 600,
        lineHeight: '22px',
        paddingBottom: '-10px',
        fontSize: '18px',
    },
});

export const useChatBoxStyles = makeStyles({
    userBubble: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorNeutralForeground1,
        borderRadius: tokens.borderRadiusXLarge,
        padding: '0px 16px',
        maxWidth: 'fit-content',
        display: 'inline-block',
    },
    userName: {
        margin: '3px 0px 3px 3px',
    },
    userBubbleMessage: {
        padding: '0px 16px 0px 0px',
    },
});

export const ChatBoxStyles = mergeStyleSets({
    chatBox: {
        padding: '0px',
        paddingRight: '10px',
        height: 'calc(100vh - 25px)',
        borderRadius: tokens.borderRadiusXLarge,
        minWidth: '300px',
    },
    root: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        alignItems: 'stretch',
        height: 'calc(95% - 45px)',
        padding: '20px',
        fontSize: '16px',
        backgroundColor: tokens.colorNeutralForegroundInverted,
        borderRadius: tokens.borderRadiusXLarge,
        position: 'relative',
    },
    chatContainer: {
        height: '100%',
        overflowX: 'hidden',
        overflowY: 'auto',
        paddingTop: '10px',
        paddingRight: '5px',
        borderRadius: tokens.borderRadiusLarge,
    },
    chat: {
        height: '100%',
        margin: 'auto',
    },
    userMessage: {
        alignSelf: 'flex-end',
        wordBreak: 'normal',
        overflowWrap: 'anywhere',
        whiteSpace: 'normal',
        fontSize: '16px',
        lineHeight: '24px',
    },
    agentMessage: {
        fontSize: '16px',
        lineHeight: '24px',
        '.fai-CopilotMessage__content': {
            width: '90%',
        },
    },
    arrowDownButton: {
        position: 'absolute',
        bottom: '195px',
        left: '50%',
        zIndex: '1',
        borderRadius: '50% !important',
        opacity: '1',
        transition: 'opacity 0.3s ease',
        pointerEvents: 'auto',
    },
    hiddenButton: {
        opacity: '0',
        pointerEvents: 'none',
    },
});

const textFieldMaxWidth = '1000px';

export const useChatInputStyles = makeStyles({
    root: {
        flex: '0 0 auto',
        marginTop: '20px',
        marginBottom: '20px',
    },
    footer: {
        display: 'flex',
        justifyContent: 'flex-end',
    },
    chatStatement: {
        color: tokens.colorNeutralForeground3,
        maxWidth: textFieldMaxWidth,
        margin: 'auto',
    },
});

export const useChatInputTextStyles = () => {
    const colors = getTheme().semanticColors;

    return {
        textFieldContainer: {
            borderColor: colors.buttonBackgroundCheckedHovered,
            borderStyle: 'solid',
            borderRadius: 3,
            borderWidth: '2px',
            maxWidth: textFieldMaxWidth,
            margin: 'auto',
            marginBottom: '8px',
        },
        textField: {
            field: {
                maxHeight: '60px',
                minHeight: '25px',
                overflowX: 'hidden',
                overflowY: 'auto',
                '::placeholder, :-ms-input-placeholder, ::-ms-input-placeholder': {
                    color: colors.inputPlaceholderText,
                    opacity: 1, // Firefox adds a lower opacity to the placeholder, so we use opacity: 1 to fix this.,
                },
            },
        },
    };
};

export const useThreadMenuStyle = () => {
    const root: IStyle = {
        flex: '0 0 20%',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'stretch',
        gap: '10px',
        backgroundColor: tokens.colorNeutralBackground3,
        paddingTop: '50px',
        paddingLeft: '20px',
        maxWidth: '300px',
    };

    const threadList: IStyle = {
        height: 'calc(100vh - 278px)',
        overflowX: 'hidden',
        overflowY: 'auto',
        maxWidth: '300px',
    };

    const searchBox: IStyle = {
        margin: '0px 10px',
        borderRadius: tokens.borderRadiusLarge,
        width: '280px',
    };

    const threadItem: IStyle = {
        padding: '10px 0px',
        paddingLeft: '10px',
        cursor: 'pointer',
    };

    const activeThreadItem: IStyle = {
        borderLeftStyle: 'solid',
        borderLeftWidth: '2px',
        borderLeftHeight: '4px',
        borderLeftColor: tokens.colorNeutralForeground2BrandSelected,
        boxSizing: 'border-box',
        backgroundColor: tokens.colorNeutralBackground3Selected,
        borderRadius: tokens.borderRadiusLarge,
    };

    return mergeStyleSets({
        root,
        threadList,
        searchBox,
        threadItem,
        activeThreadItem,
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
        maxWidth: '300px',
    },
    content: {
        flex: '0 0 20%',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'stretch',
        gap: '10px',
        backgroundColor: tokens.colorNeutralBackground3,
        paddingTop: '42px',
        paddingLeft: '5px',
        paddingRight: '10px',
    },
    actionsList: {
        maxWidth: '500px',
        height: 'calc(100vh - 220px)',
        overflowX: 'hidden',
        overflowY: 'auto',
    },
    searchBox: {
        margin: '0px 10px',
        borderRadius: tokens.borderRadiusLarge,
        minWidth: '265px',
    },
    title: {
        lineHeight: '22px',
        marginLeft: '5px',
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
