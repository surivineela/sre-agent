import { tokens } from '@fluentui/react-components';
import {
    CheckmarkCircle20Filled,
    CircleOff20Filled,
    DismissCircle20Filled,
    ErrorCircle20Filled,
    Warning20Filled,
} from '@fluentui/react-icons';
import { McpConnectorStatus } from './Connectors';

export const getStatusIcon = (status: string) => {
    let color: string;
    let icon: React.ReactElement;

    switch (status) {
        case McpConnectorStatus.Connected:
            color = tokens.colorPaletteGreenForeground1;
            icon = <CheckmarkCircle20Filled style={{ color }} />;
            break;
        case McpConnectorStatus.Failed:
            color = tokens.colorPaletteRedForeground1;
            icon = <ErrorCircle20Filled style={{ color }} />;
            break;
        case McpConnectorStatus.Disconnected:
            color = tokens.colorPaletteRedForeground1;
            icon = <CircleOff20Filled style={{ color }} />;
            break;
        case McpConnectorStatus.Initializing:
            color = tokens.colorPaletteYellowForeground1;
            icon = <Warning20Filled style={{ color }} />;
            break;
        case McpConnectorStatus.Error:
            color = tokens.colorPaletteRedForeground1;
            icon = <DismissCircle20Filled style={{ color }} />;
            break;
        default:
            color = tokens.colorNeutralForeground2;
            icon = <></>;
            break;
    }

    return { icon, color };
};
