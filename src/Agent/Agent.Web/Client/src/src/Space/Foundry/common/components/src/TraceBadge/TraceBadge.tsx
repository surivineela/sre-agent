import {
    AgentsRegular,
    ArrowTurnDownRightRegular,
    ArrowTurnUpLeftRegular,
    BrainCircuitRegular,
    Person12Regular,
    Warning12Regular,
    Wrench16Regular,
} from '@fluentui/react-icons';
import { forwardRef, useMemo } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../Strings/SREAgentResources';
import { Badge } from '../Badge/Badge';
import { useTraceBadgeStyles } from './TraceBadge.Styles';

export type TraceBadgeTypes =
    | 'incident'
    | 'agent'
    | 'subagent'
    | 'tool'
    | 'userMessage'
    | 'agentHandoff'
    | 'agentHandback'
    | 'modelGeneration'
    | 'agentResponse';

export interface ITraceBadgeProps {
    type: TraceBadgeTypes;
}

export const TraceBadge = forwardRef<HTMLDivElement, ITraceBadgeProps>(({ type }, ref) => {
    const intl = useIntl();
    const styles = useTraceBadgeStyles();

    const { label, className, icon } = useMemo(() => {
        switch (type) {
            case 'incident':
                return {
                    label: intl.formatMessage(ThreadTraceResources.incident),
                    className: styles.incident,
                    icon: <Warning12Regular aria-hidden="true" />,
                };
            case 'agent':
                return {
                    label: intl.formatMessage(ThreadTraceResources.agent),
                    className: styles.agent,
                    icon: <AgentsRegular aria-hidden="true" />,
                };
            case 'subagent':
                return {
                    label: intl.formatMessage(ThreadTraceResources.subagent),
                    className: styles.subagent,
                    icon: <AgentsRegular aria-hidden="true" />,
                };
            case 'tool':
                return {
                    label: intl.formatMessage(ThreadTraceResources.tool),
                    className: styles.tool,
                    icon: <Wrench16Regular aria-hidden="true" />,
                };
            case 'userMessage':
                return {
                    label: intl.formatMessage(ThreadTraceResources.user),
                    className: styles.user,
                    icon: <Person12Regular aria-hidden="true" />,
                };
            case 'agentHandoff':
                return {
                    label: intl.formatMessage(ThreadTraceResources.agentHandoff),
                    className: styles.agentHandoff,
                    icon: <ArrowTurnDownRightRegular aria-hidden="true" />,
                };
            case 'agentHandback':
                return {
                    label: intl.formatMessage(ThreadTraceResources.agentHandback),
                    className: styles.agentHandoff,
                    icon: <ArrowTurnUpLeftRegular aria-hidden="true" />,
                };
            case 'modelGeneration':
                return {
                    label: intl.formatMessage(ThreadTraceResources.modelGeneration),
                    className: styles.modelGeneration,
                    icon: <BrainCircuitRegular aria-hidden="true" />,
                };
            case 'agentResponse':
                return {
                    label: intl.formatMessage(ThreadTraceResources.agentResponse),
                    className: styles.agentResponse,
                    icon: <AgentsRegular aria-hidden="true" />,
                };
            default:
                return {
                    label: undefined,
                    className: undefined,
                    icon: undefined,
                };
        }
    }, [type, styles, intl]);

    return (
        <Badge ref={ref} className={className} icon={icon}>
            {label}
        </Badge>
    );
});
TraceBadge.displayName = 'TraceBadge';
