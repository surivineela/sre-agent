import {
    Button,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Link,
    mergeClasses,
    Table,
    TableBody,
    TableCell,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    Toolbar,
    ToolbarButton,
} from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular, Delete20Regular, DocumentText16Regular } from '@fluentui/react-icons';
import { FC, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SearchBoxWithDebounce } from '../../../Common/Components/SearchBox/SearchBoxWithDebounce';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { KnowledgeBaseResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { UploadedFile, useKnowledgeBase } from '../Hooks/useKnowledgeBase';
import { useKnowledgeBaseStyles } from '../Styles/KnowledgeBase.styles';
import { DeleteConfirmationDialog } from './DeleteConfirmationDialog';
import { EmptyState } from './EmptyState';

const ACCEPTED_FILE_TYPES = '.md,.txt';

const KnowledgeBase: FC = () => {
    const intl = useIntl();
    const portalContext = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);
    const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);
    const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
    const [fileToDelete, setFileToDelete] = useState<string | null>(null);
    const styles = useKnowledgeBaseStyles();

    const {
        selectedFiles,
        uploadedFiles,
        onSortChange,
        onSelectionChange,
        columns,
        sortState,
        originalUploadedFiles,
        selectedUploadedFileKeys,
        isLoadingFiles,
        isUploading,
        isDeletingFiles,
        isDragOver,
        searchText,
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

    const handleUploadAndClose = async () => {
        await handleUploadFiles();
        setIsUploadModalOpen(false);
    };

    const handleDeleteConfirmation = async () => {
        await handleBulkDeleteFiles();
        setIsDeleteConfirmOpen(false);
        setFileToDelete(null);
    };

    const handleCancelDelete = () => {
        setIsDeleteConfirmOpen(false);
        setFileToDelete(null);
    };

    const handleBulkDeleteStart = () => {
        setFileToDelete(null);
        setIsDeleteConfirmOpen(true);
    };

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

    return (
        <div className={styles.container}>
            <div className={styles.header}>{intl.formatMessage(KnowledgeBaseResources.fileUploadTitle)}</div>
            <Text className={styles.description}>{intl.formatMessage(KnowledgeBaseResources.fileUploadDescription)}</Text>
            <div className={styles.buttonsContainer}>
                <Toolbar>
                    <ToolbarButton
                        icon={<Add16Regular />}
                        className={styles.toolbarButton}
                        appearance="subtle"
                        disabled={isLoadingFiles || isUploading}
                        onClick={() => setIsUploadModalOpen(true)}
                    >
                        {intl.formatMessage(KnowledgeBaseResources.addFile)}
                    </ToolbarButton>
                    <ToolbarButton
                        icon={<Delete16Regular />}
                        appearance="subtle"
                        onClick={handleBulkDeleteStart}
                        disabled={selectedUploadedFileKeys.length === 0 || isDeletingFiles}
                    >
                        {isDeletingFiles
                            ? intl.formatMessage(KnowledgeBaseResources.deleting)
                            : intl.formatMessage(SreAgentResources.delete)}
                    </ToolbarButton>
                    <SearchBoxWithDebounce
                        className={styles.searchBox}
                        placeholder={intl.formatMessage(KnowledgeBaseResources.searchForFiles)}
                        setSearchTerm={setSearchText}
                        textToAnnounce={searchResultToAnnounce}
                        size={'small'}
                    />
                    <ToolbarButton
                        icon={<ArrowClockwise16Regular />}
                        className={styles.toolbarRefresh}
                        appearance="subtle"
                        disabled={isLoadingFiles || isUploading}
                        onClick={handleRefresh}
                    >
                        {intl.formatMessage(KnowledgeBaseResources.refresh)}
                    </ToolbarButton>
                </Toolbar>
            </div>

            {selectedUploadedFileKeys.length > 0 && (
                <Text className={styles.filesSelectedText}>
                    {intl.formatMessage(KnowledgeBaseResources.filesSelected, {
                        count: selectedUploadedFileKeys.length,
                    })}
                </Text>
            )}

            <div className={styles.detailsListContainer}>
                <DataGrid
                    columns={columns}
                    items={uploadedFiles}
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
                {!isLoadingFiles && uploadedFiles.length === 0 && (
                    <EmptyState
                        variant={originalUploadedFiles.length === 0 ? 'noItems' : 'noSearchResults'}
                        onPrimaryAction={originalUploadedFiles.length === 0 ? () => setIsUploadModalOpen(true) : () => {}}
                        isActionDisabled={isLoadingFiles || isUploading}
                    />
                )}
            </div>

            <Dialog open={isUploadModalOpen} onOpenChange={(_, data) => setIsUploadModalOpen(data.open)}>
                <DialogSurface className={styles.dialogSurface}>
                    <DialogBody className={styles.dialogBody}>
                        <DialogTitle>{intl.formatMessage(KnowledgeBaseResources.uploadFiles)}</DialogTitle>
                        <DialogContent className={styles.dialogContent}>
                            <Text>{intl.formatMessage(KnowledgeBaseResources.filesStoredIn)}</Text>

                            <div
                                className={mergeClasses(styles.dropZone, isDragOver ? styles.dropZoneDragOver : styles.dropZoneIdle)}
                                onDragOver={handleDragOver}
                                onDragLeave={handleDragLeave}
                                onDrop={handleDrop}
                            >
                                <div className={styles.emptyDropZone}>
                                    <img
                                        src={resolveResourceIcon('folder')}
                                        className={styles.folderIcon}
                                        alt={intl.formatMessage(KnowledgeBaseResources.folder)}
                                    />
                                    <Text>
                                        {intl.formatMessage(KnowledgeBaseResources.dragFilesHere)}{' '}
                                        <Link onClick={handleButtonClick}>{intl.formatMessage(KnowledgeBaseResources.browseForFiles)}</Link>
                                    </Text>
                                </div>
                            </div>

                            <div className={styles.uploadInfoContainer}>
                                <Text className={styles.uploadInfoText}>
                                    {intl.formatMessage(KnowledgeBaseResources.supportedFileFormats)}
                                </Text>
                                <Text className={styles.uploadInfoText}>{intl.formatMessage(KnowledgeBaseResources.maximumFileSize)}</Text>
                            </div>

                            {selectedFiles.length > 0 && (
                                <div className={styles.selectedFilesContainer}>
                                    <Text className={styles.selectedFilesTitle}>Files ({selectedFiles.length})</Text>
                                    <div className={styles.fileTableScrollContainer}>
                                        <Table arial-label={intl.formatMessage(KnowledgeBaseResources.selectedFilesTable)}>
                                            <TableHeader>
                                                <TableRow>
                                                    <TableHeaderCell className={styles.fileTableHeaderCell35}>
                                                        {intl.formatMessage(KnowledgeBaseResources.fileName)}
                                                    </TableHeaderCell>
                                                </TableRow>
                                            </TableHeader>
                                            <TableBody>
                                                {selectedFiles.map((file: File, index: number) => (
                                                    <TableRow key={index}>
                                                        <TableCell>
                                                            <div className={styles.fileIconCell}>
                                                                <DocumentText16Regular />
                                                                <Text>{file.name}</Text>
                                                            </div>
                                                        </TableCell>
                                                        <TableCell className={styles.actionCell}>
                                                            <Button
                                                                appearance="subtle"
                                                                icon={<Delete20Regular />}
                                                                onClick={() => handleRemoveFile(index)}
                                                                size="small"
                                                                aria-label={intl.formatMessage(KnowledgeBaseResources.removeFile)}
                                                            />
                                                        </TableCell>
                                                    </TableRow>
                                                ))}
                                            </TableBody>
                                        </Table>
                                    </div>
                                </div>
                            )}
                            <input
                                ref={fileInputRef}
                                type="file"
                                onChange={handleFileInputChange}
                                className={styles.hiddenFileInput}
                                accept={ACCEPTED_FILE_TYPES}
                                multiple
                            />
                        </DialogContent>
                    </DialogBody>
                    <DialogActions className={styles.dialogFooter}>
                        <Button appearance="secondary" onClick={() => setIsUploadModalOpen(false)}>
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                        <Button appearance="primary" onClick={handleUploadAndClose} disabled={selectedFiles.length === 0 || isUploading}>
                            {isUploading
                                ? intl.formatMessage(KnowledgeBaseResources.uploading)
                                : intl.formatMessage(KnowledgeBaseResources.uploadFiles)}
                        </Button>
                    </DialogActions>
                </DialogSurface>
            </Dialog>

            <DeleteConfirmationDialog
                isOpen={isDeleteConfirmOpen}
                onOpenChange={setIsDeleteConfirmOpen}
                onConfirmDelete={handleDeleteConfirmation}
                onCancelDelete={handleCancelDelete}
                isOperationInProgress={isDeletingFiles}
                itemType="file"
                actionVerb={intl.formatMessage(SreAgentResources.delete)}
                selectedItems={fileToDelete ? [fileToDelete] : selectedUploadedFileKeys}
                title={
                    fileToDelete
                        ? intl.formatMessage(KnowledgeBaseResources.deleteFile)
                        : selectedUploadedFileKeys.length > 1
                          ? intl.formatMessage(KnowledgeBaseResources.deleteFiles, { count: selectedUploadedFileKeys.length })
                          : intl.formatMessage(KnowledgeBaseResources.deleteFile)
                }
                message={
                    fileToDelete
                        ? intl.formatMessage(KnowledgeBaseResources.deleteFileMessage)
                        : selectedUploadedFileKeys.length > 1
                          ? intl.formatMessage(KnowledgeBaseResources.deleteFilesMessage, { count: selectedUploadedFileKeys.length })
                          : intl.formatMessage(KnowledgeBaseResources.deleteFileMessage)
                }
            />
        </div>
    );
};

export default KnowledgeBase;
