import { Button, SearchBox, TableCell, TableHeaderCell, Text, Toolbar, ToolbarButton, ToolbarDivider } from '@fluentui/react-components';
import { ArrowClockwise20Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC, memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { ExtendedAgentsGraphResources, ScheduledTasksResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentNodeType, Skill } from '../../../Contracts/ExtendedAgentGraph';
import { EntityIcon } from '../../EntityIcon';
import { EntityDeleteConfirmDialog } from '../Common/EntityDeleteConfirmDialog';
import { EntityTable } from '../Common/EntityTable';
import { BaseTableItem, EntityTableProps, SkillItem } from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';

interface SkillTableProps extends EntityTableProps {
    skills: Skill[];
}

export const SkillTable: FC<SkillTableProps> = ({ skills, openInfoPanel, refresh, lastUpdated, isLoading }) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const [searchText, setSearchText] = useState<string>();
    const [selectedSkills, setSelectedSkills] = useState<SkillItem[]>([]);

    const EMPTY_DISPLAY = useMemo(() => intl.formatMessage(SreAgentResources.none), [intl]);

    const handleSearchTextChange = useCallback((text: string) => {
        setSearchText(text);
    }, []);

    const handleSelectedItemsChange = useCallback((items: BaseTableItem[]) => {
        setSelectedSkills(items as SkillItem[]);
    }, []);

    const skillItems = useMemo<SkillItem[]>(() => {
        const query = searchText?.trim().toLowerCase();
        let filtered = skills;

        if (query) {
            filtered = skills.filter(
                skill => skill.name?.toLowerCase().includes(query) || skill.description?.toLowerCase().includes(query)
            );
        }

        return filtered.map(skill => ({
            name: skill.name || EMPTY_DISPLAY,
            description: skill.description || EMPTY_DISPLAY,
            toolsCount: skill.tools?.length || 0,
            hasContent: !!skill.skillMdContent,
            filesCount: skill.additionalFiles?.length || 0,
            data: skill,
        }));
    }, [EMPTY_DISPLAY, skills, searchText]);

    const renderTableHeaders = useCallback(() => {
        return (
            <>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.skillName)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.skillDescription)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.skillTools)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.additionalFiles)}
                </TableHeaderCell>
            </>
        );
    }, [intl, styles.tableHeader]);

    const renderTableCells = useCallback(
        (item: BaseTableItem) => {
            const skillItem = item as SkillItem;
            return (
                <>
                    <TableCell tabIndex={0} role="gridcell">
                        <Button
                            appearance="transparent"
                            onClick={() => openInfoPanel?.(skillItem.name, ExtendedAgentNodeType.Skill)}
                            className={styles.transparentButton}
                        >
                            <Text className={styles.clickableText}>{skillItem.name}</Text>
                        </Button>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text title={skillItem.description}>
                            {skillItem.description.length > 80 ? `${skillItem.description.slice(0, 80)}…` : skillItem.description}
                        </Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        {skillItem.toolsCount > 0 ? (
                            <div className={styles.flexRowSmall}>
                                <EntityIcon type="tool" shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }} />
                                <Text>{skillItem.toolsCount}</Text>
                            </div>
                        ) : (
                            <Text>{EMPTY_DISPLAY}</Text>
                        )}
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        {skillItem.filesCount > 0 ? (
                            <div className={styles.flexRowSmall}>
                                <EntityIcon type="file" shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }} />
                                <Text>{skillItem.filesCount}</Text>
                            </div>
                        ) : (
                            <Text>{EMPTY_DISPLAY}</Text>
                        )}
                    </TableCell>
                </>
            );
        },
        [styles, openInfoPanel, EMPTY_DISPLAY]
    );

    return (
        <div className={styles.entityTable}>
            <SkillTableToolbar
                searchText={searchText}
                setSearchText={handleSearchTextChange}
                refresh={refresh}
                lastUpdated={lastUpdated}
                isLoading={isLoading}
                selectedSkills={selectedSkills}
            />
            <EntityTable
                activeTab="skills"
                searchText={searchText}
                items={skillItems}
                setSelectedItems={handleSelectedItemsChange}
                renderTableHeaders={renderTableHeaders}
                renderTableCells={renderTableCells}
                isLoading={isLoading}
            />
        </div>
    );
};

interface SkillTableToolbarProps {
    searchText?: string;
    setSearchText: (text: string) => void;
    refresh: () => void;
    lastUpdated?: string;
    isLoading?: boolean;
    selectedSkills: SkillItem[];
}

const SkillTableToolbar: FC<SkillTableToolbarProps> = memo(({ searchText, setSearchText, refresh, lastUpdated, selectedSkills }) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);

    const agentClient = useMemo(() => new ExtendedAgentClient(sreAgentEndpoint), [sreAgentEndpoint]);

    const isDeleteDisabled = useMemo(() => selectedSkills.length === 0 || isDeleting, [isDeleting, selectedSkills.length]);

    const handleDelete = useCallback(async () => {
        setIsDeleting(true);
        setShowDeleteConfirmationDialog(false);

        const skillCount = selectedSkills.length;
        const skillNames = selectedSkills.map(s => s.name);

        const notificationId = azPortalContext?.startNotification(
            intl.formatMessage(ExtendedAgentsGraphResources.deleteSkillTitle),
            intl.formatMessage(ExtendedAgentsGraphResources.deletingSkills, { count: skillCount })
        );

        const results = await Promise.all(
            selectedSkills.map(async skillItem => {
                const response = await agentClient.deleteSkill(skillItem.name);
                return { name: skillItem.name, success: response.isSuccessful };
            })
        );

        const successCount = results.filter(r => r.success).length;
        const failureCount = results.filter(r => !r.success).length;

        if (failureCount === 0) {
            azPortalContext?.stopNotification(
                notificationId!,
                true,
                intl.formatMessage(ExtendedAgentsGraphResources.deleteSkillSuccess, {
                    count: successCount,
                    name: skillCount === 1 ? skillNames[0] : undefined,
                })
            );
        } else {
            azPortalContext?.stopNotification(
                notificationId!,
                false,
                intl.formatMessage(ExtendedAgentsGraphResources.deleteSkillPartialError, {
                    successCount,
                    failureCount,
                })
            );
        }

        refresh();
        setIsDeleting(false);
    }, [agentClient, azPortalContext, intl, refresh, selectedSkills]);

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
                    <SearchBox
                        className={styles.searchBox}
                        value={searchText || ''}
                        onChange={(_, data) => setSearchText(data.value)}
                        placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchSkillsPlaceholder)}
                    />
                </Toolbar>
                <EntityDeleteConfirmDialog
                    showDialog={showDeleteConfirmationDialog}
                    setShowDialog={setShowDeleteConfirmationDialog}
                    handleDelete={handleDelete}
                    numItems={selectedSkills.length}
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
});

SkillTableToolbar.displayName = 'SkillTableToolbar';
