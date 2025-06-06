import { tokens } from '@fluentui/react-components';
import { StepState } from './StepWizard.contracts';

export const stepContainerStyles: React.CSSProperties = {
    display: 'flex',
    gap: 12,
    paddingLeft: 2,
};

export const getCircleStyles = (state: StepState): React.CSSProperties => {
    return {
        borderRadius: '50%',
        width: 20,
        height: 20,
        margin: 2,
        background: state === 'current' ? tokens.colorBrandForegroundLink : tokens.colorNeutralStrokeDisabled,
        color: tokens.colorNeutralBackground1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: 12,
    };
};

export const getLabelStyles = (state: StepState): React.CSSProperties => {
    return {
        fontWeight: state === 'current' ? tokens.fontWeightSemibold : tokens.fontWeightRegular,
    };
};

export const separatorStyles: React.CSSProperties = {
    minHeight: 24,
    width: 2,
    marginLeft: 12,
    background: tokens.colorNeutralBackgroundDisabled,
};
