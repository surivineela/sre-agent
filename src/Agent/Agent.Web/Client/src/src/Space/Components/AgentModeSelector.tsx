import { makeStyles, tokens } from '@fluentui/react-components';
import { Settings32Regular } from '@fluentui/react-icons';
import { Menu, MenuItemCheckbox, MenuList, MenuPopover, MenuTrigger } from '@fluentui/react-menu';
import { Spinner } from '@fluentui/react-spinner';
import { Text } from '@fluentui/react-text';
import { memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { AgentModeResources } from '../../Strings/SREAgentResources';
import { IAgentModeSelectorProps } from '../Contracts/Activities';
import { useAgentModeSelector } from '../Hooks/useAgentModeSelector';

const useAgentModeSelectorStyles = makeStyles({
    menuSurface: {
        width: '280px',
        padding: '12px',
        display: 'flex',
        flexDirection: 'column',
        boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
    },
    currentModeValue: {
        color: tokens.colorBrandForeground1,
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    errorMessage: {
        color: tokens.colorPaletteRedForeground2,
        marginLeft: '27px',
    },
});

const AgentModeSelector = (props: IAgentModeSelectorProps) => {
    const {
        agentModes,
        isUpdatingAgentMode,
        threadAgentMode,
        isButtonDisabled,
        buttonTooltipText,
        showButtonLoadingSpinner,
        updateThreadAgentMode,
        updatingAgentModeError,
    } = useAgentModeSelector(props);
    const { logAmplitudeControlEvent } = useAzPortalContext();
    const { menuSurface, currentModeValue, errorMessage } = useAgentModeSelectorStyles();
    const intl = useIntl();

    const { canWriteThreads } = useUserPermissions();

    const currentModeDisplay = useMemo(() => (threadAgentMode ? threadAgentMode.toLowerCase() : 'unknown'), [threadAgentMode]);

    const noPermissionTooltip = intl.formatMessage(AgentModeResources.agentModeNoPermission, {
        mode: currentModeDisplay,
    });

    // Fluent Menu expects a checkedValues map where key is the checkbox group name
    const checkedValues = useMemo(
        () => ({
            agentMode: threadAgentMode ? [threadAgentMode.toLowerCase()] : [],
        }),
        [threadAgentMode]
    );

    return (
        <Menu positioning="after-top">
            <MenuTrigger>
                <span style={{ height: '100%' }}>
                    <PermissionedButton
                        canPerform={canWriteThreads}
                        noPermissionTooltip={noPermissionTooltip}
                        allowedTooltip={buttonTooltipText}
                        disabledReason={isButtonDisabled}
                        icon={showButtonLoadingSpinner ? <Spinner size="tiny" /> : <Settings32Regular />}
                        shape={'rounded'}
                        appearance={'subtle'}
                        style={{ marginRight: tokens.spacingHorizontalS, height: '100%' }}
                    />
                </span>
            </MenuTrigger>
            <MenuPopover className={canWriteThreads ? menuSurface : undefined}>
                {canWriteThreads && (
                    <>
                        <Text size={100} className={errorMessage}>
                            {updatingAgentModeError}
                        </Text>
                        <MenuList
                            checkedValues={checkedValues}
                            onCheckedValueChange={(_, data) => {
                                const selectedMode = data.checkedItems?.[0];
                                if (selectedMode) {
                                    updateThreadAgentMode(selectedMode);
                                    logAmplitudeControlEvent({
                                        targetType: 'radioButton',
                                        targetAction: 'clicked',
                                        targetName: 'threadAgentMode',
                                        targetFriendlyName: 'Thread agent mode',
                                        valueObjectName: selectedMode,
                                        valueObjectFriendlyName: selectedMode,
                                    });
                                }
                            }}
                        >
                            {agentModes.map((mode, index) => {
                                const showSelectedStyle =
                                    equals(mode.name, threadAgentMode || '', AntUxStringComparison.IgnoreCase) && !isUpdatingAgentMode;
                                return (
                                    <MenuItemCheckbox
                                        key={index}
                                        name="agentMode"
                                        subText={mode.description}
                                        value={mode.name.toLowerCase()}
                                        onClick={() => updateThreadAgentMode(mode.name)}
                                        disabled={isUpdatingAgentMode}
                                    >
                                        <Text
                                            className={showSelectedStyle ? currentModeValue : ''}
                                            weight={showSelectedStyle ? 'semibold' : undefined}
                                            size={showSelectedStyle ? 300 : undefined}
                                        >
                                            {mode.displayName}
                                        </Text>
                                    </MenuItemCheckbox>
                                );
                            })}
                        </MenuList>
                    </>
                )}
            </MenuPopover>
        </Menu>
    );
};

export default memo(AgentModeSelector);
