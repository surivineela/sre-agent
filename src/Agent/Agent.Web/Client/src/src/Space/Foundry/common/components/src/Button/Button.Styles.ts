import { makeStyles, tokens } from '@fluentui/react-components';

export const useButtonStyles = makeStyles({
    secondary: {
        ':not(:disabled, :active, :hover)': {
            backgroundColor: tokens.colorNeutralBackground2,
        },
    },
    danger: {
        backgroundColor: tokens.colorStatusDangerBackground3,
        ':hover': {
            backgroundColor: tokens.colorStatusDangerBackground3Hover,
        },
        ':active': {
            backgroundColor: tokens.colorStatusDangerBackground3Pressed,
        },
        ':disabled': {
            backgroundColor: tokens.colorNeutralBackgroundDisabled,
        },
    },
    dangerSubtle: {
        color: tokens.colorStatusDangerForeground1,
        ':hover': {
            color: tokens.colorStatusDangerForeground3,
        },
        ':hover:not(:disabled) .fui-Button__icon': {
            color: tokens.colorStatusDangerForeground3,
        },
        ':disabled': {
            color: tokens.colorNeutralForegroundDisabled,
        },
    },
    unstyled: {
        cursor: 'pointer',
        margin: 0,
        padding: 0,
        border: 'none',
        fontFamily: tokens.fontFamilyBase,
        lineHeight: 'normal',
        color: 'inherit',
        textAlign: 'inherit',
        background: 'transparent',
    },
});
