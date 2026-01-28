import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useWizardStepperHorizontalStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        width: '100%',
    },
    stepperContainer: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'center',
        width: '100%',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: '16px',
        boxShadow: tokens.shadow4,
        padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalXXL}`,
        boxSizing: 'border-box',
    },
    stepsRow: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'center',
        gap: '0',
        width: '100%',
    },
    stepWrapper: {
        display: 'flex',
        alignItems: 'center',
        flex: 1,
    },
    stepWrapperLast: {
        flex: 0,
    },
    step: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalXS,
        minWidth: '120px',
        maxWidth: '180px',
        textAlign: 'center',
    },
    iconContainer: {
        width: '24px',
        height: '24px',
        borderRadius: tokens.borderRadiusCircular,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
        marginBottom: tokens.spacingVerticalXS,
    },
    pendingIcon: {
        backgroundColor: tokens.colorNeutralBackground4,
        ...shorthands.border(tokens.strokeWidthThick, 'solid', tokens.colorNeutralStroke1),
    },
    activeIcon: {
        backgroundColor: tokens.colorBrandBackground,
        boxShadow: `0 0 0 3px ${tokens.colorBrandBackgroundInvertedSelected}`,
    },
    completedIcon: {
        backgroundColor: tokens.colorPaletteGreenBackground3,
        color: tokens.colorNeutralForegroundInverted,
        fontSize: tokens.fontSizeBase400,
    },
    stepTitle: {
        fontWeight: tokens.fontWeightSemibold,
    },
    activeTitleText: {
        color: tokens.colorNeutralForeground1,
    },
    pendingTitleText: {
        color: tokens.colorNeutralForeground3,
    },
    completedTitleText: {
        color: tokens.colorNeutralForeground2,
    },
    stepDescription: {
        color: tokens.colorNeutralForeground3,
        maxWidth: '150px',
        textAlign: 'center',
    },
    connector: {
        flex: 1,
        height: '2px',
        backgroundColor: tokens.colorNeutralStroke2,
        marginTop: tokens.spacingVerticalM,
        marginLeft: tokens.spacingHorizontalS,
        marginRight: tokens.spacingHorizontalS,
        minWidth: '40px',
    },
    connectorCompleted: {
        backgroundColor: tokens.colorPaletteGreenBackground3,
    },
});
