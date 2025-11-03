import { Menu, MenuButtonProps, MenuItem, MenuList, MenuPopover, MenuTrigger, SplitButton } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';
import { memo, useRef } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
import { EntityTypeExt } from './ExtendedAgentCreationDialog/types';

interface MenuOption {
    label: string;
    description: string | undefined;
    icon: React.ReactNode;
    entityType: EntityTypeExt;
}

export interface CreateButtonProps {
    handleCreateItemStandalone: (targetType: EntityTypeExt) => void;
    disabled?: boolean;
}

const CreateButton = memo(({ handleCreateItemStandalone, disabled }: CreateButtonProps) => {
    const { menuItemWithIcon, menuItemContent } = useExtendedAgentGraphStyles();
    const intl = useIntl();
    const createButtonRef = useRef<HTMLButtonElement>(null);
    const options: MenuOption[] = [
        {
            label: intl.formatMessage(ExtendedAgentsGraphResources.subagent),
            description: undefined,
            icon: <EntityIcon type="agent" />,
            entityType: 'agent' as EntityTypeExt,
        },
        {
            label: intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeIncident),
            description: undefined,
            icon: <EntityIcon type="incidentTrigger" />,
            entityType: 'incidentTrigger' as EntityTypeExt,
        },
        {
            label: intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeScheduled),
            description: undefined,
            icon: <EntityIcon type="scheduledTrigger" />,
            entityType: 'scheduledTrigger' as EntityTypeExt,
        },
        {
            label: intl.formatMessage(ExtendedAgentsGraphResources.kustoTool),
            description: undefined,
            icon: <EntityIcon type="toolWithGear" />,
            entityType: 'tool' as EntityTypeExt,
        },
    ];

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
                        />
                    ))}
                </MenuList>
            </MenuPopover>
        </Menu>
    );
});

CreateButton.displayName = 'CreateButton';

export default CreateButton;
