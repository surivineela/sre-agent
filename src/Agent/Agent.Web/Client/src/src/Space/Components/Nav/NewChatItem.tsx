import { Dialog, Tooltip, useRestoreFocusTarget } from '@fluentui/react-components';
import { Add20Filled, Add20Regular, bundleIcon, Search20Filled, Search20Regular } from '@fluentui/react-icons';
import { memo, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation } from 'react-router-dom';
import { SpecialControlValue } from '../../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { Thread, ThreadSource } from '../../../Common/Contracts/DataPlane/Thread';
import useUserPermissions from '../../../Common/Hooks/useUserPermissions';
import { KnowledgeGraphBuildStatusContext } from '../../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { ActivitiesResources } from '../../../Strings/SREAgentResources';
import { usePermissionContext } from '../../Contracts/PermissionContext';
import { getNavItemIdFromPathName } from '../../Utilities';
import ThreadSearchDialog from '../ThreadSearchDialog';
import { CopilotNavItem } from './CopilotNavItem';

interface ITNewChatNavItemProps {
    isNavOpen: boolean;
    threads: Thread[];
    selectThread: (threadId: string | null) => void;
    excludedSources?: ThreadSource[];
    children?: React.ReactNode;
}

const NewChatIcon = bundleIcon(Add20Filled, Add20Regular);
const SearchIcon = bundleIcon(Search20Filled, Search20Regular);

const NewChatNavItem = (props: ITNewChatNavItemProps) => {
    const location = useLocation();
    const intl = useIntl();

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { canWriteThreads } = usePermissionContext();
    const { canReadThreads } = useUserPermissions();
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const [isSearchDialogOpen, setIsSearchDialogOpen] = useState<boolean>(false);

    return (
        canReadThreads && (
            <>
                <Item
                    disabled={!canWriteThreads || !hasChatPermissions}
                    icon={<NewChatIcon />}
                    value={'newThread'}
                    onClick={() => props.selectThread(null)}
                    isNavOpen={props.isNavOpen}
                    label={intl.formatMessage(ActivitiesResources.createThreadButtonText)}
                >
                    {props.children}
                </Item>
                {hasChatPermissions && (
                    <Item
                        icon={<SearchIcon />}
                        value={'searchThreads'}
                        onClick={() => {
                            logAmplitudeControlEvent({
                                targetType: 'button',
                                targetAction: 'clicked',
                                targetName: 'searchThreads',
                                targetFriendlyName: 'Search threads',
                                valueObjectName: SpecialControlValue.DoAction,
                                valueObjectFriendlyName: SpecialControlValue.DoAction,
                            });
                            setIsSearchDialogOpen(true);
                        }}
                        disabled={false}
                        isNavOpen={props.isNavOpen}
                        label={intl.formatMessage(ActivitiesResources.searchThread)}
                    />
                )}
                <Dialog open={isSearchDialogOpen} onOpenChange={(_, data) => setIsSearchDialogOpen(data.open)}>
                    <ThreadSearchDialog {...props} activeThreadId={getNavItemIdFromPathName(location.pathname)} />
                </Dialog>
            </>
        )
    );
};

const Item = memo(
    (props: {
        disabled: boolean;
        icon: JSX.Element;
        value: string;
        onClick: () => void;
        label: string;
        isNavOpen: boolean;
        children?: React.ReactNode;
    }) => {
        const restoreFocusTargetAttribute = useRestoreFocusTarget();

        return (
            <div style={{ display: 'flex', alignItems: 'center' }}>
                <Tooltip content={props.label} relationship="label">
                    <CopilotNavItem
                        {...restoreFocusTargetAttribute}
                        disabled={props.disabled}
                        icon={props.icon}
                        value={props.value}
                        onClick={props.onClick}
                        aria-label={props.label}
                    >
                        {props.isNavOpen ? props.label : null}
                    </CopilotNavItem>
                </Tooltip>
                {props.children}
            </div>
        );
    }
);

export default memo(NewChatNavItem);
