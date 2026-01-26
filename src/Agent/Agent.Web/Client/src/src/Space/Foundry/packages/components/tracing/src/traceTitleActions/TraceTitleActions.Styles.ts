import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useTraceTitleActionsStyles = makeStyles({
    titleActions: {
        overflow: 'hidden',
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    refreshButton: {
        minWidth: 'auto',
        ...shorthands.transition('transform', '0.15s', 'ease-out'),
        ':hover:not(:disabled)': {
            transform: 'rotate(30deg)',
        },
        ':active:not(:disabled)': {
            transform: 'rotate(180deg)',
        },
    },
    refreshButtonSpinning: {
        minWidth: 'auto',
        animationName: {
            from: { transform: 'rotate(0deg)' },
            to: { transform: 'rotate(360deg)' },
        },
        animationDuration: '0.8s',
        animationIterationCount: 'infinite',
        animationTimingFunction: 'linear',
        opacity: 0.7,
    },
});
