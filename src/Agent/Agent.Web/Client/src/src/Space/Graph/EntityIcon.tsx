import { tokens } from '@fluentui/react-components';
import {
    AgentsRegular,
    PlayRegular,
    PlugConnectedRegular,
    TimerRegular,
    WarningRegular,
    WrenchRegular,
    WrenchSettingsRegular,
} from '@fluentui/react-icons';
import { FC, useMemo } from 'react';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
export interface EntityIconProps {
    type: 'agent' | 'scheduledTask' | 'incidentTrigger' | 'genericTrigger' | 'tool' | 'toolWithGear' | 'connector';
    shorthandStyle?: {
        wrapperSize: number;
        iconSize: number;
        borderRadius: number;
    };
    wrapperStyle?: React.CSSProperties;
    iconStyle?: React.CSSProperties;
}

export const EntityIcon: FC<EntityIconProps> = ({ type, shorthandStyle: size, wrapperStyle, iconStyle }) => {
    const { menuIconWrapper } = useExtendedAgentGraphStyles();
    const { backgroundColor, foregroundColor, Icon } = useMemo(() => {
        switch (type) {
            case 'agent':
                return {
                    backgroundColor: tokens.colorPaletteLavenderBackground2,
                    foregroundColor: tokens.colorPaletteLavenderForeground2,
                    Icon: AgentsRegular,
                };
            case 'scheduledTask':
                return {
                    backgroundColor: tokens.colorPaletteForestBackground2,
                    foregroundColor: tokens.colorPaletteForestForeground2,
                    Icon: TimerRegular,
                };
            case 'incidentTrigger':
                return {
                    backgroundColor: tokens.colorPaletteCranberryBackground2,
                    foregroundColor: tokens.colorPaletteCranberryForeground2,
                    Icon: WarningRegular,
                };
            case 'genericTrigger':
                return {
                    backgroundColor: tokens.colorNeutralBackground3,
                    foregroundColor: tokens.colorNeutralForeground3,
                    Icon: PlayRegular,
                };
            case 'tool':
                return {
                    backgroundColor: tokens.colorPaletteLilacBackground2,
                    foregroundColor: tokens.colorPaletteLilacForeground2,
                    Icon: WrenchRegular,
                };
            case 'toolWithGear':
                return {
                    backgroundColor: tokens.colorPaletteLilacBackground2,
                    foregroundColor: tokens.colorPaletteLilacForeground2,
                    Icon: WrenchSettingsRegular,
                };
            case 'connector':
                return {
                    backgroundColor: tokens.colorPaletteGreenBackground2,
                    foregroundColor: tokens.colorPaletteGreenForeground2,
                    Icon: PlugConnectedRegular,
                };
        }
    }, [type]);

    const wrapperStyleFinal = useMemo(() => {
        const sizeStyle = size
            ? { height: `${size.wrapperSize}px`, width: `${size.wrapperSize}px`, borderRadius: `${size.borderRadius}px` }
            : {};
        const style = { backgroundColor: backgroundColor, ...wrapperStyle, ...sizeStyle };
        return style;
    }, [backgroundColor, size, wrapperStyle]);

    const iconStyleFinal = useMemo(() => {
        const sizeStyle = size ? { height: `${size.iconSize}px`, width: `${size.iconSize}px` } : {};
        const style = { color: foregroundColor, ...iconStyle, ...sizeStyle };
        return style;
    }, [foregroundColor, size, iconStyle]);

    if (!Icon) {
        return null;
    }

    return (
        <div className={menuIconWrapper} style={wrapperStyleFinal}>
            <Icon style={iconStyleFinal} />
        </div>
    );
};
