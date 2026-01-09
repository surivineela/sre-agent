import { SplitCopilotNavItem } from '@fluentui-copilot/react-copilot';
import { makeStyles, MenuButtonProps, MenuProps, useRestoreFocusTarget } from '@fluentui/react-components';
import { Text } from '@fluentui/react-text';
import { memo, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation } from 'react-router-dom';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import ThreadActionsMenu from '../Activities/ThreadActionsMenu';
import { ThreadNavContext } from '../Contracts/Context';
import { PrimaryNavItemValues } from '../Contracts/SreAgentSpace';
import { constructNavItemId, getNavItemIdFromPathName } from '../Utilities';

interface IThreadItemProps {
    item: Thread;
    isThreadUnread: boolean;
}

const useStyles = makeStyles({
    threadItemButton: {
        minWidth: '0px',
    },
    threadItemMoreOptionButton: {
        paddingInlineStart: '0px',
        paddingInlineEnd: '0px',
        maxWidth: '32px',
        minWidth: '32px',
    },
    text: {
        overflow: 'hidden',
        whiteSpace: 'nowrap',
        textOverflow: 'ellipsis',
    },
});

const ThreadItem = memo(({ item, isThreadUnread }: IThreadItemProps) => {
    const intl = useIntl();

    const [open, setOpen] = useState(false);
    const onOpenChange: MenuProps['onOpenChange'] = (_, data) => {
        setOpen(data.open);
    };
    const location = useLocation();
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const styles = useStyles();
    const { updateThreadTitle, updateThreadFavorite, assignThreadItemDivRef, selectThread, deleteThread } = useContext(ThreadNavContext);

    const itemNavId = constructNavItemId(PrimaryNavItemValues.Threads, undefined, item.id);

    const restoreFocusTargetAttribute = useRestoreFocusTarget();

    return (
        <ThreadActionsMenu
            open={open}
            onOpenChange={onOpenChange}
            thread={item}
            updateThreadTitle={updateThreadTitle}
            updateThreadFavorite={updateThreadFavorite}
            handleThreadDelete={() => {
                deleteThread(item);
            }}
            trigger={(triggerProps: MenuButtonProps) => (
                <SplitCopilotNavItem
                    ref={(el: HTMLDivElement) => assignThreadItemDivRef(item.id, el)}
                    navItem={{
                        level: 1,
                        className: styles.threadItemButton,
                        value: itemNavId,
                        children: (
                            <Text weight={isThreadUnread ? 'bold' : 'regular'} size={300} className={styles.text}>
                                {item.title}
                            </Text>
                        ),
                        onClick: () => {
                            if (getNavItemIdFromPathName(location.pathname) === itemNavId) {
                                return;
                            }
                            logAmplitudeControlEvent({
                                targetType: 'button',
                                targetAction: 'clicked',
                                targetName: 'selectThread',
                                targetFriendlyName: 'Select thread',
                                valueObjectName: item.id,
                                valueObjectFriendlyName: item.id,
                                metadata: {
                                    threadId: item.id,
                                    threadType: item.source ?? 'unknown',
                                },
                            });
                            selectThread(item.id);
                        },
                        onContextMenu: (e: React.MouseEvent) => {
                            setOpen(true);
                            e.preventDefault();
                        },
                    }}
                    menuButton={{
                        ...triggerProps,
                        ...restoreFocusTargetAttribute,
                        className: styles.threadItemMoreOptionButton,
                        'aria-label': intl.formatMessage(SreAgentResources.moreOptions),
                    }}
                    menuButtonTooltip={{
                        content: intl.formatMessage(SreAgentResources.moreOptions),
                        relationship: 'label',
                    }}
                />
            )}
        />
    );
});

export default memo(ThreadItem);
