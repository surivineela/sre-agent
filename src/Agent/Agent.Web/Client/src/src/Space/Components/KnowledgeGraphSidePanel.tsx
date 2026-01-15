import { Button, DrawerHeader, makeStyles, Subtitle2, tokens } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeGraphSearchResult } from '../../Common/Contracts/DataPlane/Message';
import { KnowledgeGraphCardResources, SreAgentResources } from '../../Strings/SREAgentResources';
import KnowledgeGraphPanelContent from './KnowledgeGraphPanelContent';

interface KnowledgeGraphSidePanelProps {
    knowledgeGraphResult: KnowledgeGraphSearchResult | null;
    onClose: () => void;
}

const useStyles = makeStyles({
    header: {
        display: 'flex',
        flexWrap: 'nowrap',
        alignItems: 'center',
        justifyContent: 'space-between',
        minWidth: '0px',
        minHeight: '0px',
        gap: tokens.spacingHorizontalS,
    },
    headerText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flex: '1 1 auto',
    },
    headerButton: {
        flex: '0 1 auto',
    },
    content: {
        padding: '12px',
        height: '100%',
        width: '100%',
        boxSizing: 'border-box',
        overflowY: 'auto',
    },
});

const KnowledgeGraphSidePanel = ({ knowledgeGraphResult, onClose }: KnowledgeGraphSidePanelProps) => {
    const styles = useStyles();
    const intl = useIntl();

    if (!knowledgeGraphResult) return null;

    return (
        <>
            <DrawerHeader>
                <div className={styles.header}>
                    <Subtitle2 className={styles.headerText}>
                        {intl.formatMessage(KnowledgeGraphCardResources.knowledgeGraphSearchResults)}
                    </Subtitle2>
                    <Button
                        appearance="subtle"
                        icon={<Dismiss24Regular />}
                        onClick={onClose}
                        aria-label={intl.formatMessage(SreAgentResources.closePanel)}
                        className={styles.headerButton}
                    />
                </div>
            </DrawerHeader>
            <div className={styles.content}>
                <KnowledgeGraphPanelContent knowledgeGraphResult={knowledgeGraphResult} />
            </div>
        </>
    );
};

export default memo(KnowledgeGraphSidePanel);
