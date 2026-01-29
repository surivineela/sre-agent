import { mergeClasses, Tooltip } from '@fluentui/react-components';
import { ArrowClockwise20Regular } from '@fluentui/react-icons';
import { type JSX } from 'react';
import { useIntl } from 'react-intl';
import { ThreadTraceResources } from '../../../../../../../Strings/SREAgentResources';
import { Button } from '../../../../../common/components/src/Button/Button';
import { MetricsBadge } from '../../../../../common/components/src/MetricsBadge/MetricsBadge';
import { ISpan } from '../types/trace';
import { useTraceTitleActionsStyles } from './TraceTitleActions.Styles';

interface ITraceTitleActions {
    rootSpan?: ISpan;
    threadId?: string;
    onRefresh?: () => void;
    isLoading?: boolean;
}

export function TraceTitleActions({ threadId, onRefresh, isLoading }: ITraceTitleActions): JSX.Element | undefined {
    const styles = useTraceTitleActionsStyles();
    const intl = useIntl();

    return threadId ? (
        <div className={styles.titleActions}>
            <MetricsBadge label={threadId} />
            {onRefresh && (
                <Tooltip content={intl.formatMessage(ThreadTraceResources.refreshTraces)} relationship="label">
                    <Button
                        appearance="subtle"
                        aria-label={intl.formatMessage(ThreadTraceResources.refreshTraces)}
                        className={mergeClasses(styles.refreshButton, isLoading && styles.refreshButtonSpinning)}
                        disabled={isLoading}
                        icon={<ArrowClockwise20Regular aria-hidden="true" />}
                        onClick={onRefresh}
                    />
                </Tooltip>
            )}
        </div>
    ) : undefined;
}
