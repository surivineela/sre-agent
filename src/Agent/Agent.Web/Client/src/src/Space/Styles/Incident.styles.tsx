import { makeStyles, tokens } from '@fluentui/react-components';

export const useActionsStatusBarStyles = makeStyles({
    container: {
        padding: '8px 0px',
    },
    innerContainerNoBorder: {
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
        flexWrap: 'wrap',
        padding: '10px 0px 0px 0px',
    },
    statusGroup: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    error: { color: tokens.colorPaletteRedForeground1 },
    warning: { color: '#F7630C' },
    success: { color: tokens.colorPaletteLightGreenForeground1 },
    dropdown: {
        backgroundColor: tokens.colorNeutralBackground3,
        border: 'none',
        boxShadow: 'none',
        maxWidth: 'fit-content',
        minWidth: '30px',
    },
    statusPillGray: {
        backgroundColor: tokens.colorNeutralForeground3,
        color: tokens.colorNeutralBackground1,
        borderRadius: '12px',
        fontSize: '12px',
        padding: '0 6px',
        fontWeight: 600,
    },
    title: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    icon: {
        padding: '2px',
        display: 'flex',
        alignItems: 'center',
    },
    buttonUnclicked: {
        maxWidth: 'fit-content',
        minWidth: '20px',
        padding: '0px',
        borderRadius: tokens.borderRadiusXLarge,
    },
    buttonClicked: {
        maxWidth: 'fit-content',
        minWidth: '20px',
        padding: '1px 8px',
        borderRadius: '14px',
    },
});

export const useIncidentStyles = makeStyles({
    statusRow: {
        display: 'flex',
        gap: '24px',
        alignItems: 'center',
    },
    statusItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    verticalBar: {
        width: '4px',
        height: '20px',
        borderRadius: '4px',
        backgroundColor: tokens.colorNeutralForeground1,
    },
    verticalBarGray: {
        width: '4px',
        height: '20px',
        borderRadius: '4px',
        backgroundColor: tokens.colorNeutralForeground3,
    },
    count: {
        fontWeight: 600,
        fontSize: '16px',
    },
    label: {
        fontSize: tokens.fontSizeBase100,
    },
});
