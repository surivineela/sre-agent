import { TaskListAdd24Regular } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { TodoInfo } from '../../Common/Contracts/DataPlane/TodoPlan';
import { ToDoPlanResources } from '../../Strings/SREAgentResources';
import { ChatBoxSidePanelContext } from '../Contracts/Context';
import { SpecialMessageCard } from './Chat/SpecialMessageCard';

const TodoPlanChatMessage = ({ todoPlan }: { todoPlan: TodoInfo }) => {
    const { openTodoPlan } = useContext(ChatBoxSidePanelContext);

    const intl = useIntl();

    return (
        <SpecialMessageCard
            icon={<TaskListAdd24Regular />}
            primaryText={todoPlan.title}
            secondaryText={intl.formatMessage(ToDoPlanResources.todoPlanText)}
            onClick={() => {
                openTodoPlan(todoPlan);
            }}
        />
    );
};

export default memo(TodoPlanChatMessage);
