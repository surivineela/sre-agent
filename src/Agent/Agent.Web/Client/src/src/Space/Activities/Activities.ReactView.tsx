import { tokens } from '@fluentui/react-components';
import { FC, useCallback, useContext, useState } from 'react';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { NoAccessError } from '../../Common/Components/NoAccessError';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import { useUserPermissions } from '../../Common/Hooks/useUserPermissions';
import { AgentContext } from '../Contracts/Context';
import { useActivities } from '../Hooks/useActivities';
import { activitiesStylesRoot, ThreadContentStyles } from '../Styles/Activities.styles';
import { Resizable, ResizableChildProps } from './Resizable';
import { ThreadContent } from './ThreadContent';
import { ThreadsMenu } from './ThreadsMenu';

const Activities: FC = () => {
    const { logAmplitudeControlEvent } = useAzPortalContext();
    const { canReadThreads } = useUserPermissions();
    const { resourceId } = useContext(EnvironmentContext);

    const {
        selectedThread,
        addThread,
        deleteThread,
        selectThread,
        updateThreadLastReadTime,
        threadContentAndActionKey,
        activeThreadId,
        threadMenuHandleRef,
    } = useActivities();

    const [menuCollapsed, setMenuCollapsed] = useState<boolean>(false);
    const [actionsCollapsed, setActionsCollapsed] = useState<boolean>(true);

    const collapseResizables = useCallback(() => {
        setMenuCollapsed(true);
        setActionsCollapsed(true);
    }, []);

    const onExpandOrCollapseThreadsMenu = useCallback(
        (collapsed: boolean) => {
            setMenuCollapsed(collapsed);

            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: `${collapsed ? 'collapse' : 'expand'}ThreadMenu`,
                targetFriendlyName: `${collapsed ? 'Collapse' : 'Expand'} Thread Menu`,
                valueObjectName: SpecialControlValue.DoAction,
                valueObjectFriendlyName: SpecialControlValue.DoAction,
            });
        },
        [logAmplitudeControlEvent]
    );

    return (
        <AgentContext.Provider value={{ threadContentAndActionKey, activeThreadId, selectThread }}>
            <div style={activitiesStylesRoot}>
                {canReadThreads ? (
                    <>
                        <Resizable
                            position="left"
                            initialWidth="320px"
                            minWidthPixels={200}
                            maxWidthPixels={640}
                            maxWidthPercent={actionsCollapsed ? 50 : 33}
                            collapsedWidthPixels={70}
                            collapsed={menuCollapsed}
                            setCollapsed={onExpandOrCollapseThreadsMenu}
                            style={{ backgroundColor: tokens.colorNeutralBackground3 }}
                        >
                            {(resizableChildProps: ResizableChildProps) => (
                                <ThreadsMenu
                                    selectThread={selectThread}
                                    deleteThread={deleteThread}
                                    ref={threadMenuHandleRef}
                                    {...resizableChildProps}
                                />
                            )}
                        </Resizable>
                        <ThreadContent
                            thread={selectedThread}
                            addThread={addThread}
                            deleteThread={deleteThread}
                            updateThreadLastReadTime={updateThreadLastReadTime}
                            collapseResizables={collapseResizables}
                        />
                    </>
                ) : (
                    <div className={ThreadContentStyles.root}>
                        <NoAccessError requiredPermission={PermissionActions.AgentThreadsRead} resourceId={resourceId} />
                    </div>
                )}
            </div>
        </AgentContext.Provider>
    );
};

export default Activities;
