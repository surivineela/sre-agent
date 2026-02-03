import { makeStyles, tokens } from '@fluentui/react-components';

export const useSelectableCardStyles = makeStyles({
    card: {
        minWidth: '250px',
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
