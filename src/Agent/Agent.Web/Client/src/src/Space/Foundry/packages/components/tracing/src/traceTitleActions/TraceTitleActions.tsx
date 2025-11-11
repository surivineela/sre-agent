import { type JSX } from 'react';
import { MetricsBadge } from '../../../../../common/components/src/MetricsBadge/MetricsBadge';
import { ISpan } from '../types/trace';
import { useTraceTitleActionsStyles } from './TraceTitleActions.Styles';

interface ITraceTitleActions {
    rootSpan?: ISpan;
    threadId?: string;
}

export function TraceTitleActions({ threadId }: ITraceTitleActions): JSX.Element | undefined {
    const styles = useTraceTitleActionsStyles();
    return threadId ? <div className={styles.titleActions}>{threadId ? <MetricsBadge label={threadId} /> : null}</div> : undefined;
}
