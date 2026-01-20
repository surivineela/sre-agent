import { makeStyles, tokens } from '@fluentui/react-components';

export const useTreeViewItemStyles = makeStyles({
    item: {
        position: 'relative',
        borderRadius: tokens.borderRadiusXLarge,
        backgroundColor: 'transparent',
        '&:before': {
            content: '""',
            position: 'absolute',
            top: 0,
            left: 0,
            bottom: 0,
            right: 0,
            borderRadius: tokens.borderRadiusXLarge,
        },
        '&:hover': {
            backgroundColor: 'transparent',
            '&:before': {
                backgroundColor: tokens.colorNeutralBackgroundInverted,
                opacity: 0.05,
            },
        },
    },
    itemSelected: {
        '&:before, &:hover&:before': {
            backgroundColor: tokens.colorNeutralBackgroundInverted,
            opacity: 0.15,
        },
    },
    itemTitleContainer: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        alignItems: 'center',
        flex: 1,
        minWidth: 0,
        overflow: 'hidden',
    },
    itemTitle: {
        color: tokens.colorNeutralForeground1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    itemContent: {
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
        paddingTop: tokens.spacingVerticalXS,
        paddingBottom: tokens.spacingVerticalXS,
        paddingRight: tokens.spacingHorizontalM,
        paddingLeft: tokens.spacingHorizontalS,
    },
    checkmark: {
        color: tokens.colorStatusSuccessForeground1,
    },
    errorMark: {
        color: tokens.colorStatusDangerForeground1,
    },
    expandButton: {
        position: 'absolute',
        top: '50%',
        transform: 'translateY(-50%)',
        left: tokens.spacingHorizontalXXS,
        '&:hover': {
            backgroundColor: 'inherit',
        },
    },
    selectedItemDecorator: {
        position: 'absolute',
        top: tokens.spacingHorizontalS,
        left: 0,
        width: '3px',
        height: '16px',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorCompoundBrandForeground1,
    },
});
