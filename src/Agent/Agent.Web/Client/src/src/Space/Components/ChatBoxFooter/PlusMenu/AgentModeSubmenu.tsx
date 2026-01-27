import { Menu, MenuItem, MenuList, MenuPopover, MenuTrigger, Text } from '@fluentui/react-components';
import { Checkmark16Regular, Settings20Regular } from '@fluentui/react-icons';
import { FC, memo, useCallback } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { AgentModeResources } from '../../../../Strings/SREAgentResources';
import { useAgentModeSelector } from '../../../Hooks/useAgentModeSelector';
import { usePlusMenuStyles } from '../styles';

export interface AgentModeSubmenuProps {
    threadId?: string | null;
    disabled: boolean;
}

export const AgentModeSubmenu: FC<AgentModeSubmenuProps> = memo(({ threadId, disabled }) => {
    const intl = useIntl();
    const styles = usePlusMenuStyles();
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const { agentModes, threadAgentMode, isUpdatingAgentMode, updateThreadAgentMode } = useAgentModeSelector({
        id: 'agent-mode-submenu',
        threadId: threadId ?? '',
        disabled,
    });

    const handleModeSelect = useCallback(
        (modeName: string) => {
            updateThreadAgentMode(modeName);
            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'agentModeFromPlusMenu',
                targetFriendlyName: 'Agent mode from plus menu',
                valueObjectName: modeName,
                valueObjectFriendlyName: modeName,
            });
        },
        [updateThreadAgentMode, logAmplitudeControlEvent]
    );

    return (
        <Menu positioning="after">
            <MenuTrigger disableButtonEnhancement>
                <MenuItem icon={<Settings20Regular />}>{intl.formatMessage(AgentModeResources.agentMode)}</MenuItem>
            </MenuTrigger>
            <MenuPopover className={styles.submenuPopover}>
                <MenuList>
                    {agentModes.map(mode => {
                        const isSelected = mode.name.toLowerCase() === threadAgentMode?.toLowerCase();
                        return (
                            <MenuItem
                                key={mode.name}
                                onClick={() => handleModeSelect(mode.name)}
                                disabled={isUpdatingAgentMode || disabled}
                                secondaryContent={isSelected ? <Checkmark16Regular className={styles.checkmarkIcon} /> : undefined}
                            >
                                <div className={styles.submenuItemContent}>
                                    <Text>{mode.displayName}</Text>
                                    {mode.description && <Text className={styles.submenuItemDescription}>{mode.description}</Text>}
                                </div>
                            </MenuItem>
                        );
                    })}
                </MenuList>
            </MenuPopover>
        </Menu>
    );
});
