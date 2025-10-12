import { makeStyles } from '@fluentui/react-components';

export const useMessageBarWithContextStyles = makeStyles({
    messageBarContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--spacingS)',
        '& > :last-child': {
            marginBottom: 'var(--spacingS)',
        },
    },
});
