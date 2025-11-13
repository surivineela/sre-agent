import { Body1, Caption1, Caption1Stronger } from '@fluentui-copilot/react-copilot';
import { EntityCard, EntityTitle } from '@fluentui-copilot/react-entity-cards';
import { Badge, BadgeProps, Button, Menu, MenuItem, MenuList, MenuPopover, MenuTrigger, tokens } from '@fluentui/react-components';
import { ArrowRightRegular, Clock28Regular, MoreHorizontalRegular } from '@fluentui/react-icons';
import * as React from 'react';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router-dom';
import { ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { GroupMessageKey } from '../ScheduledTasks/V2/ScheduledTasksUtilities';

export interface ScheduledTaskExecutionCardProps {
    name: string;
    description: string;
    secondaryText: string;
    footer: {
        status: JSX.Element;
        timestamp?: JSX.Element;
        schedule?: JSX.Element;
        messageGrouping?: GroupMessageKey;
    };
    actions?: {
        menuItems?: JSX.Element;
        additionalActions?: JSX.Element;
    };
}

const ScheduledTaskChatMessage: React.FC<ScheduledTaskExecutionCardProps> = ({ name, description, secondaryText, footer, actions }) => {
    const intl = useIntl();
    const location = useLocation();
    const navigate = useNavigate();

    const badgeProps: Partial<BadgeProps> = {
        appearance: 'outline',
        color: 'important',
        size: 'large',
        style: { padding: tokens.spacingVerticalM },
    };

    return (
        <div>
            <EntityCard
                orientation="vertical"
                role="group"
                style={{ maxWidth: 'unset' }}
                entityTitle={
                    <EntityTitle
                        media={<Clock28Regular />}
                        primaryText={name}
                        secondaryText={secondaryText}
                        actions={
                            <>
                                {actions?.additionalActions}
                                <Menu>
                                    <MenuTrigger>
                                        <Button
                                            appearance="transparent"
                                            aria-label={intl.formatMessage(SreAgentResources.moreOptions)}
                                            icon={<MoreHorizontalRegular />}
                                        />
                                    </MenuTrigger>
                                    <MenuPopover>
                                        <MenuList>
                                            {actions?.menuItems}
                                            <MenuItem
                                                icon={<ArrowRightRegular />}
                                                onClick={() => navigate({ ...location, pathname: '/views/scheduledtasks' })}
                                            >
                                                {intl.formatMessage(ScheduledTasksResources.goToScheduledTasksButtonText)}
                                            </MenuItem>
                                        </MenuList>
                                    </MenuPopover>
                                </Menu>
                            </>
                        }
                    />
                }
                content={{
                    style: {
                        width: '100%',
                        maxWidth: 'unset',
                        maxHeight: 'unset',
                        borderRadius: 'unset',
                        flexDirection: 'column',
                        gap: tokens.spacingVerticalXL,
                        marginBottom: 'unset',
                    },
                }}
            >
                <Body1>{description}</Body1>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                    {footer.status}
                    {footer.timestamp && <Badge {...badgeProps}>{footer.timestamp}</Badge>}
                    {footer.schedule && <Badge {...badgeProps}>{footer.schedule}</Badge>}
                    {footer.messageGrouping && (
                        <Badge {...badgeProps}>
                            <Caption1>
                                {intl.formatMessage(ScheduledTasksResources.messageGroupingBadgeText)}{' '}
                                <Caption1Stronger>
                                    {footer.messageGrouping === GroupMessageKey.SameThread
                                        ? intl.formatMessage(ScheduledTasksResources.useSameThread)
                                        : intl.formatMessage(ScheduledTasksResources.newThreadForEachRun)}
                                </Caption1Stronger>
                            </Caption1>
                        </Badge>
                    )}
                </div>
            </EntityCard>
        </div>
    );
};

export default memo(ScheduledTaskChatMessage);
