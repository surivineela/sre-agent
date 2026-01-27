import { makeStyles, tokens } from '@fluentui/react-components';
import {
    ArrowClockwiseFilled,
    ArrowCounterclockwiseFilled,
    CheckmarkCircleColor,
    CheckmarkCircleRegular,
    DismissCircleRegular,
    FluentIcon,
    QuestionCircleRegular,
    SubtractCircleRegular,
} from '@fluentui/react-icons';
import {
    AgentTaskStatus,
    HypothesisStatus,
    InvestigationStatusCommon,
    TaskProgressStatus,
} from '../../../Common/Contracts/DataPlane/AgentTask';

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

export const getStatusPillComponentStyleProperties = (
    status?: string | null
):
    | {
        icon: FluentIcon;
        color?: 'brand' | 'success' | 'severe' | 'warning' | 'important' | 'informative' | 'subtle';
    }
    | undefined => {
    switch (status?.toLowerCase()) {
        case InvestigationStatusCommon.NotStarted:
            return {
                icon: SubtractCircleRegular,
                color: 'subtle',
            };
        case AgentTaskStatus.PendingUserApproval:
        case AgentTaskStatus.InProgress:
        case InvestigationStatusCommon.InProgress:
        case TaskProgressStatus.Started:
        case TaskProgressStatus.InProgress:
        case HypothesisStatus.Pending:
        case HypothesisStatus.Validating:
            return {
                icon: ArrowCounterclockwiseFilled,
                color: 'brand',
            };
        case AgentTaskStatus.Complete:
        case InvestigationStatusCommon.Complete:
        case TaskProgressStatus.Completed:
        case HypothesisStatus.Validated:
            return {
                icon: CheckmarkCircleRegular,
                color: 'success',
            };
        case AgentTaskStatus.Failed:
        case TaskProgressStatus.Failed:
            return {
                icon: DismissCircleRegular,
                color: 'severe',
            };
        case HypothesisStatus.Invalidated:
            return {
                icon: DismissCircleRegular,
                color: 'important',
            };
        case HypothesisStatus.Inconclusive:
            return {
                icon: QuestionCircleRegular,
                color: 'warning',
            };
        case AgentTaskStatus.Cancelled:
            return {
                icon: SubtractCircleRegular,
                color: 'informative',
            };
    }
};

export const getStatusPillComponentText = (status?: string | null) => {
    switch (status?.toLowerCase()) {
        case InvestigationStatusCommon.NotStarted:
            return 'Not started';
        case AgentTaskStatus.PendingUserApproval:
            return 'Pending approval';
        case AgentTaskStatus.InProgress:
        case InvestigationStatusCommon.InProgress:
        case TaskProgressStatus.InProgress:
            return 'In progress';
        case TaskProgressStatus.Started:
            return 'Started';
        case HypothesisStatus.Pending:
            return 'Pending';
        case HypothesisStatus.Validating:
            return 'Validating';
        case AgentTaskStatus.Complete:
        case InvestigationStatusCommon.Complete:
        case TaskProgressStatus.Completed:
            return 'Complete';
        case HypothesisStatus.Validated:
            return 'Validated';
        case AgentTaskStatus.Failed:
        case TaskProgressStatus.Failed:
            return 'Failed';
        case HypothesisStatus.Invalidated:
            return 'Invalidated';
        case HypothesisStatus.Inconclusive:
            return 'Inconclusive';
        case AgentTaskStatus.Cancelled:
            return 'Cancelled';
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

export const useCommonStyles = makeStyles({
    card: {
        transitionProperty: 'box-shadow',
        transitionDelay: tokens.curveDecelerateMid,
        transitionDuration: tokens.durationNormal,
    },
    cardBorder: {
        border: `${tokens.strokeWidthThick} solid ${tokens.colorNeutralStroke2}`,
    },
});
