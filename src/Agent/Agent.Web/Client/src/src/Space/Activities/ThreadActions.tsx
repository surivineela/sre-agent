import { Button, Card, CardFooter, CardHeader, InputOnChangeData, SearchBox, SearchBoxChangeEvent, Text } from '@fluentui/react-components';
import {
    ArrowSync16Filled,
    CheckmarkCircle16Filled,
    Dismiss16Filled,
    PanelRightContractRegular,
    PanelRightExpandRegular,
} from '@fluentui/react-icons';
import { Shimmer } from '@fluentui/react/lib/Shimmer';
import debounce from 'lodash/debounce';
import { FC, memo, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { Action, ActionStatus } from '../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { ActionsResources, ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import Fade from '../Components/Fade';
import { IThreadActivitiesProps } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { useActions } from '../Hooks/useActions';
import { actionSearchBoxStyle, getExpandCollapseButtonStyles, shimmerStyle, useThreadActionsStyles } from '../Styles/Activities.styles';

export const ThreadActions: FC<IThreadActivitiesProps> = (props: IThreadActivitiesProps) => {
    const { threadContentAndActionKey } = useContext(AgentContext);

    const { root } = useThreadActionsStyles();

    return (
        <div key={threadContentAndActionKey} className={root}>
            <ThreadActionsContent {...props} />
        </div>
    );
};

const expandCollapseButtonStyles = getExpandCollapseButtonStyles('right');

const ThreadActionsContent: FC<IThreadActivitiesProps> = (props: IThreadActivitiesProps) => {
    const { thread, collapsed, setCollapsed } = props;
    const { actions, isLoading } = useActions(thread?.id);
    const intl = useIntl();

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
        <div className={actionsStyles.content}>
            <div style={expandCollapseButtonStyles.container}>
                <Button
                    style={expandCollapseButtonStyles.button}
                    icon={collapsed ? <PanelRightExpandRegular /> : <PanelRightContractRegular />}
                    onClick={() => setCollapsed(!collapsed)}
                    aria-label={intl.formatMessage(ActivitiesResources.hideThreadActionsButtonText)}
                    appearance="transparent"
                />
            </div>
            <Fade visible={!collapsed} unmountOnExit>
                <Text as="h3" className={actionsStyles.title}>
                    {intl.formatMessage(ActionsResources.actions)}
                </Text>
            </Fade>
            <Fade visible={!collapsed} unmountOnExit>
                <SearchBox
                    style={actionSearchBoxStyle}
                    placeholder={intl.formatMessage(SreAgentResources.search)}
                    onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchString(data.value ?? ''))}
                />
            </Fade>
            <Fade visible={!collapsed} unmountOnExit>
                <Shimmer isDataLoaded={!isLoading} style={shimmerStyle}>
                    <ActionCardList actions={filteredActions} />
                </Shimmer>
            </Fade>
        </div>
    );
};

const ActionCardList = memo(({ actions }: { actions: Action[] }) => {
    const actionsStyles = useThreadActionsStyles();

    return (
        <div className={actionsStyles.actionsList}>
            {actions.map(action => {
                return <ActionCard key={action.id} action={action} />;
            })}
        </div>
    );
});

const ActionIcon = ({ action }: { action: Action }) => {
    const actionsStyles = useThreadActionsStyles();

    switch (action?.status) {
        case ActionStatus.InProgress:
        case ActionStatus.Pending:
            return (
                <div className={actionsStyles.pendingIcon}>
                    <ArrowSync16Filled primaryFill="white" />
                </div>
            );
        case ActionStatus.Failed:
            return (
                <div className={actionsStyles.errorIcon}>
                    <Dismiss16Filled primaryFill="white" />
                </div>
            );
        case ActionStatus.Completed:
            return (
                <div className={actionsStyles.completedIcon}>
                    <CheckmarkCircle16Filled primaryFill="white" />
                </div>
            );
        default:
            return <></>;
    }
};

const ActionCard = memo(({ action }: { action: Action }) => {
    const actionsStyles = useThreadActionsStyles();

    return (
        <Card id={action.id} className={actionsStyles.card}>
            <CardHeader header={action?.title} className={actionsStyles.cardHeader} />
            <div className={actionsStyles.iconStatusRow}>
                <ActionIcon action={action} />
                {action?.status}
            </div>
            <CardFooter>{getSafeDateTime(action.timeStamp).toLocaleString()}</CardFooter>
        </Card>
    );
});

ActionCardList.displayName = 'ActionCardList';
ActionCard.displayName = 'ActionCard';
