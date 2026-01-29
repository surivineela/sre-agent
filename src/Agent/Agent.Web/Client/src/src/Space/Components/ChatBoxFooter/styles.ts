import { makeStyles, tokens } from '@fluentui/react-components';

export const usePlusMenuStyles = makeStyles({
    plusButton: {
        minWidth: '32px',
        width: '32px',
        height: '32px',
        padding: '0',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: 'transparent',
        border: 'none',
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    plusButtonActive: {
        backgroundColor: tokens.colorNeutralBackground1Pressed,
    },
    menuPopover: {
        borderRadius: '12px',
        padding: '4px',
        minWidth: '220px',
    },
    checkmarkIcon: {
        color: tokens.colorBrandForeground1,
    },
    submenuPopover: {
        borderRadius: '12px',
        padding: '4px',
        minWidth: '180px',
    },
    submenuItemContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
    submenuItemDescription: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        lineHeight: tokens.lineHeightBase200,
    },
    iconPillContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
});

export const useIconPillStyles = makeStyles({
    iconPill: {
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '32px',
        minWidth: '32px',
        borderRadius: '8px',
        backgroundColor: tokens.colorNeutralBackground3,
        border: 'none',
        cursor: 'pointer',
        transition: 'all 0.15s ease',
        gap: '0px',
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
        },
    },
    iconPillIcon: {
        color: tokens.colorBrandForeground1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '32px',
        height: '32px',
    },
    iconPillDismiss: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        opacity: 0,
        width: 0,
        overflow: 'hidden',
        transition: 'opacity 0.15s ease, width 0.15s ease',
        color: tokens.colorNeutralForeground2,
        '&:hover': {
            color: tokens.colorNeutralForeground1,
        },
    },
    iconPillDismissVisible: {
        opacity: 1,
        width: '16px',
    },
});
