import { Warning20Regular } from '@fluentui/react-icons';
import { FC, useEffect, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { IncidentDocument } from '../../../../../../../../Common/Contracts/Azure/IncidentHandler';
import { Thread } from '../../../../../../../../Common/Contracts/DataPlane/Thread';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';
interface IIncidentTraceDetailsProps {
    span: ISpan;
    thread?: Thread;
    incident?: IncidentDocument;
}

export const IncidentTraceDetails: FC<IIncidentTraceDetailsProps> = ({ span, incident, thread }) => {
    const intl = useIntl();
    const [incidentDetailsExpanded, setIncidentDetailsExpanded] = useState(false);
    const styles = useTracePanelStyles();
    useEffect(() => {
        setIncidentDetailsExpanded(false);
    }, [span]);

    return (
        <>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <Warning20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.incidentDetails)}</div>
                    <ExpandCollapseButton isExpanded={incidentDetailsExpanded} setIsExpanded={setIncidentDetailsExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.incidentId)}</div>
                        <div className={styles.rightPaneSubsectionBody}>{incident?.id ?? '-'}</div>
                    </div>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.incidentPlatform)}</div>
                        <div className={styles.rightPaneSubsectionBody}>{thread?.incidentSource?.incidentType ?? '-'}</div>
                    </div>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.description)}</div>
                        <div className={incidentDetailsExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {incident?.description ?? '-'}
                        </div>
                    </div>
                </div>
            </div>
        </>
    );
};
