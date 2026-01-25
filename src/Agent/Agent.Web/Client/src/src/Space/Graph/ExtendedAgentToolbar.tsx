import { Button, mergeClasses, RadioGroup, tokens, Tooltip } from '@fluentui/react-components';
import { ArrowClockwise20Regular, ArrowDown20Regular, DividerTall20Regular } from '@fluentui/react-icons';
import { FC, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { FirstPartyHelper } from '../../Common/Helpers/FirstPartyHelper';
import { ExtendedAgentsGraphResources, PlaygroundResources } from '../../Strings/SREAgentResources';
import { CopilotRadio as Radio } from '../Components/Common/CopilotRadio';
import { DirtyStateContext } from '../Contracts/Context';
import { ExtendedAgentGraphView } from '../Contracts/ExtendedAgentGraph';
import {
    DirtyStateOnChangeConfirmationWrapper,
    DirtyStateOnClickConfirmationWrapper,
} from '../IncidentManagement/CreateIncidentHandler/DirtyStateConfirmationDialog';
import { useCommonStyles } from '../Styles/Common.styles';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
import CreateButton from './CreateButton';
import { EntityTypeExt } from './ExtendedAgentCreationDialog/types';
import { InstallMcpDialog } from './InstallMcpDialog';

interface ExtendedAgentToolbarProps {
    currentView: ExtendedAgentGraphView;
    onViewChange: (view: ExtendedAgentGraphView) => void;
    onRefresh: () => void;
    onCreateItem: (itemType: EntityTypeExt) => void;
    isLoading: boolean;
    disableCreateMetaAgent: boolean;
    disableCreateSubagent: boolean;
    disableCreateSkill: boolean;
}

export const ExtendedAgentToolbar: FC<ExtendedAgentToolbarProps> = ({
    currentView,
    onViewChange,
    onRefresh,
    onCreateItem,
    isLoading,
    disableCreateMetaAgent,
    disableCreateSubagent,
    disableCreateSkill,
}) => {
    const { toolbarWrapper, toolbarRefreshButton, toolbarInstallMcpButton } = useExtendedAgentGraphStyles();
    const { isDirty } = useContext(DirtyStateContext);
    const { contentHeader } = useCommonStyles();

    const intl = useIntl();
    const [showMcpDialog, setShowMcpDialog] = useState(false);

    const { userInfo } = useContext(EnvironmentContext);
    const tenantId = userInfo?.directoryId || '';
    const isFirstParty = FirstPartyHelper.isFirstPartyAgent(tenantId);

    return (
        <div className={mergeClasses(contentHeader, toolbarWrapper)}>
            <CreateButton
                handleCreateItemStandalone={onCreateItem}
                disableCreateMetaAgent={disableCreateMetaAgent}
                disableCreateSubagent={disableCreateSubagent}
                disableCreateSkill={disableCreateSkill}
                disabled={isLoading}
            />
            <DirtyStateOnChangeConfirmationWrapper isDirty={isDirty}>
                <RadioGroup
                    name="viewToggle"
                    value={currentView}
                    layout="horizontal"
                    onChange={(_, data) => onViewChange(data.value as ExtendedAgentGraphView)}
                >
                    <Radio value={ExtendedAgentGraphView.Canvas} label={intl.formatMessage(ExtendedAgentsGraphResources.canvasView)} />
                    <Radio value={ExtendedAgentGraphView.Table} label={intl.formatMessage(ExtendedAgentsGraphResources.tableView)} />
                    <Radio value={ExtendedAgentGraphView.Playground} label={intl.formatMessage(PlaygroundResources.testPlayground)} />
                </RadioGroup>
            </DirtyStateOnChangeConfirmationWrapper>
            <div className={toolbarRefreshButton}>
                <DividerTall20Regular color={tokens.colorNeutralStroke2} />
                <DirtyStateOnClickConfirmationWrapper isDirty={isDirty}>
                    <Button appearance="transparent" icon={<ArrowClockwise20Regular />} onClick={onRefresh} disabled={isLoading}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.refreshGraphButton)}
                    </Button>
                </DirtyStateOnClickConfirmationWrapper>
            </div>
            {isFirstParty && (
                <div className={toolbarInstallMcpButton}>
                    <Tooltip content={intl.formatMessage(ExtendedAgentsGraphResources.installMcpTooltip)} relationship="label">
                        <Button appearance="primary" icon={<ArrowDown20Regular />} onClick={() => setShowMcpDialog(true)}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.installMcp)}
                        </Button>
                    </Tooltip>
                </div>
            )}
            {isFirstParty && <InstallMcpDialog isOpen={showMcpDialog} onOpenChange={setShowMcpDialog} />}
        </div>
    );
};
