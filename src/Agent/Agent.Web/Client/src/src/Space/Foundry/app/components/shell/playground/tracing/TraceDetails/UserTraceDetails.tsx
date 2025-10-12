import { Chat20Regular } from '@fluentui/react-icons';
import { FC, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';

const getDetailsFromSpan = (span: ISpan) => {
    return {
        output: span.attributes?.message ?? '-',
    };
};

interface UserTraceDetailsProps {
    span: ISpan;
}

export const UserTraceDetails: FC<UserTraceDetailsProps> = ({ span }) => {
    const intl = useIntl();
    const [userInputExpanded, setUserInputExpanded] = useState(false);
    const styles = useTracePanelStyles();
    const { output } = useMemo(() => getDetailsFromSpan(span), [span]);
    useEffect(() => {
        setUserInputExpanded(false);
    }, [span]);

    return (
        <>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <Chat20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.input)}</div>
                    <ExpandCollapseButton isExpanded={userInputExpanded} setIsExpanded={setUserInputExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.userPrompt)}</div>
                        <div className={userInputExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {output}
                        </div>
                    </div>
                </div>
            </div>
        </>
    );
};
