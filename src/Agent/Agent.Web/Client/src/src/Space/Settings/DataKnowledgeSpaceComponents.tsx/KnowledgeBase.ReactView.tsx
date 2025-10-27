import {
    Button,
    Checkbox,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    InputOnChangeData,
    SearchBox,
    SearchBoxChangeEvent,
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
import { debounce } from 'lodash';
import { FC, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { KnowledgeBaseResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useKnowledgeBase } from '../Hooks/useKnowledgeBaseNew';
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
        originalUploadedFiles,
        selectedUploadedFileKeys,
        isLoadingFiles,
        isUploading,
        isDeletingFiles,
        isDragOver,
        searchText,
        tableFeatures,
        headerSortProps,
        getColumnWidth,
        handleFileInputChange,
        handleButtonClick,
        handleDragOver,
        handleDragLeave,
        handleDrop,
        handleRemoveFile,
        handleUploadFiles,
        handleBulkDeleteFiles,
        onUpdateUploadedFileSelection,
        handleRefresh,
        setSearchText,
        fileInputRef,
    } = useKnowledgeBase(portalContext, resourceId);

    const handleUploadAndClose = async () => {
        await handleUploadFiles();
        setIsUploadModalOpen(false);
    };

    const isDeleteDisabled = selectedUploadedFileKeys.length === 0 || isDeletingFiles;

    const [selectedRowsSet, setSelectedRowsSet] = useState(new Set<string>(selectedUploadedFileKeys));

    const handleRowSelect = (fileName: string, isSelected: boolean) => {
        const newSelection = new Set(selectedRowsSet);
        if (isSelected) {
            newSelection.add(fileName);
        } else {
            newSelection.delete(fileName);
        }
        setSelectedRowsSet(newSelection);
        onUpdateUploadedFileSelection(Array.from(newSelection));
    };

    const handleSelectAll = (isSelected: boolean) => {
        if (isSelected) {
            const allFileNames = new Set((uploadedFiles || []).map(file => file.name));
            setSelectedRowsSet(allFileNames);
            onUpdateUploadedFileSelection(Array.from(allFileNames));
        } else {
            setSelectedRowsSet(new Set());
            onUpdateUploadedFileSelection([]);
        }
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

    useEffect(() => {
        setSelectedRowsSet(new Set(selectedUploadedFileKeys));
    }, [selectedUploadedFileKeys]);

    return (
        <div className={styles.container}>
            <div className={styles.header}>{intl.formatMessage(KnowledgeBaseResources.fileUploadTitle)}</div>
            <Text className={styles.description}>
                {intl.formatMessage(KnowledgeBaseResources.fileUploadDescription)}{' '}
                <span className={styles.linkText}>{intl.formatMessage(KnowledgeBaseResources.fileUploadLinkDescription)}</span>
            </Text>
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
                        disabled={isDeleteDisabled}
                    >
                        {isDeletingFiles
                            ? intl.formatMessage(KnowledgeBaseResources.deleting)
                            : intl.formatMessage(SreAgentResources.delete)}
                    </ToolbarButton>
                    <SearchBox
                        className={styles.searchBox}
                        placeholder={intl.formatMessage(KnowledgeBaseResources.searchForFiles)}
                        value={searchText}
                        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
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
                <Table sortable className={styles.detailsList}>
                    <TableHeader>
                        <TableRow>
                            <TableHeaderCell className={styles.checkboxCell}>
                                <Checkbox
                                    checked={selectedRowsSet.size === (uploadedFiles || []).length && (uploadedFiles || []).length > 0}
                                    onChange={(_e, data) => handleSelectAll(data.checked === true)}
                                />
                            </TableHeaderCell>
                            {tableFeatures.columns.map(column => (
                                <TableHeaderCell
                                    key={column.columnId}
                                    {...headerSortProps(column.columnId)}
                                    style={getColumnWidth(String(column.columnId))}
                                >
                                    {column.renderHeaderCell()}
                                </TableHeaderCell>
                            ))}
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {tableFeatures.sort.sort(tableFeatures.getRows()).map(({ item }) => (
                            <TableRow key={item.name}>
                                <TableCell className={styles.checkboxCell}>
                                    <Checkbox
                                        checked={selectedRowsSet.has(item.name)}
                                        onChange={(_e, data) => handleRowSelect(item.name, data.checked === true)}
                                    />
                                </TableCell>
                                <TableCell>{item.name}</TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
                {!isLoadingFiles && uploadedFiles.length === 0 && (
                    <EmptyState
                        type="knowledgeBase"
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
                                className={`${styles.dropZone} ${isDragOver ? styles.dropZoneDragOver : styles.dropZoneIdle}`}
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
                                        <span onClick={handleButtonClick} className={styles.linkText}>
                                            {intl.formatMessage(KnowledgeBaseResources.browseForFiles)}
                                        </span>
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
