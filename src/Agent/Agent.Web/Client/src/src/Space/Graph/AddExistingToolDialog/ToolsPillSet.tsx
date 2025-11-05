import { Badge, Button, Link, makeStyles, tokens } from '@fluentui/react-components';
import { DismissRegular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';

export interface ToolsPillSetProps {
    toolNames: string[];
    onRemoveTool: (toolName: string) => void;
    onClearAll: () => void;
}

export const ToolsPillSet: FC<ToolsPillSetProps> = ({ toolNames, onRemoveTool, onClearAll }) => {
    const intl = useIntl();
    const styles = useToolPillSetStyles();

    if (toolNames.length === 0) {
        return null;
    }
    return (
        <div className={styles.root}>
            {toolNames.map(toolName => (
                <ToolPill key={toolName} toolName={toolName} onRemove={onRemoveTool} />
            ))}
            <Link onClick={onClearAll} className={styles.clearAllLink}>
                {intl.formatMessage(ExtendedAgentsGraphResources.clearAll)}
            </Link>
        </div>
    );
};

interface ToolPillProps {
    toolName: string;
    onRemove: (toolName: string) => void;
}

const ToolPill: FC<ToolPillProps> = ({ toolName, onRemove }) => {
    const intl = useIntl();
    const styles = useToolPillSetStyles();
    return (
        <Badge size="medium" className={styles.toolPill}>
            {toolName}
            <Button
                appearance="transparent"
                aria-label={intl.formatMessage(ExtendedAgentsGraphResources.removeToolWithName, { toolName })}
                onClick={() => onRemove(toolName)}
                className={styles.toolPillRemoveButton}
                icon={<DismissRegular className={styles.toolPillRemoveButtonIcon} />}
            />
        </Badge>
    );
};

const useToolPillSetStyles = makeStyles({
    root: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '8px',
    },
    clearAllLink: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
    },
    toolPill: {
        display: 'flex',
        alignItems: 'center',
        gap: '2px',
        backgroundColor: tokens.colorNeutralBackground2,
        color: tokens.colorNeutralForeground2,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        paddingRight: '0px',
    },
    toolPillRemoveButton: {
        width: '18px',
        height: '18px',
        minWidth: '18px',
        padding: '1px',
        marginRight: '2px',
        zIndex: 1,
        borderRadius: `${tokens.borderRadiusCircular} !important`,
    },
    toolPillRemoveButtonIcon: {
        height: '12px',
        width: '12px',
    },
});
