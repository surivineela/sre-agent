import { Button, DrawerHeader, DrawerHeaderTitle } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { MemorySearchFocusOptions } from '../Contracts/Context';
import MemorySearchPanelContent from './MemorySearchPanelContent';

interface MemorySidePanelProps {
    memoryResult: MemorySearchResult | null;
    onClose: () => void;
    focusOptions?: MemorySearchFocusOptions;
    onFocusHandled?: () => void;
}

const MemorySidePanel = ({ memoryResult, onClose, focusOptions, onFocusHandled }: MemorySidePanelProps) => {
    const intl = useIntl();

    if (!memoryResult) return null;

    return (
        <>
            <DrawerHeader>
                <DrawerHeaderTitle
                    action={
                        <Button
                            appearance="subtle"
                            aria-label={intl.formatMessage(SreAgentResources.closePanel)}
                            icon={<Dismiss24Regular />}
                            onClick={onClose}
                        />
                    }
                >
                    {intl.formatMessage(MemorySearchCardResources.memorySearchResults)}
                </DrawerHeaderTitle>
            </DrawerHeader>
            <MemorySearchPanelContent memoryResult={memoryResult} focusOptions={focusOptions} onFocusHandled={onFocusHandled} />
        </>
    );
};

export default memo(MemorySidePanel);
