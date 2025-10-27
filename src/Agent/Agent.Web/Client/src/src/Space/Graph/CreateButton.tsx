import { Menu, MenuButtonProps, MenuItem, MenuList, MenuPopover, MenuTrigger, SplitButton, tokens } from '@fluentui/react-components';
import { AddRegular, AgentsRegular, TimerRegular, WarningRegular, WrenchSettingsRegular } from '@fluentui/react-icons';
import { memo, useRef } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityType } from './ExtendedAgentCreationDialog/types';

interface MenuOption {
    label: string;
    description: string | undefined;
    icon: React.ReactNode;
    backgroundColor: string;
    entityType: EntityType;
}

export interface CreateButtonProps {
    handleCreateItemStandalone: (targetType: EntityType) => void;
    disabled?: boolean;
}

const CreateButton = memo(({ handleCreateItemStandalone, disabled }: CreateButtonProps) => {
    const { menuItemWithIcon, menuIconWrapper, menuItemContent } = useExtendedAgentGraphStyles();
    const intl = useIntl();
    const createButtonRef = useRef<HTMLButtonElement>(null);
    const options: MenuOption[] = [
        {
            label: intl.formatMessage(ExtendedAgentsGraphResources.subagent),
            description: undefined,
            icon: <AgentsRegular style={{ color: tokens.colorPaletteLavenderForeground2 }} />,
            backgroundColor: tokens.colorPaletteLavenderBackground2,
            entityType: 'agent' as EntityType,
        },
        {
            label: intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeIncident),
            description: undefined,
            icon: <WarningRegular style={{ color: tokens.colorPaletteCranberryForeground2 }} />,
            backgroundColor: tokens.colorPaletteCranberryBackground2,
            entityType: 'trigger' as EntityType,
        },
        {
            label: intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeScheduled),
            description: undefined,
            icon: <TimerRegular style={{ color: tokens.colorPaletteForestForeground2 }} />,
            backgroundColor: tokens.colorPaletteForestBackground2,
            entityType: 'trigger' as EntityType,
        },
        {
            label: intl.formatMessage(ExtendedAgentsGraphResources.kustoTool),
            description: undefined,
            icon: <WrenchSettingsRegular style={{ color: tokens.colorPaletteLilacForeground2 }} />,
            backgroundColor: tokens.colorPaletteLilacBackground2,
            entityType: 'tool' as EntityType,
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
                            icon={
                                <div className={menuIconWrapper} style={{ backgroundColor: option.backgroundColor }}>
                                    {option.icon}
                                </div>
                            }
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
