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

export const getStatusPillComponentStyleProperties = (status?: string | null) => {
    switch (status?.toLowerCase()) {
        case InvestigationStatusCommon.NotStarted:
            return {
                iconFontColor: undefined,
                statusTextFontColor: undefined,
                icon: SubtractCircleRegular,
                backgroundColor: undefined,
                borderColor: tokens.colorNeutralBackground6,
            };
        case InvestigationStatusCommon.InProgress:
        case TaskProgressStatus.Started:
        case TaskProgressStatus.InProgress:
        case HypothesisStatus.Pending:
        case HypothesisStatus.Validating:
            return {
                iconFontColor: tokens.colorBrandForegroundLinkHover,
                statusTextFontColor: undefined,
                icon: ArrowCounterclockwiseFilled,
                backgroundColor: undefined,
                borderColor: tokens.colorNeutralBackground6,
            };
        case InvestigationStatusCommon.Complete:
        case TaskProgressStatus.Completed:
        case HypothesisStatus.Validated:
            return {
                iconFontColor: tokens.colorNeutralForegroundInverted,
                statusTextFontColor: tokens.colorNeutralForegroundInverted,
                icon: CheckmarkCircleRegular,
                backgroundColor: tokens.colorPaletteGreenBackground3,
                borderColor: undefined,
            };
        case TaskProgressStatus.Failed:
            return {
                iconFontColor: tokens.colorNeutralForegroundInverted,
                statusTextFontColor: tokens.colorNeutralForegroundInverted,
                icon: DismissCircleRegular,
                backgroundColor: tokens.colorStatusDangerBackground3,
                borderColor: undefined,
            };
        case HypothesisStatus.Invalidated:
            return {
                iconFontColor: undefined,
                statusTextFontColor: undefined,
                icon: DismissCircleRegular,
                backgroundColor: tokens.colorNeutralBackground3,
                borderColor: undefined,
            };
        case HypothesisStatus.Inconclusive:
            return {
                iconFontColor: tokens.colorStatusWarningForeground3,
                statusTextFontColor: tokens.colorStatusWarningForeground3,
                icon: QuestionCircleRegular,
                backgroundColor: undefined,
                borderColor: tokens.colorStatusWarningForeground1,
            };
    }
};

export const getStatusPillComponentText = (status?: string | null) => {
    switch (status?.toLowerCase()) {
        case InvestigationStatusCommon.NotStarted:
            return 'Not started';
        case InvestigationStatusCommon.InProgress:
        case TaskProgressStatus.InProgress:
            return 'In progress';
        case TaskProgressStatus.Started:
            return 'Started';
        case HypothesisStatus.Pending:
            return 'Pending';
        case HypothesisStatus.Validating:
            return 'Validating';
        case InvestigationStatusCommon.Complete:
        case TaskProgressStatus.Completed:
            return 'Complete';
        case HypothesisStatus.Validated:
            return 'Validated';
        case TaskProgressStatus.Failed:
            return 'Failed';
        case HypothesisStatus.Invalidated:
            return 'Invalidated';
        case HypothesisStatus.Inconclusive:
            return 'Inconclusive';
    }
};

export const getHypothesisNodeThemeColor = (status?: string | null) => {
    switch (status?.toLowerCase()) {
        case HypothesisStatus.Validated:
            return tokens.colorStatusSuccessForeground1;
        case HypothesisStatus.Inconclusive:
            return tokens.colorStatusWarningForeground1;
        default:
            return tokens.colorNeutralForeground3;
    }
};
