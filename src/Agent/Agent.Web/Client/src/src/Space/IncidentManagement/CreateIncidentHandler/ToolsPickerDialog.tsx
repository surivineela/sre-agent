import { IColumn } from '@fluentui/react';
import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
} from '@fluentui/react-components';
import { useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ToolInfo } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentHandlerCreateResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { MultipleSelectionShimmerDetailsList } from '../../Components/MultipleSelectionShimmerDetailsList';
import { ToolTableFieldNames } from './Contracts';

export interface ToolsPickerProps {
    visible: boolean;
    onDismiss: () => void;
    onSave: (toolNames: string[]) => void;
    existingToolsSelection: string[];
    tools: ToolInfo[];
    loading: boolean;
}

export const ToolsPickerDialog = ({ visible, onDismiss, onSave, existingToolsSelection, tools, loading }: ToolsPickerProps) => {
    const intl = useIntl();

    const [selectedToolNames, setSelectedToolNames] = useState<string[]>(existingToolsSelection);

    const toolsTableColumns: IColumn[] = useMemo(() => {
        return [
            {
                key: ToolTableFieldNames.Name,
                fieldName: ToolTableFieldNames.Name,
                name: intl.formatMessage(IncidentHandlerCreateResources.tool),
                minWidth: 100,
                maxWidth: 200,
                isResizable: true,
            },
            {
                key: ToolTableFieldNames.Description,
                fieldName: ToolTableFieldNames.Description,
                name: intl.formatMessage(IncidentHandlerCreateResources.description),
                minWidth: 200,
                isMultiline: true,
                isResizable: true,
            },
        ];
    }, [intl]);

    return (
        <Dialog modalType="modal" open={visible}>
            <DialogSurface
                style={{
                    width: '60vw',
                    maxWidth: '800px',
                }}
            >
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(IncidentHandlerCreateResources.manageTools)}</DialogTitle>
                    <DialogContent>
                        <div
                            style={{
                                display: 'flex',
                                flexDirection: 'column',
                                padding: 0,
                                height: '100%',
                                overflowY: 'auto',
                            }}
                        >
                            <MultipleSelectionShimmerDetailsList
                                data={tools}
                                selectedKeys={selectedToolNames}
                                loading={loading}
                                columns={toolsTableColumns}
                                onChange={setSelectedToolNames}
                                getKey={(item: ToolInfo) => item.name}
                                filter={(searchTerm, item) => {
                                    return (
                                        item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                                        !!item.description?.toLowerCase().includes(searchTerm.toLowerCase())
                                    );
                                }}
                            />
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <DialogTrigger disableButtonEnhancement>
                            <Button
                                appearance="primary"
                                onClick={() => {
                                    onSave(selectedToolNames);
                                }}
                            >
                                {intl.formatMessage(SreAgentResources.save)}
                            </Button>
                        </DialogTrigger>
                        <DialogTrigger disableButtonEnhancement>
                            <Button
                                appearance="secondary"
                                onClick={() => {
                                    onDismiss();
                                }}
                            >
                                {intl.formatMessage(SreAgentResources.cancel)}
                            </Button>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
