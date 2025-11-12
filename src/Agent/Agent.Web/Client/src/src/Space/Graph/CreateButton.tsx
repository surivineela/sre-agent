import { Menu, MenuButtonProps, MenuItem, MenuList, MenuPopover, MenuTrigger, SplitButton } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';
import { memo, useMemo, useRef } from 'react';
import { useIntl } from 'react-intl';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
import { EntityTypeExt } from './ExtendedAgentCreationDialog/types';

interface MenuOption {
    label: string;
    icon: React.ReactNode;
    entityType: EntityTypeExt;
    description?: string;
    disabled?: boolean;
}

export interface CreateButtonProps {
    handleCreateItemStandalone: (targetType: EntityTypeExt) => void;
    disableCreateMetaAgent?: boolean;
    disabled?: boolean;
}

const CreateButton = memo(({ handleCreateItemStandalone, disableCreateMetaAgent, disabled }: CreateButtonProps) => {
    const allowMetaAgentOverride = useConfigSetting(SettingNames.AllowMetaAgentOverride);
    const { menuItemWithIcon, menuItemContent } = useExtendedAgentGraphStyles();
    const intl = useIntl();
    const createButtonRef = useRef<HTMLButtonElement>(null);
    const options: MenuOption[] = useMemo(() => {
        const metaAgentOption = allowMetaAgentOverride
            ? [
                  {
                      label: intl.formatMessage(ExtendedAgentsGraphResources.metaAgentCreateMenuLabel),
                      description: intl.formatMessage(ExtendedAgentsGraphResources.metaAgentCreateMenuDescription),
                      icon: <EntityIcon type="metaAgent" iconStyle={{ height: 24, width: 24 }} />,
                      entityType: 'metaAgent' as EntityTypeExt,
                      disabled: disableCreateMetaAgent,
                  },
              ]
            : [];

        return [
            {
                label: intl.formatMessage(ExtendedAgentsGraphResources.subAgentCreateMenuLabel),
                description: intl.formatMessage(ExtendedAgentsGraphResources.subAgentCreateMenuDescription),
                icon: <EntityIcon type="agent" />,
                entityType: 'agent' as EntityTypeExt,
            },
            ...metaAgentOption,
            {
                label: intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeIncident),
                description: intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerCreateMenuDescription),
                icon: <EntityIcon type="incidentTrigger" />,
                entityType: 'incidentTrigger' as EntityTypeExt,
            },
            {
                label: intl.formatMessage(ExtendedAgentsGraphResources.scheduledTaskTriggerCreateMenuLabel),
                description: intl.formatMessage(ExtendedAgentsGraphResources.scheduledTaskTriggerCreateMenuDescription),
                icon: <EntityIcon type="scheduledTask" />,
                entityType: 'scheduledTask' as EntityTypeExt,
            },
            {
                label: intl.formatMessage(ExtendedAgentsGraphResources.kustoToolCreateMenuLabel),
                description: intl.formatMessage(ExtendedAgentsGraphResources.kustoToolCreateMenuDescription),
                icon: <EntityIcon type="toolWithGear" />,
                entityType: 'tool' as EntityTypeExt,
            },
        ];
    }, [intl, disableCreateMetaAgent, allowMetaAgentOverride]);

    return (
        <Menu positioning={{ target: createButtonRef.current, position: 'below', align: 'start' }}>
            <MenuTrigger disableButtonEnhancement>
                {(triggerProps: MenuButtonProps) => (
                    <SplitButton
                        ref={createButtonRef}
                        icon={<AddRegular />}
                        appearance="primary"
                        menuButton={triggerProps}
                        primaryActionButton={{ onClick: () => handleCreateItemStandalone('agent') }}
                        disabled={disabled}
                    >
                        {intl.formatMessage(ExtendedAgentsGraphResources.create)}
                    </SplitButton>
                )}
            </MenuTrigger>
            <MenuPopover>
                <MenuList>
                    {options.map(option => (
                        <MenuItem
                            key={option.label}
                            className={menuItemWithIcon}
                            icon={<>{option.icon}</>}
                            content={<div className={menuItemContent}>{option.label}</div>}
                            subText={option.description}
                            onClick={() => handleCreateItemStandalone(option.entityType)}
                            disabled={option.disabled}
                        />
                    ))}
                </MenuList>
            </MenuPopover>
        </Menu>
    );
});

CreateButton.displayName = 'CreateButton';

export default CreateButton;
