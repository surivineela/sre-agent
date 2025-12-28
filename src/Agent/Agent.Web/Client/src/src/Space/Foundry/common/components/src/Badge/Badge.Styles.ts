import { makeStyles, tokens } from '@fluentui/react-components';

export const useBadgeStyles = makeStyles({
    badge: {
        flexShrink: 0,
        minWidth: 'max-content',
        whiteSpace: 'nowrap',
        padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXS}`,
    },
});
