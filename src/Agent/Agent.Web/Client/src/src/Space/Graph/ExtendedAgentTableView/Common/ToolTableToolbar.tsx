import { InputOnChangeData, SearchBox, Text, Toolbar, ToolbarButton, ToolbarDivider } from '@fluentui/react-components';
import { ArrowClockwise20Regular, Delete16Regular } from '@fluentui/react-icons';
import { SearchBoxChangeEvent } from '@fluentui/react-search';
import { FC, ReactNode } from 'react';
import { MessageDescriptor, useIntl } from 'react-intl';
import { ScheduledTasksResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';
import { ToolType, useToolTableDelete } from '../Hooks/useToolTableDelete';
import { EntityDeleteConfirmDialog } from './EntityDeleteConfirmDialog';

interface ToolTableToolbarProps {
    toolType: ToolType;
    selectedTools: ExtendedTool[];
    searchText?: string;
    setSearchText: (searchText: string) => void;
    searchPlaceholder: MessageDescriptor;
    refresh: () => void;
    lastUpdated?: string;
    additionalFilters?: ReactNode;
}

export const ToolTableToolbar: FC<ToolTableToolbarProps> = ({
    toolType,
    selectedTools,
    searchText,
    setSearchText,
    searchPlaceholder,
    refresh,
    lastUpdated,
    additionalFilters,
}) => {
    const intl = useIntl();
    const styles = useListViewStyles();

    const { isDeleteDisabled, showDeleteConfirmationDialog, setShowDeleteConfirmationDialog, handleDelete } = useToolTableDelete({
        toolType,
        selectedTools,
        refresh,
    });

    return (
        <div className={styles.toolbar}>
            <div className={styles.searchAndToolbar}>
                <Toolbar className={styles.toolbarButtons}>
                    <ToolbarButton
                        appearance="subtle"
                        className={styles.toolbarButton}
                        icon={<Delete16Regular />}
                        onClick={() => setShowDeleteConfirmationDialog(true)}
                        disabled={isDeleteDisabled}
                    >
                        {intl.formatMessage(SreAgentResources.delete)}
                    </ToolbarButton>
                    <ToolbarDivider />
                </Toolbar>
                <div className={styles.searchBoxAndFilters}>
                    <SearchBox
                        className={styles.searchBox}
                        placeholder={intl.formatMessage(searchPlaceholder)}
                        value={searchText}
                        onChange={(_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? '')}
                        size={'small'}
                    />
                    {additionalFilters}
                </div>
                <EntityDeleteConfirmDialog
                    showDialog={showDeleteConfirmationDialog}
                    setShowDialog={setShowDeleteConfirmationDialog}
                    handleDelete={handleDelete}
                    numItems={selectedTools.length}
                />
            </div>
            {lastUpdated && (
                <div className={styles.lastUpdated}>
                    <ArrowClockwise20Regular />
                    <Text>{`${intl.formatMessage(ScheduledTasksResources.lastUpdated)}: ${lastUpdated}`}</Text>
                </div>
            )}
        </div>
    );
};
