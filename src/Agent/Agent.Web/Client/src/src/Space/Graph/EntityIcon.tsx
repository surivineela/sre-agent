import { Image, tokens } from '@fluentui/react-components';
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
    type:
        | 'agent'
        | 'metaAgent'
        | 'scheduledTask'
        | 'scheduledTaskRun'
        | 'incidentTrigger'
        | 'genericTrigger'
        | 'tool'
        | 'toolWithGear'
        | 'connector';
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
            case 'metaAgent':
                return {
                    backgroundColor: 'transparent',
                    foregroundColor: 'transparent',
                    Icon: ({ style }: { style?: React.CSSProperties }) => <Image src={'./SreAgent.svg'} style={style} aria-hidden="true" />,
                };
            case 'scheduledTask':
                return {
                    backgroundColor: tokens.colorPaletteForestBackground2,
                    foregroundColor: tokens.colorPaletteForestForeground2,
                    Icon: TimerRegular,
                };
            case 'scheduledTaskRun':
                return {
                    backgroundColor: tokens.colorPalettePlatinumBackground2,
                    foregroundColor: tokens.colorPalettePlatinumForeground2,
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
                    backgroundColor: tokens.colorPaletteGoldBackground2,
                    foregroundColor: tokens.colorPaletteGoldForeground2,
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
