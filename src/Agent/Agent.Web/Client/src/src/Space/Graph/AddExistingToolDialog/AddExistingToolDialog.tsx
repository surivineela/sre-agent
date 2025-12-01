import { Button, Dialog, DialogBody, DialogSurface, DialogTitle, ToolbarButton } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { PillSet } from '../Common/PillSet';
import { ToolsPicker } from '../Common/ToolsPicker/ToolsPicker';
import { useToolsPicker } from '../Common/ToolsPicker/useToolsPicker';
import { McpConnection } from '../ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { useAddExistingToolDialogStyles } from './AddExistingToolDialog.Styles';

export interface AddExistingToolDialogProps {
    onDismiss: () => void;
    addToolsToAgent: (agentName: string, toolsNames: string[]) => void;
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    mcpConnections?: McpConnection[];
    toolPickerInfo?: { agent: ExtendedAgent };
}

export const AddExistingToolDialog: FC<AddExistingToolDialogProps> = ({
    onDismiss,
    addToolsToAgent,
    existingTools,
    systemTools,
    mcpConnections,
    toolPickerInfo,
}) => {
    const agentName = useMemo(() => toolPickerInfo?.agent?.name, [toolPickerInfo?.agent?.name]);
    const excludedToolNames = useMemo(
        () => [
            ...(toolPickerInfo?.agent?.tools || []),
            ...(toolPickerInfo?.agent?.systemTools || []),
            ...(toolPickerInfo?.agent?.mcpTools || []),
        ],
        [toolPickerInfo?.agent?.tools, toolPickerInfo?.agent?.systemTools, toolPickerInfo?.agent?.mcpTools]
    );

    return (
        <Dialog
            open={!!toolPickerInfo}
            onOpenChange={(_, data) => {
                if (!data.open) {
                    onDismiss();
                }
            }}
        >
            <AddExistingToolDialogInner
                onDismiss={onDismiss}
                onSubmit={(selectedToolNames: string[]) => {
                    if (agentName) {
                        addToolsToAgent(agentName, selectedToolNames);
                    }
                    onDismiss();
                }}
                existingTools={existingTools}
                systemTools={systemTools}
                mcpConnections={mcpConnections}
                excludedToolNames={excludedToolNames}
            />
        </Dialog>
    );
};

interface AddExistingToolDialogInnerProps {
    onDismiss: () => void;
    onSubmit: (selectedToolNames: string[]) => void;
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    mcpConnections?: McpConnection[];
    excludedToolNames?: string[];
}

const AddExistingToolDialogInner: FC<AddExistingToolDialogInnerProps> = ({
    onDismiss,
    onSubmit,
    existingTools,
    systemTools,
    mcpConnections,
    excludedToolNames,
}) => {
    const intl = useIntl();
    const styles = useAddExistingToolDialogStyles();
    const [selectedToolNames, setSelectedToolNames] = useState<string[]>([]);

    const {
        toolType,
        onToolTypeChange,
        expandedGroupNames,
        onGroupExpandedChange,
        onSelectedToolChange,
        onSelectAllToolsInGroup,
        onSelectAllTools,
        onClearSelectedTools,
        searchQuery,
        setSearchQuery,
        groups,
        pillItems,
    } = useToolsPicker({
        selectedToolNames,
        setSelectedToolNames,
        existingTools,
        systemTools,
        mcpConnections,
        excludedToolNames,
    });

    return (
        <DialogSurface className={styles.dialogSurface}>
            <DialogBody className={styles.dialogBody}>
                <div className={styles.dialogTitleWrapper}>
                    <DialogTitle
                        className={styles.dialogTitle}
                        action={
                            <ToolbarButton
                                aria-label={intl.formatMessage(SreAgentResources.close)}
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={onDismiss}
                            />
                        }
                    >
                        {intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAddExistingTools)}
                    </DialogTitle>
                </div>
                <PillSet
                    items={pillItems}
                    onRemoveItem={toolName => onSelectedToolChange(toolName, false)}
                    onClearAll={onClearSelectedTools}
                />
                <div className={styles.dialogContentWrapper}>
                    <ToolsPicker
                        toolType={toolType}
                        onToolTypeChange={onToolTypeChange}
                        groups={groups}
                        expandedGroupNames={expandedGroupNames}
                        onGroupExpandedChange={onGroupExpandedChange}
                        selectedToolNames={selectedToolNames}
                        onSelectedToolChange={onSelectedToolChange}
                        onSelectAllToolsInGroup={onSelectAllToolsInGroup}
                        onSelectAllTools={onSelectAllTools}
                        searchQuery={searchQuery}
                        setSearchQuery={setSearchQuery}
                    />
                </div>
                <div className={styles.buttonsContainer}>
                    <Button
                        appearance="primary"
                        onClick={() => {
                            onSubmit(selectedToolNames);
                        }}
                        disabled={!selectedToolNames.length}
                    >
                        {intl.formatMessage(ExtendedAgentsGraphResources.addTools)}
                    </Button>
                    <Button appearance="secondary" onClick={onDismiss}>
                        {intl.formatMessage(SreAgentResources.cancel)}
                    </Button>
                </div>
            </DialogBody>
        </DialogSurface>
    );
};
