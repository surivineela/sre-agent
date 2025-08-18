import { tokens } from '@fluentui/react-components';
import {
    ArrowClockwiseFilled,
    ArrowCounterclockwiseFilled,
    CheckmarkCircleColor,
    CheckmarkCircleRegular,
    DismissCircleRegular,
    QuestionCircleRegular,
    SubtractCircleRegular,
} from '@fluentui/react-icons';
import { HypothesisStatus, InvestigationStatusCommon, TaskProgressStatus } from '../../../Common/Contracts/DataPlane/AgentTask';

export const getInitialInvestigationStepsIcon = (status: string) => {
    const style = {
        fontSize: `${tokens.fontSizeBase600}px`,
        minWidth: `${tokens.fontSizeBase600}px`,
    };
    switch (status.toLowerCase()) {
        case InvestigationStatusCommon.Complete:
        case TaskProgressStatus.Completed:
            return <CheckmarkCircleColor style={style} />;
        case InvestigationStatusCommon.InProgress:
        case TaskProgressStatus.InProgress:
            return <ArrowClockwiseFilled style={{ ...style, color: tokens.colorBrandForegroundLinkHover }} />;
        default:
            return <SubtractCircleRegular style={{ ...style }} />;
    }
};

export const getStatusPillComponentProperties = (status?: string | null) => {
    switch (status?.toLowerCase()) {
        case HypothesisStatus.Validated:
            return {
                text: 'Validated',
                iconFontColor: tokens.colorNeutralForegroundInverted,
                statusTextFontColor: tokens.colorNeutralForegroundInverted,
                icon: CheckmarkCircleRegular,
                backgroundColor: tokens.colorPaletteGreenBackground3,
                borderColor: undefined,
            };
        case InvestigationStatusCommon.Complete:
        case TaskProgressStatus.Completed:
            return {
                text: 'Complete',
                iconFontColor: tokens.colorNeutralForegroundInverted,
                statusTextFontColor: tokens.colorNeutralForegroundInverted,
                icon: CheckmarkCircleRegular,
                backgroundColor: tokens.colorPaletteGreenBackground3,
                borderColor: undefined,
            };
        case HypothesisStatus.Invalidated:
            return {
                text: 'Invalidated',
                iconFontColor: undefined,
                statusTextFontColor: undefined,
                icon: DismissCircleRegular,
                backgroundColor: tokens.colorNeutralBackground3,
                borderColor: undefined,
            };
        case HypothesisStatus.Inconclusive:
            return {
                text: 'Inconclusive',
                iconFontColor: tokens.colorStatusWarningForeground3,
                statusTextFontColor: tokens.colorStatusWarningForeground3,
                icon: QuestionCircleRegular,
                backgroundColor: undefined,
                borderColor: tokens.colorStatusWarningBackground2,
            };
        case HypothesisStatus.Pending:
            return {
                text: 'Pending',
                iconFontColor: tokens.colorBrandForegroundLinkHover,
                statusTextFontColor: undefined,
                icon: ArrowCounterclockwiseFilled,
                backgroundColor: undefined,
                borderColor: tokens.colorNeutralBackground6,
            };
        case InvestigationStatusCommon.InProgress:
        case TaskProgressStatus.InProgress:
            return {
                text: 'In Progress',
                iconFontColor: tokens.colorBrandForegroundLinkHover,
                statusTextFontColor: undefined,
                icon: ArrowCounterclockwiseFilled,
                backgroundColor: undefined,
                borderColor: tokens.colorNeutralBackground6,
            };
        default:
            return {
                text: 'Not Started',
                iconFontColor: undefined,
                statusTextFontColor: undefined,
                icon: SubtractCircleRegular,
                backgroundColor: undefined,
                borderColor: tokens.colorNeutralBackground6,
            };
    }
};
