import { EntityCard, EntityTitle } from '@fluentui-copilot/react-copilot';
import { Link, makeStyles, Subtitle2 } from '@fluentui/react-components';
import { TaskListAdd24Regular } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { TodoInfo } from '../../Common/Contracts/DataPlane/TodoPlan';
import { ToDoPlanResources } from '../../Strings/SREAgentResources';
import { ChatBoxSidePanelContext } from '../Contracts/Context';

const useStyles = makeStyles({
    root: {
        padding: '5px 0px',
    },
    text: {
        paddingLeft: '3px',
    },
});

const TodoPlanChatMessage = ({ todoPlan }: { todoPlan: TodoInfo }) => {
    const { openTodoPlan } = useContext(ChatBoxSidePanelContext);

    const styles = useStyles();

    const intl = useIntl();

    return (
        <div className={styles.root}>
            <EntityCard
                orientation="horizontal"
                role="group"
                style={{ maxWidth: 'unset' }}
                entityTitle={
                    <EntityTitle
                        media={<TaskListAdd24Regular />}
                        primaryText={
                            <Link
                                appearance="subtle"
                                onClick={e => {
                                    e.stopPropagation();
                                    openTodoPlan(todoPlan);
                                }}
                            >
                                <Subtitle2 wrap={true} block={true}>
                                    {todoPlan.title}
                                </Subtitle2>
                            </Link>
                        }
                        secondaryText={intl.formatMessage(ToDoPlanResources.todoPlanText)}
                    />
                }
            />
        </div>
    );
};

export default memo(TodoPlanChatMessage);
