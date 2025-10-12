import { makeStyles, tokens } from '@fluentui/react-components';

export const useTraceTitleActionsStyles = makeStyles({
    titleActions: {
        overflow: 'hidden',
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
});
