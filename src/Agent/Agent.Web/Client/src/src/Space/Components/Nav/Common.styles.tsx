import { tokens } from '@fluentui-copilot/react-copilot';
import { GriffelStyle, makeStyles } from '@fluentui/react-components';

const getNavItemPaddingStyles = (level: number = 1): GriffelStyle => {
    return {
        paddingInlineStart: `calc(${tokens.spacingHorizontalS} + calc(calc(${level} - 1) * 26px))`,
        paddingInlineEnd: tokens.spacingHorizontalS,
        '&::after': {
            marginInlineStart: `calc(-1 * ( ${tokens.spacingHorizontalS} + calc(calc(${level} - 1) * 26px)))`,
        },
    };
};

export const useCopilotNavItemStyles = makeStyles({
    navItem: getNavItemPaddingStyles(1),
    iconOnlyNavItem: {
        width: 'fit-content',
    },
});

export const useSplitCopilotNavItemStyles = makeStyles({
    splitNavItem: {
        '& > button': getNavItemPaddingStyles(2),
    },
});
