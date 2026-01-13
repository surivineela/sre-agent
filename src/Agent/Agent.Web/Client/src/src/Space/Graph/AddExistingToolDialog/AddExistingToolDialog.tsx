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
    addToolsToAgent: (agentName: string, nonMcpToolNames: string[], mcpToolNames: string[]) => void;
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
        ],
        [toolPickerInfo?.agent?.tools, toolPickerInfo?.agent?.systemTools]
    );
    const excludedMcpToolNames = useMemo(
        () => [
            ...(toolPickerInfo?.agent?.mcpTools || []),
        ],
        [toolPickerInfo?.agent?.mcpTools]
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
                onSubmit={(selectedNonMcpToolNames: string[], selectedMcpToolNames: string[]) => {
                    if (agentName) {
                        addToolsToAgent(agentName, selectedNonMcpToolNames, selectedMcpToolNames);
                    }
                    onDismiss();
                }}
                existingTools={existingTools}
                systemTools={systemTools}
                mcpConnections={mcpConnections}
                excludedToolNames={excludedToolNames}
                excludedMcpToolNames={excludedMcpToolNames}
            />
        </Dialog>
    );
};

interface AddExistingToolDialogInnerProps {
    onDismiss: () => void;
    onSubmit: (selectedNonMcpToolNames: string[], selectedMcpToolNames: string[]) => void;
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    mcpConnections?: McpConnection[];
    excludedToolNames?: string[];
    excludedMcpToolNames?: string[];
}

const AddExistingToolDialogInner: FC<AddExistingToolDialogInnerProps> = ({
    onDismiss,
    onSubmit,
    existingTools,
    systemTools,
    mcpConnections,
    excludedToolNames,
    excludedMcpToolNames,
}) => {
    const intl = useIntl();
    const styles = useAddExistingToolDialogStyles();
    const [selectedToolNames, setSelectedToolNames] = useState<string[]>([]);
    const [selectedMcpToolNames, setSelectedMcpToolNames] = useState<string[]>([]);

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
        selectedToolKeys,
    } = useToolsPicker({
        selectedToolNames,
        setSelectedToolNames,
        selectedMcpToolNames,
        setSelectedMcpToolNames,
        existingTools,
        systemTools,
        mcpConnections,
        excludedToolNames,
        excludedMcpToolNames
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
                    onRemoveItem={key => onSelectedToolChange(key, false)}
                    onClearAll={onClearSelectedTools}
                />
                <div className={styles.dialogContentWrapper}>
                    <ToolsPicker
                        toolType={toolType}
                        onToolTypeChange={onToolTypeChange}
                        groups={groups}
                        expandedGroupNames={expandedGroupNames}
                        onGroupExpandedChange={onGroupExpandedChange}
                        selectedToolKeys={selectedToolKeys}
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
                            onSubmit(selectedToolNames, selectedMcpToolNames);
                        }}
                        disabled={!selectedToolNames.length && !selectedMcpToolNames.length}
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
