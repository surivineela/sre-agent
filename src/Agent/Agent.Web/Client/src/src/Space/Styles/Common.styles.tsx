import { makeStyles, tokens } from '@fluentui/react-components';

export const useCommonStyles = makeStyles({
    contentRootBorderAndBackground: {
        borderRadius: '24px',
        boxShadow: tokens.shadow4,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    contentHeader: {
        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    },
});
