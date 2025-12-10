import { Button, DrawerHeader, Subtitle2 } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeGraphSearchResult } from '../../Common/Contracts/DataPlane/Message';
import { KnowledgeGraphCardResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useMemorySidePanelStyles } from '../Styles/MemorySidePanel.styles';
import KnowledgeGraphPanelContent from './KnowledgeGraphPanelContent';

interface KnowledgeGraphSidePanelProps {
    knowledgeGraphResult: KnowledgeGraphSearchResult | null;
    onClose: () => void;
}

const KnowledgeGraphSidePanel = ({ knowledgeGraphResult, onClose }: KnowledgeGraphSidePanelProps) => {
    const styles = useMemorySidePanelStyles();
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
