import { Button } from '@fluentui/react-button';
import { makeStyles, tokens } from '@fluentui/react-components';
import { Settings16Regular } from '@fluentui/react-icons';
import { Menu, MenuItemCheckbox, MenuList, MenuPopover, MenuTrigger } from '@fluentui/react-menu';
import { Spinner } from '@fluentui/react-spinner';
import { Text } from '@fluentui/react-text';
import { Tooltip } from '@fluentui/react-tooltip';
import { memo, useMemo } from 'react';
import { FormattedMessage } from 'react-intl';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
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
    restrictionMessage: {
        color: tokens.colorPaletteRedForeground2,
        fontStyle: 'italic',
        marginTop: '8px',
        padding: '6px 8px',
        backgroundColor: tokens.colorPaletteRedBackground1,
        borderRadius: '4px',
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
        agentModesInfo,
        isUpdatingAgentMode,
        threadAgentMode,
        isButtonDisabled,
        buttonTooltipText,
        showButtonLoadingSpinner,
        updateThreadAgentMode,
        updatingAgentModeError,
        getAgentModeInfo,
    } = useAgentModeSelector(props);

    const { menuSurface, restrictionMessage, currentModeValue, errorMessage } = useAgentModeSelectorStyles();

    const checkedValues = useMemo(() => {
        const checked: Record<string, string[]> = {};
        if (threadAgentMode) {
            checked['agentMode'] = [threadAgentMode];
        }
        return checked;
    }, [threadAgentMode]);

    return (
        <Menu positioning={'after-top'}>
            <Tooltip content={buttonTooltipText} relationship="label">
                <MenuTrigger>
                    <Button
                        style={{ fontSize: '13px', padding: '2px 4px', paddingRight: '8px' }}
                        appearance="outline"
                        icon={showButtonLoadingSpinner ? <Spinner size="tiny" /> : <Settings16Regular />}
                        disabled={isButtonDisabled}
                    >
                        <FormattedMessage {...AgentModeResources.agentMode} />
                    </Button>
                </MenuTrigger>
            </Tooltip>
            <MenuPopover className={menuSurface}>
                <Text size={100} className={errorMessage}>
                    {updatingAgentModeError}
                </Text>
                <MenuList
                    checkedValues={checkedValues}
                    onCheckedValueChange={(_, data) => {
                        const selectedMode = data.checkedItems?.[0];
                        if (selectedMode) {
                            updateThreadAgentMode(selectedMode);
                        }
                    }}
                >
                    {agentModes.map((mode, index) => {
                        const modeInfo = getAgentModeInfo(mode.toLowerCase());
                        const showSelectedStyle =
                            equals(mode, threadAgentMode || '', AntUxStringComparison.IgnoreCase) && !isUpdatingAgentMode;
                        return (
                            <MenuItemCheckbox
                                key={index}
                                name={'agentMode'}
                                subText={modeInfo.description}
                                value={modeInfo.mode.toLowerCase()}
                                onClick={() => updateThreadAgentMode(mode)}
                                disabled={isUpdatingAgentMode}
                            >
                                <Text
                                    className={showSelectedStyle ? currentModeValue : ''}
                                    weight={showSelectedStyle ? 'semibold' : undefined}
                                    size={showSelectedStyle ? 300 : undefined}
                                >
                                    {modeInfo.displayName}
                                </Text>
                            </MenuItemCheckbox>
                        );
                    })}
                </MenuList>
                {agentModesInfo.info && (
                    <Text size={200} className={restrictionMessage}>
                        {agentModesInfo.info}
                    </Text>
                )}
            </MenuPopover>
        </Menu>
    );
};

export default memo(AgentModeSelector);
