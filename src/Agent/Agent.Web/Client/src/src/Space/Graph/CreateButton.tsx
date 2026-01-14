import { Menu, MenuButtonProps, MenuItem, MenuList, MenuPopover, MenuTrigger, SplitButton } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';
import { memo, useMemo, useRef } from 'react';
import { useIntl } from 'react-intl';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { EntityTypeExt } from './ExtendedAgentCreationDialog/types';

export interface CreateButtonProps {
    handleCreateItemStandalone: (targetType: EntityTypeExt) => void;
    disableCreateMetaAgent?: boolean;
    disableCreateSubagent?: boolean;
    disableCreateSkill?: boolean;
    disabled?: boolean;
}

const CreateButton = memo(
    ({ handleCreateItemStandalone, disableCreateMetaAgent, disableCreateSubagent, disableCreateSkill, disabled }: CreateButtonProps) => {
        const allowMetaAgentOverride = useConfigSetting(SettingNames.AllowMetaAgentOverride);
        const showIncidentTriggerWithLearnings = useConfigSetting(SettingNames.ShowIncidentTriggerWithLearnings);
        const intl = useIntl();
        const createButtonRef = useRef<HTMLButtonElement>(null);

        const primaryAction = useMemo(() => (disableCreateSubagent ? 'skill' : 'agent'), [disableCreateSubagent]);

        return (
            <Menu positioning={{ target: createButtonRef.current, position: 'below', align: 'start' }}>
                <MenuTrigger disableButtonEnhancement>
                    {(triggerProps: MenuButtonProps) => (
                        <SplitButton
                            ref={createButtonRef}
                            icon={<AddRegular />}
                            appearance="primary"
                            menuButton={triggerProps}
                            primaryActionButton={{ onClick: () => handleCreateItemStandalone(primaryAction) }}
                            disabled={disabled}
                        >
                            {intl.formatMessage(ExtendedAgentsGraphResources.create)}
                        </SplitButton>
                    )}
                </MenuTrigger>
                <MenuPopover>
                    <MenuList>
                        <Menu>
                            <MenuTrigger disableButtonEnhancement>
                                <MenuItem>{intl.formatMessage(ExtendedAgentsGraphResources.agent)}</MenuItem>
                            </MenuTrigger>
                            <MenuPopover>
                                <MenuList>
                                    {allowMetaAgentOverride && (
                                        <MenuItem onClick={() => handleCreateItemStandalone('metaAgent')} disabled={disableCreateMetaAgent}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.metaAgentCreateMenuLabel)}
                                        </MenuItem>
                                    )}
                                    <MenuItem onClick={() => handleCreateItemStandalone('agent')} disabled={disableCreateSubagent}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.subAgentCreateMenuLabel)}
                                    </MenuItem>
                                </MenuList>
                            </MenuPopover>
                        </Menu>
                        <MenuItem onClick={() => handleCreateItemStandalone('skill')} disabled={disableCreateSkill}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.skillCreateMenuLabel)}
                        </MenuItem>
                        <Menu>
                            <MenuTrigger disableButtonEnhancement>
                                <MenuItem>{intl.formatMessage(ExtendedAgentsGraphResources.trigger)}</MenuItem>
                            </MenuTrigger>
                            <MenuPopover>
                                <MenuList>
                                    <MenuItem onClick={() => handleCreateItemStandalone('incidentTrigger')}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerCreateMenuLabel)}
                                    </MenuItem>
                                    {showIncidentTriggerWithLearnings && (
                                        <MenuItem onClick={() => handleCreateItemStandalone('incidentTriggerWithLearnings')}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerWithLearningsCreateMenuLabel)}
                                        </MenuItem>
                                    )}
                                    <MenuItem onClick={() => handleCreateItemStandalone('scheduledTask')}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.scheduledTaskTriggerCreateMenuLabel)}
                                    </MenuItem>
                                </MenuList>
                            </MenuPopover>
                        </Menu>
                        <Menu>
                            <MenuTrigger disableButtonEnhancement>
                                <MenuItem>{intl.formatMessage(ExtendedAgentsGraphResources.tool)}</MenuItem>
                            </MenuTrigger>
                            <MenuPopover>
                                <MenuList>
                                    <MenuItem onClick={() => handleCreateItemStandalone('tool')}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.kustoToolCreateMenuLabel)}
                                    </MenuItem>
                                    <MenuItem onClick={() => handleCreateItemStandalone('pythonTool')}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolCreateMenuLabel)}
                                    </MenuItem>
                                </MenuList>
                            </MenuPopover>
                        </Menu>
                    </MenuList>
                </MenuPopover>
            </Menu>
        );
    }
);

CreateButton.displayName = 'CreateButton';

export default CreateButton;
