import { Body1, tokens } from '@fluentui-copilot/react-copilot';
import { CopilotNavItem } from '@fluentui-copilot/react-copilot-nav';
import { Dialog, makeStyles, useRestoreFocusTarget } from '@fluentui/react-components';
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
import Fade from '../Fade';
import ThreadSearchDialog from '../ThreadSearchDialog';

interface ITNewChatNavItemProps {
    isNavOpen: boolean;
    threads: Thread[];
    selectThread: (threadId: string | null) => void;
    excludedSources?: ThreadSource[];
}

const NewChatIcon = bundleIcon(Add20Filled, Add20Regular);
const SearchIcon = bundleIcon(Search20Filled, Search20Regular);

const useStyles = makeStyles({
    navItemCollapsed: {
        paddingInlineStart: tokens.spacingHorizontalS,
    },
});

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
                />
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
    (props: { disabled: boolean; icon: JSX.Element; value: string; onClick: () => void; label: string; isNavOpen: boolean }) => {
        const { navItemCollapsed } = useStyles();

        const restoreFocusTargetAttribute = useRestoreFocusTarget();

        return (
            <CopilotNavItem
                {...restoreFocusTargetAttribute}
                disabled={props.disabled}
                icon={props.icon}
                value={props.value}
                onClick={props.onClick}
                className={props.isNavOpen ? undefined : navItemCollapsed}
            >
                <Fade visible={props.isNavOpen} unmountOnExit>
                    <Body1 wrap={false}>{props.label}</Body1>
                </Fade>
            </CopilotNavItem>
        );
    }
);

export default memo(NewChatNavItem);
