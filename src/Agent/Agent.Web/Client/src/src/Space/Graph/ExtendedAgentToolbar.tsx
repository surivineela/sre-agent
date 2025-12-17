import { Button, Radio, RadioGroup, tokens } from '@fluentui/react-components';
import { ArrowClockwise20Regular, DividerTall20Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgentGraphView } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
import CreateButton from './CreateButton';
import { EntityTypeExt } from './ExtendedAgentCreationDialog/types';

interface ExtendedAgentToolbarProps {
    currentView: ExtendedAgentGraphView;
    onViewChange: (view: ExtendedAgentGraphView) => void;
    onRefresh: () => void;
    onCreateItem: (itemType: EntityTypeExt) => void;
    isLoading: boolean;
    hasData: boolean;
    showEmptyState: boolean;
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
    hasData,
    showEmptyState,
    disableCreateMetaAgent,
    disableCreateSubagent,
    disableCreateSkill,
}) => {
    const { toolbarWrapper, toolbarRefreshButton } = useExtendedAgentGraphStyles();
    const intl = useIntl();

    return (
        <div className={toolbarWrapper}>
            <CreateButton
                handleCreateItemStandalone={onCreateItem}
                disableCreateMetaAgent={disableCreateMetaAgent}
                disableCreateSubagent={disableCreateSubagent}
                disableCreateSkill={disableCreateSkill}
                disabled={isLoading || !hasData}
            />
            <RadioGroup
                name="viewToggle"
                value={currentView}
                layout="horizontal"
                onChange={(_, data) => onViewChange(data.value as ExtendedAgentGraphView)}
            >
                <Radio value={ExtendedAgentGraphView.Visual} label={intl.formatMessage(ExtendedAgentsGraphResources.canvasView)} />
                <Radio
                    value={ExtendedAgentGraphView.Grid}
                    label={intl.formatMessage(ExtendedAgentsGraphResources.tableView)}
                    disabled={showEmptyState}
                />
            </RadioGroup>
            <div className={toolbarRefreshButton}>
                <DividerTall20Regular color={tokens.colorNeutralStroke2} />
                <Button appearance="transparent" icon={<ArrowClockwise20Regular />} onClick={onRefresh} disabled={isLoading}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.refreshGraphButton)}
                </Button>
            </div>
        </div>
    );
};
