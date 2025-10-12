import { Caption1, mergeClasses, TreeItemLayout } from '@fluentui/react-components';
import { ChevronDown16Regular, ChevronUp16Regular } from '@fluentui/react-icons';
import { useMemo, type JSX } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { Thread } from '../../../../../../../../Common/Contracts/DataPlane/Thread';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { Button } from '../../../../../../common/components/src/Button/Button';
import { MetricsBadge } from '../../../../../../common/components/src/MetricsBadge/MetricsBadge';
import { TraceBadge } from '../../../../../../common/components/src/TraceBadge/TraceBadge';
import type { ISpanTreeNode } from '../../types/trace';
import { getBadgeType, getSpanDurationSeconds, getSpanTitle, getSpanTotalTokens } from '../../utils/traceHelper';
import { useTreeViewItemStyles } from './TreeViewItem.Styles';

interface ITreeViewItem {
    span: ISpanTreeNode;
    thread: Thread;
    isOpen: boolean;
    isSelected: boolean;
    onToggleOpen: (spanId: string) => void;
}

export function TreeViewItem({ span, isOpen, isSelected, onToggleOpen, thread }: ITreeViewItem): JSX.Element {
    const intl = useIntl();
    const styles = useTreeViewItemStyles();
    const isParent = useMemo(() => span.uiChildren.length > 0, [span.uiChildren.length]);

    const title = useMemo(() => getSpanTitle(span, thread), [span, thread]);
    const id = useMemo(() => span.context?.span_id ?? '', [span.context?.span_id]);
    // const hasError = useMemo(() => span.status?.status_code === 'ERROR', [span.status?.status_code]);
    // const completed = useMemo(() => !hasError, [hasError]);

    const seconds = useMemo(() => getSpanDurationSeconds(span), [span]);
    const tokens = useMemo(() => getSpanTotalTokens(span), [span]);
    const badgeType = useMemo(() => getBadgeType(span), [span]);

    const renderCollapseButton = () => (
        <Button
            appearance="subtle"
            aria-label={intl.formatMessage(ThreadTraceResources.collapseSpanWithId, { id })}
            className={styles.expandButton}
            icon={<ChevronDown16Regular aria-hidden={true} />}
            onClick={e => {
                e.preventDefault();
                e.stopPropagation();
                onToggleOpen(id.toString());
            }}
        />
    );
    const renderExpandButton = () => (
        <Button
            appearance="subtle"
            aria-label={intl.formatMessage(ThreadTraceResources.expandSpanWithId, { id })}
            className={styles.expandButton}
            icon={<ChevronUp16Regular aria-hidden={true} />}
            onClick={e => {
                e.preventDefault();
                e.stopPropagation();
                onToggleOpen(id.toString());
            }}
        />
    );

    return (
        <TreeItemLayout
            className={isSelected ? mergeClasses(styles.item, styles.itemSelected) : styles.item}
            expandIcon={isParent ? <span /> : undefined}
        >
            {isSelected ? <span className={styles.selectedItemDecorator} /> : null}
            {isParent ? (isOpen ? renderCollapseButton() : renderExpandButton()) : null}
            <div className={styles.itemContent} data-tree-path-id={id}>
                {badgeType ? <TraceBadge type={badgeType} /> : null}
                <div className={styles.itemTitleContainer}>
                    <Caption1 className={styles.itemTitle}>{title}</Caption1>
                    {/* {completed ? (
                        <CheckmarkCircle16Regular
                            aria-label={intl.formatMessage(ThreadTraceResources.spanIdCompleted, { id })}
                            className={styles.checkmark}
                        />
                    ) : null} */}
                    {/* {hasError ? (
                        <ErrorCircle16Regular
                            aria-label={intl.formatMessage(ThreadTraceResources.spanIdFailed, { id })}
                            className={styles.errorMark}
                        />
                    ) : null} */}
                </div>
                {seconds !== undefined && seconds !== 0 ? (
                    <MetricsBadge label={intl.formatMessage(ThreadTraceResources.secondsWithLabel, { seconds })} />
                ) : null}
                {tokens !== undefined && tokens !== 0 ? (
                    <MetricsBadge label={intl.formatMessage(ThreadTraceResources.tokensWithLabel, { tokens })} />
                ) : null}
            </div>
        </TreeItemLayout>
    );
}
