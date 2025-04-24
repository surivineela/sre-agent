import { FC, memo, useContext, useMemo, useState } from 'react';
import { IThreadActivitiesProps } from '../Contracts/Activities';
import { Shimmer } from '@fluentui/react/lib/Shimmer';
import {  useThreadActionsStyles } from '../Styles/Activities.styles';
import { ActionsResources, SreAgentResources } from '../../Strings/SREResources.resjson';
import debounce from 'lodash/debounce';
import { AgentContext } from './Activities.ReactView';
import { Action, ActionStatus } from '../../Common/Contracts/Azure/SreAgent';
import { Text, Card, CardHeader, CardFooter, SearchBoxChangeEvent, InputOnChangeData, SearchBox } from '@fluentui/react-components';
import { useActions } from '../Hooks/useActions';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { ArrowSync16Filled, CheckmarkCircle16Filled, Dismiss16Filled } from '@fluentui/react-icons';

export const ThreadActions: FC<IThreadActivitiesProps> = (props: IThreadActivitiesProps) => {
    const { thread } = props;
    const { threadsInitialized } = useContext(AgentContext);
    const { actions, isLoading } = useActions(thread?.id);
    const [searchString, setSearchString] = useState<string>();

    const actionsStyles = useThreadActionsStyles();

    const filteredActions = useMemo(() => {
        if (searchString) {
            return (actions ?? []).filter(action => action.title.toLowerCase().includes(searchString.toLowerCase()));
        } else {
            return actions ?? [];
        }
    }, [searchString, actions]);

  return (
    <div className={actionsStyles.root}>
      <Text as="h3" className={actionsStyles.title}>{ActionsResources.actions}</Text>
      <SearchBox
        disabled={!threadsInitialized}
        placeholder={SreAgentResources.search}
        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchString(data.value ?? ''))}
        className={actionsStyles.searchBox}
      />
      <Shimmer isDataLoaded={threadsInitialized || isLoading}>
            <ActionCardList actions={filteredActions} />
      </Shimmer>
    </div>
  );
};

const ActionCardList = memo(
  ({
    actions,
  }: {
    actions: Action[];
  }) => {
    const actionsStyles = useThreadActionsStyles();

    return (
      <div className={actionsStyles.actionsList}>
        {actions.map(action => {
          return <ActionCard key={action.id} action={action} />;
        })}
      </div>
    );
  }
);

const getIcon = (action: Action) => {
    const actionsStyles = useThreadActionsStyles();

    switch(action?.status) {
        case ActionStatus.InProgress:
        case ActionStatus.Pending:
            return  <div className={actionsStyles.pendingIcon}>
                        <ArrowSync16Filled primaryFill="white" />
                    </div>;
        case ActionStatus.Failed:
            return <div className={actionsStyles.errorIcon}>
                      <Dismiss16Filled primaryFill="white" />
            </div>;
        case ActionStatus.Completed:
            return <div className={actionsStyles.completedIcon}>
                      <CheckmarkCircle16Filled primaryFill="white" />
                </div>;
        default:
            return <></>;
    }
}

const ActionCard = memo(
  ({ action }: { action: Action; }) => {
    const actionsStyles = useThreadActionsStyles();

    return (
        <Card id={action.id} className={actionsStyles.card}>
            <CardHeader header={action?.title} className={actionsStyles.cardHeader}/>
            <div className={actionsStyles.iconStatusRow}>
                {getIcon(action)}
                {action?.status}
            </div>
            <CardFooter>
                {getSafeDateTime(action.timeStamp).toLocaleString()}
            </CardFooter>
        </Card>
        
    );
  }
);

ActionCardList.displayName = 'ActionCardList';
ActionCard.displayName = 'ActionCard';
