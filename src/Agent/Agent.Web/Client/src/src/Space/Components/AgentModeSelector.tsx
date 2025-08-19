import { Button } from '@fluentui/react-button';
import { makeStyles, OverflowItem, tokens } from '@fluentui/react-components';
import { Settings16Regular } from '@fluentui/react-icons';
import { Menu, MenuItem, MenuItemCheckbox, MenuList, MenuPopover, MenuTrigger } from '@fluentui/react-menu';
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

    const { menuSurface, currentModeValue, errorMessage } = useAgentModeSelectorStyles();

    const checkedValues = useMemo(() => {
        const checked: Record<string, string[]> = {};
        if (threadAgentMode) {
            checked['agentMode'] = [threadAgentMode];
        }
        return checked;
    }, [threadAgentMode]);

    const ButtonComponent = () => {
        return (
            <Button
                style={{ fontSize: '13px', padding: '2px 8px 2px 4px', whiteSpace: 'nowrap' }}
                icon={showButtonLoadingSpinner ? <Spinner size="tiny" /> : <Settings16Regular />}
                disabled={isButtonDisabled}
            >
                <FormattedMessage {...AgentModeResources.agentMode} />
            </Button>
        );
    };

    return (
        <Menu positioning={'after-top'}>
            <Tooltip content={buttonTooltipText} relationship="label">
                <MenuTrigger>
                    {props.asOverflowItem ? (
                        <OverflowItem id={props.id}>
                            <div>
                                <ButtonComponent />
                            </div>
                        </OverflowItem>
                    ) : (
                        <MenuItem
                            icon={showButtonLoadingSpinner ? <Spinner size="tiny" /> : <Settings16Regular />}
                            disabled={isButtonDisabled}
                        >
                            <FormattedMessage {...AgentModeResources.agentMode} />
                        </MenuItem>
                    )}
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
                        const showSelectedStyle =
                            equals(mode.name, threadAgentMode || '', AntUxStringComparison.IgnoreCase) && !isUpdatingAgentMode;
                        return (
                            <MenuItemCheckbox
                                key={index}
                                name={'agentMode'}
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
            </MenuPopover>
        </Menu>
    );
};

export default memo(AgentModeSelector);
