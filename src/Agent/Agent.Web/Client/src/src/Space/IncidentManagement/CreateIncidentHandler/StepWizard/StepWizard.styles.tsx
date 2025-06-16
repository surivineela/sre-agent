import { tokens } from '@fluentui/react-components';
import { StepState } from './StepWizard.contracts';

export const stepContainerStyles: React.CSSProperties = {
    display: 'flex',
    gap: 12,
    paddingLeft: 2,
    alignItems: 'center',
};

const getStepColor = (state: StepState): string => {
    switch (state) {
        case 'current':
            return tokens.colorBrandForegroundLink;
        case 'complete':
            return tokens.colorPaletteGreenForeground1;
        case 'skipped':
        case 'upcoming':
        default:
            return tokens.colorNeutralStrokeDisabled;
    }
};

export const getCircleStyles = (state: StepState): React.CSSProperties => {
    return {
        width: 24,
        height: 24,
        color: getStepColor(state),
    };
};

export const getLabelStyles = (state: StepState): React.CSSProperties => {
    return {
        fontWeight: state === 'current' ? tokens.fontWeightSemibold : tokens.fontWeightRegular,
    };
};

export const separatorStyles: React.CSSProperties = {
    height: 24,
    width: 2,
    margin: 13,
    background: tokens.colorNeutralBackgroundDisabled,
};
