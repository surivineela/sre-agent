import {
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Text,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import { ArrowClockwise16Regular, Delete16Regular, Document16Regular, Globe16Regular, WebAssetRegular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { SearchBoxWithDebounce } from '../../../Common/Components/SearchBox/SearchBoxWithDebounce';
import { TextWithLink } from '../../../Common/Components/TextWithLink';
import { SreAgentFwLinks } from '../../../Common/Constants/FwLinks';
import { KnowledgeBaseResources, KnowledgeSettingsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { UploadedFile, useKnowledgeBase } from '../Hooks/useKnowledgeBase';
import { useKnowledgeSettingsStyles } from '../Styles/KnowledgeSettings.styles';
import { ActionCard } from './ActionCard';
import { AddRepositoryDialog } from './AddRepositoryDialog/AddRepositoryDialog';
import { AddWebPageDialog } from './AddWebPageDialog';
import { DeleteConfirmationDialog } from './DeleteConfirmationDialog';
import { FileUploadDialog } from './FileUploadDialog';
import { KnowledgeSettingsEmptyState } from './KnowledgeSettingsEmptyState';

type KnowledgeSourceType = 'all' | 'file' | 'webpage' | 'repository';

const KnowledgeSettings: FC = () => {
    const intl = useIntl();
    const styles = useKnowledgeSettingsStyles();
    const portalContext = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);
    const { agentObj } = useContext(SreAgentContext);

    const [isFileUploadDialogOpen, setIsFileUploadDialogOpen] = useState(false);
    const [isAddWebPageDialogOpen, setIsAddWebPageDialogOpen] = useState(false);
    const [isAddRepositoryDialogOpen, setIsAddRepositoryDialogOpen] = useState(false);
    const [typeFilter, setTypeFilter] = useState<KnowledgeSourceType>('all');
    const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);

    const {
        selectedFiles,
        uploadedFiles,
        originalUploadedFiles,
        selectedUploadedFileKeys,
        isLoadingFiles,
        isUploading,
        isDeletingFiles,
        isDragOver,
        searchText,
        sortState,
        columns,
        onSortChange,
        onSelectionChange,
        handleFileInputChange,
        handleButtonClick,
        handleDragOver,
        handleDragLeave,
        handleDrop,
        handleRemoveFile,
        handleUploadFiles,
        handleBulkDeleteFiles,
        handleRefresh,
        setSearchText,
        fileInputRef,
    } = useKnowledgeBase(portalContext, resourceId);

    const handleAddFile = useCallback(() => {
        setIsFileUploadDialogOpen(true);
    }, []);

    const handleAddWebPage = useCallback(() => {
        setIsAddWebPageDialogOpen(true);
    }, []);

    const handleAddWebPageSubmit = useCallback(
        (url: string, name: string, description?: string) => {
            portalContext.log({
                action: 'addWebPage',
                actionModifier: 'submitted',
                resourceId,
                logLevel: 'info',
                data: { url, name, description },
            });
            setIsAddWebPageDialogOpen(false);
            // TODO: Implement API call to add web page
        },
        [portalContext, resourceId]
    );

    const handleAddWebPageCancel = useCallback(() => {
        setIsAddWebPageDialogOpen(false);
    }, []);

    const handleAddWebPageDialogOpenChange = useCallback((open: boolean) => {
        setIsAddWebPageDialogOpen(open);
    }, []);

    const handleAddRepository = useCallback(() => {
        setIsAddRepositoryDialogOpen(true);
    }, []);

    const handleAddRepositoryDialogOpenChange = useCallback((open: boolean) => {
        setIsAddRepositoryDialogOpen(open);
    }, []);

    const handleAddRepositorySuccess = useCallback(() => {
        handleRefresh();
    }, [handleRefresh]);

    const handleUploadAndClose = useCallback(async () => {
        await handleUploadFiles();
        setIsFileUploadDialogOpen(false);
    }, [handleUploadFiles]);

    const handleFileUploadDialogCancel = useCallback(() => {
        setIsFileUploadDialogOpen(false);
    }, []);

    const handleCreateFile = useCallback(() => {
        setIsFileUploadDialogOpen(false);
        // TODO: Implement create file functionality
    }, []);

    const handleFileUploadDialogOpenChange = useCallback((open: boolean) => {
        setIsFileUploadDialogOpen(open);
    }, []);

    const handleTypeFilterChange = useCallback((keys: string[]) => {
        setTypeFilter((keys[0] as KnowledgeSourceType) || 'all');
    }, []);

    const handleDeleteConfirmation = useCallback(async () => {
        await handleBulkDeleteFiles();
        setIsDeleteConfirmOpen(false);
    }, [handleBulkDeleteFiles]);

    const handleCancelDelete = useCallback(() => {
        setIsDeleteConfirmOpen(false);
    }, []);

    const handleBulkDeleteStart = useCallback(() => {
        setIsDeleteConfirmOpen(true);
    }, []);

    const typeFilterOptions = useMemo(
        () => [
            { key: 'all', label: intl.formatMessage(KnowledgeSettingsResources.typeAll) },
            { key: 'file', label: intl.formatMessage(KnowledgeSettingsResources.typeFile) },
            { key: 'webpage', label: intl.formatMessage(KnowledgeSettingsResources.typeWebPage) },
            { key: 'repository', label: intl.formatMessage(KnowledgeSettingsResources.typeRepository) },
        ],
        [intl]
    );

    const searchResultToAnnounce = useMemo(() => {
        if (searchText) {
            return intl.formatMessage(KnowledgeBaseResources.searchResultsFound, {
                count: uploadedFiles.length,
                total: originalUploadedFiles.length,
            });
        }
        return undefined;
    }, [searchText, uploadedFiles.length, originalUploadedFiles.length, intl]);

    const columnSizingOptions = useMemo(
        () => ({
            name: {
                minWidth: 250,
                idealWidth: 250,
                defaultWidth: 250,
            },
        }),
        []
    );

    const showEmptyState = !isLoadingFiles && uploadedFiles.length === 0 && originalUploadedFiles.length === 0;

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <h2 className={styles.title}>{intl.formatMessage(KnowledgeSettingsResources.knowledgeBaseTitle)}</h2>
                <TextWithLink
                    text={intl.formatMessage(KnowledgeSettingsResources.knowledgeBaseDescription)}
                    linkText={intl.formatMessage(KnowledgeSettingsResources.learnMoreAboutKnowledgeSources)}
                    linkUrl={SreAgentFwLinks.sreAgentSupportedServices}
                    textClassName={styles.description}
                />
            </div>

            <div className={styles.actionCardsContainer}>
                <ActionCard
                    icon={<Document16Regular />}
                    label={intl.formatMessage(KnowledgeSettingsResources.addFile)}
                    onClick={handleAddFile}
                />
                <ActionCard
                    icon={<Globe16Regular />}
                    label={intl.formatMessage(KnowledgeSettingsResources.addWebPage)}
                    onClick={handleAddWebPage}
                />
                <ActionCard
                    icon={<WebAssetRegular />}
                    label={intl.formatMessage(KnowledgeSettingsResources.addRepository)}
                    onClick={handleAddRepository}
                />
            </div>

            <Toolbar className={styles.toolbar}>
                <ToolbarButton
                    className={styles.deleteButton}
                    icon={<Delete16Regular />}
                    appearance="subtle"
                    onClick={handleBulkDeleteStart}
                    disabled={selectedUploadedFileKeys.length === 0 || isDeletingFiles || showEmptyState}
                >
                    {intl.formatMessage(SreAgentResources.delete)}
                </ToolbarButton>
                <ToolbarDivider />
                <SearchBoxWithDebounce
                    className={styles.searchBox}
                    placeholder={intl.formatMessage(KnowledgeSettingsResources.searchKnowledgeSources)}
                    setSearchTerm={setSearchText}
                    textToAnnounce={searchResultToAnnounce}
                    size="small"
                />
                <PillFilter
                    filterType="combobox"
                    label={intl.formatMessage(KnowledgeSettingsResources.typeColumn)}
                    options={typeFilterOptions}
                    selectedKeys={[typeFilter]}
                    onApply={handleTypeFilterChange}
                />
                <div className={styles.lastIndexedText}>
                    <ToolbarButton icon={<ArrowClockwise16Regular />} appearance="subtle" disabled={isLoadingFiles} onClick={handleRefresh}>
                        {intl.formatMessage(KnowledgeSettingsResources.lastIndexed, { time: '2:10 PM' })}
                    </ToolbarButton>
                </div>
            </Toolbar>

            {selectedUploadedFileKeys.length > 0 && (
                <Text className={styles.filesSelectedText}>
                    {intl.formatMessage(KnowledgeBaseResources.filesSelected, { count: selectedUploadedFileKeys.length })}
                </Text>
            )}

            <div className={styles.dataGridContainer}>
                <DataGrid
                    columns={columns}
                    items={showEmptyState ? [] : uploadedFiles}
                    sortable={!isLoadingFiles}
                    sortState={sortState}
                    onSortChange={onSortChange}
                    selectionMode="multiselect"
                    onSelectionChange={onSelectionChange}
                    getRowId={(item: UploadedFile) => item.name}
                    columnSizingOptions={columnSizingOptions}
                    resizableColumns
                >
                    <DataGridHeader>
                        <DataGridRow
                            selectionCell={{
                                checkboxIndicator: { 'aria-label': intl.formatMessage(SreAgentResources.selectAllRowsAriaLabel) },
                            }}
                        >
                            {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                        </DataGridRow>
                    </DataGridHeader>
                    <DataGridBody<UploadedFile>>
                        {({ item, rowId }) => (
                            <DataGridRow<UploadedFile>
                                key={rowId}
                                selectionCell={{
                                    checkboxIndicator: { 'aria-label': intl.formatMessage(SreAgentResources.selectRowAriaLabel) },
                                }}
                            >
                                {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                            </DataGridRow>
                        )}
                    </DataGridBody>
                </DataGrid>
            </div>

            {showEmptyState && (
                <KnowledgeSettingsEmptyState
                    onAddFile={handleAddFile}
                    onAddWebPage={handleAddWebPage}
                    onAddRepository={handleAddRepository}
                />
            )}

            <FileUploadDialog
                isOpen={isFileUploadDialogOpen}
                onOpenChange={handleFileUploadDialogOpenChange}
                selectedFiles={selectedFiles}
                isDragOver={isDragOver}
                isUploading={isUploading}
                fileInputRef={fileInputRef}
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
                onBrowseClick={handleButtonClick}
                onFileInputChange={handleFileInputChange}
                onRemoveFile={handleRemoveFile}
                onUpload={handleUploadAndClose}
                onCancel={handleFileUploadDialogCancel}
                onCreateFile={handleCreateFile}
            />

            <AddWebPageDialog
                isOpen={isAddWebPageDialogOpen}
                onOpenChange={handleAddWebPageDialogOpenChange}
                onAddWebPage={handleAddWebPageSubmit}
                onCancel={handleAddWebPageCancel}
            />

            <AddRepositoryDialog
                isOpen={isAddRepositoryDialogOpen}
                onOpenChange={handleAddRepositoryDialogOpenChange}
                onSuccess={handleAddRepositorySuccess}
                agentName={agentObj?.name}
                agentLocation={agentObj?.location}
            />

            <DeleteConfirmationDialog
                isOpen={isDeleteConfirmOpen}
                onOpenChange={setIsDeleteConfirmOpen}
                onConfirmDelete={handleDeleteConfirmation}
                onCancelDelete={handleCancelDelete}
                isOperationInProgress={isDeletingFiles}
                itemType="file"
                actionVerb={intl.formatMessage(SreAgentResources.delete)}
                selectedItems={selectedUploadedFileKeys}
                title={
                    selectedUploadedFileKeys.length > 1
                        ? intl.formatMessage(KnowledgeBaseResources.deleteFiles, { count: selectedUploadedFileKeys.length })
                        : intl.formatMessage(KnowledgeBaseResources.deleteFile)
                }
                message={
                    selectedUploadedFileKeys.length > 1
                        ? intl.formatMessage(KnowledgeBaseResources.deleteFilesMessage, { count: selectedUploadedFileKeys.length })
                        : intl.formatMessage(KnowledgeBaseResources.deleteFileMessage)
                }
            />
        </div>
    );
};

export default KnowledgeSettings;
