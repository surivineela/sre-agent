import { Clock12Regular } from '@fluentui/react-icons';
import { useMemo, type JSX } from 'react';
import { MetricsBadge } from '../../../../../common/components/src/MetricsBadge/MetricsBadge';
import { ISpan } from '../types/trace';
import { formatStartTime } from '../utils/traceHelper';
import { useTraceTitleActionsStyles } from './TraceTitleActions.Styles';

interface ITraceTitleActions {
    rootSpan?: ISpan;
    threadId?: string;
}

export function TraceTitleActions({ rootSpan, threadId }: ITraceTitleActions): JSX.Element | undefined {
    const styles = useTraceTitleActionsStyles();
    const startTime = useMemo(() => formatStartTime(rootSpan?.start_time?.toISOString()), [rootSpan?.start_time]);

    if (!threadId && !startTime) {
        return undefined;
    }

    return (
        <div className={styles.titleActions}>
            {threadId ? <MetricsBadge label={threadId} /> : null}
            {startTime ? <MetricsBadge label={startTime} icon={<Clock12Regular aria-hidden="true" />} /> : null}
        </div>
    );
}
