import { makeStyles, tokens } from '@fluentui/react-components';

export const useSelectableCardStyles = makeStyles({
    card: {
        boxSizing: 'border-box',
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        padding: '16px',
        width: '196px',
        height: '64px',
        backgroundColor: tokens.colorNeutralBackground1,
        border: 'none',
        boxShadow: '0px 2px 4px rgba(0, 0, 0, 0.14), 0px 0px 2px rgba(0, 0, 0, 0.12)',
        borderRadius: '12px',
        cursor: 'pointer',
        transitionProperty: 'all',
        transitionDuration: '0.2s',
        transitionTimingFunction: 'ease',
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
            boxShadow: '0px 4px 8px rgba(0, 0, 0, 0.14), 0px 0px 4px rgba(0, 0, 0, 0.12)',
        },
    },
    cardDisabled: {
        cursor: 'not-allowed',
        opacity: 0.5,
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground1,
            boxShadow: '0px 2px 4px rgba(0, 0, 0, 0.14), 0px 0px 2px rgba(0, 0, 0, 0.12)',
        },
    },
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '24px',
        height: '24px',
    },
    title: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
});
