import { Button, DrawerHeader, Subtitle2 } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useMemorySidePanelStyles } from '../Styles/MemorySidePanel.styles';
import MemorySearchPanelContent from './MemorySearchPanelContent';

interface MemorySidePanelProps {
    memoryResult: MemorySearchResult | null;
    onClose: () => void;
}

const MemorySidePanel = ({ memoryResult, onClose }: MemorySidePanelProps) => {
    const styles = useMemorySidePanelStyles();
    const intl = useIntl();

    if (!memoryResult) return null;

    return (
        <>
            <DrawerHeader>
                <div className={styles.header}>
                    <Subtitle2 className={styles.headerText}>{intl.formatMessage(MemorySearchCardResources.memorySearchResults)}</Subtitle2>
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
                <MemorySearchPanelContent memoryResult={memoryResult} />
            </div>
        </>
    );
};

export default memo(MemorySidePanel);
