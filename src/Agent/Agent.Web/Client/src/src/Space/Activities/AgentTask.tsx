import { Button, DrawerHeader, DrawerHeaderTitle, makeStyles, tokens } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { ForwardedRef, forwardRef, memo, useContext, useEffect, useImperativeHandle, useState } from 'react';
import { StreamingMessage } from '../../Common/Contracts/Azure/Streaming';
import Fade from '../Components/Fade';
import { AgentTaskHandle } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { Resizable } from './Resizable';

interface IAgentTaskProps {
    collapseResizables: () => void;
}

const useAgentTaskStyles = makeStyles({
    root: {
        backgroundColor: tokens.colorNeutralBackground1,
        height: '100%',
        borderRadius: tokens.borderRadiusXLarge,
    },
});

const AgentTask = forwardRef((props: IAgentTaskProps, ref: ForwardedRef<AgentTaskHandle>) => {
    const { collapseResizables } = props;
    const [collapsed, setCollapsed] = useState(true);
    const { subscribeTaskUpdateEvent } = useContext(StreamingContext);

    const { root } = useAgentTaskStyles();

    useImperativeHandle(ref, () => ({
        openAgentTask: (_taskId: string) => {
            if (collapsed) {
                setCollapsed(false);
                collapseResizables();
            }
        },
    }));

    useEffect(() => {
        const unsubscribe = subscribeTaskUpdateEvent((message: StreamingMessage) => {
            // ToDo: Handle task update message
            console.log(message);
        });

        return () => {
            unsubscribe();
        };
    }, [subscribeTaskUpdateEvent]);

    return (
        <Resizable
            position="right"
            initialWidth="50%"
            minWidthPixels={500}
            collapsedWidthPixels={collapsed ? 0 : 500}
            collapsed={collapsed}
            setCollapsed={setCollapsed}
            style={{ height: 'calc(100vh - 100px)', width: '100%' }}
        >
            {() => (
                <Fade visible={!collapsed} appear={true} unmountOnExit={true}>
                    <div className={root}>
                        <DrawerHeader>
                            <DrawerHeaderTitle
                                action={
                                    <Button
                                        appearance="subtle"
                                        aria-label="Close"
                                        icon={<Dismiss24Regular />}
                                        onClick={() => setCollapsed(true)}
                                    />
                                }
                            >
                                {'Deep investigation'}
                            </DrawerHeaderTitle>
                        </DrawerHeader>
                    </div>
                </Fade>
            )}
        </Resizable>
    );
});

export default memo(AgentTask);
