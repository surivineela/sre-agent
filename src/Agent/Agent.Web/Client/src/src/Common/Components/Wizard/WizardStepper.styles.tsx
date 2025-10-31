import { makeStyles, tokens } from '@fluentui/react-components';

export const useWizardStepperStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0',
    },
    stepColumn: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        alignItems: 'center',
    },
    stepRow: {
        display: 'flex',
        alignItems: 'baseline',
        gap: tokens.spacingHorizontalM,
        paddingBottom: tokens.spacingVerticalM,
    },
    iconContainer: {
        width: '20px',
        height: '20px',
        borderRadius: '50%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
    },
    pendingIcon: {
        backgroundColor: tokens.colorNeutralForegroundDisabled,
        color: tokens.colorNeutralForegroundInverted,
        border: `2px solid ${tokens.colorNeutralForegroundDisabled}`,
    },
    activeIcon: {
        backgroundColor: tokens.colorBrandBackground,
        color: tokens.colorNeutralForegroundInverted,
        border: `2px solid ${tokens.colorBrandBackground}`,
    },
    completedIcon: {
        backgroundColor: tokens.colorNeutralForegroundInverted,
        color: tokens.colorPaletteGreenBackground3,
    },
    stepTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightRegular,
    },
    activeTitleText: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    pendingTitleText: {
        color: tokens.colorNeutralForeground3,
    },
    completedTitleText: {
        color: tokens.colorNeutralForeground2,
    },
    connector: {
        width: '1px',
        height: '28px',
        backgroundColor: tokens.colorNeutralStroke2,
    },
    lastStep: {
        '& $connector': {
            display: 'none',
        },
    },
});
