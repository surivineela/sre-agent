import { ConstrainMode, DetailsListLayoutMode } from '@fluentui/react';
import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    InputOnChangeData,
    SearchBox,
    SearchBoxChangeEvent,
    Text,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { debounce } from 'lodash';
import { FC, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import ShimmeredDetailsListWithSelection, { OnUpdateSelectionArgs } from '../../Common/Components/ShimmeredDetailsListWithSelection';
import { KnowledgeBaseResources, SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { UploadedFile, useKnowledgeBase } from './Hooks/useKnowledgeBase';
import { useKnowledgeBaseStyles } from './Styles/KnowledgeBase.styles';

const KnowledgeBase: FC = () => {
    const intl = useIntl();
    const portalContext = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);
    const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);

    const styles = useKnowledgeBaseStyles();

    const {
        selectedFiles,
        uploadedFiles,
        selectedUploadedFileKeys,
        isLoadingFiles,
        isUploading,
        isDeletingFiles,
        isDragOver,
        searchText,
        columns,
        handleFileInputChange,
        handleButtonClick,
        handleDragOver,
        handleDragLeave,
        handleDrop,
        handleRemoveFile,
        handleRemoveAllFiles,
        handleUploadFiles,
        handleBulkDeleteFiles,
        onUpdateUploadedFileSelection,
        handleRefresh,
        setSearchText,
        formatFileSize,
        fileInputRef,
    } = useKnowledgeBase(portalContext, resourceId);

    const handleUploadAndClose = async () => {
        await handleUploadFiles();
        setIsUploadModalOpen(false);
    };

    const isDeleteDisabled = selectedUploadedFileKeys.length === 0 || isDeletingFiles;

    const handleSelectionUpdate = ({ selectedKeys }: OnUpdateSelectionArgs<UploadedFile>) => {
        onUpdateUploadedFileSelection(selectedKeys);
    };

    return (
        <div className={styles.container}>
            <div className={styles.header}>{intl.formatMessage(SettingsTabResources.knowledgeBase)}</div>
            {intl.formatMessage(KnowledgeBaseResources.fileUploadDescription)}
            <div className={styles.buttonsContainer}>
                <Toolbar>
                    <ToolbarButton
                        icon={<Add16Regular />}
                        className={styles.toolbarButton}
                        appearance="subtle"
                        disabled={isLoadingFiles || isUploading}
                        onClick={() => setIsUploadModalOpen(true)}
                    >
                        {intl.formatMessage(KnowledgeBaseResources.uploadFiles)}
                    </ToolbarButton>
                    <ToolbarButton
                        icon={<ArrowClockwise16Regular />}
                        className={styles.toolbarButton}
                        appearance="subtle"
                        disabled={isLoadingFiles || isUploading}
                        onClick={handleRefresh}
                    >
                        {intl.formatMessage(KnowledgeBaseResources.refresh)}
                    </ToolbarButton>
                    <ToolbarDivider className={styles.toolbarDivider} />
                    <ToolbarButton
                        icon={<Delete16Regular />}
                        appearance="subtle"
                        onClick={handleBulkDeleteFiles}
                        disabled={isDeleteDisabled}
                    >
                        {isDeletingFiles
                            ? intl.formatMessage(KnowledgeBaseResources.deleting)
                            : intl.formatMessage(SreAgentResources.delete)}
                    </ToolbarButton>
                </Toolbar>
            </div>
            <div className={styles.pillsContainer}>
                <SearchBox
                    className={styles.searchBox}
                    placeholder={intl.formatMessage(KnowledgeBaseResources.searchForFiles)}
                    value={searchText}
                    onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
                />
            </div>

            {selectedUploadedFileKeys.length > 0 && (
                <Text className={styles.filesSelectedText}>
                    {intl.formatMessage(KnowledgeBaseResources.filesSelected, {
                        count: selectedUploadedFileKeys.length,
                        plural: selectedUploadedFileKeys.length > 1 ? 's' : '',
                    })}
                </Text>
            )}

            <div className={styles.detailsListContainer}>
                <ShimmeredDetailsListWithSelection<UploadedFile>
                    enableShimmer={isLoadingFiles}
                    items={uploadedFiles || []}
                    getKey={fileObj => fileObj.name}
                    columns={columns}
                    className={styles.detailsList}
                    selectedKeys={selectedUploadedFileKeys}
                    onUpdateSelection={handleSelectionUpdate}
                    layoutMode={DetailsListLayoutMode.justified}
                    constrainMode={ConstrainMode.horizontalConstrained}
                />
                {!isLoadingFiles && uploadedFiles.length === 0 && (
                    <div className={styles.noFilesContainer}>{intl.formatMessage(KnowledgeBaseResources.noFilesUploaded)}</div>
                )}
            </div>

            <Dialog open={isUploadModalOpen} onOpenChange={(_, data) => setIsUploadModalOpen(data.open)}>
                <DialogSurface className={styles.dialogSurface}>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(KnowledgeBaseResources.uploadFiles)}</DialogTitle>
                        <DialogContent className={styles.dialogContent}>
                            <Text>{intl.formatMessage(KnowledgeBaseResources.acceptedFileTypes)}</Text>

                            <div
                                className={`${styles.dropZone} ${isDragOver ? styles.dropZoneDragOver : styles.dropZoneIdle}`}
                                onDragOver={handleDragOver}
                                onDragLeave={handleDragLeave}
                                onDrop={handleDrop}
                            >
                                {selectedFiles.length === 0 ? (
                                    <div className={styles.emptyDropZone}>
                                        <Text>{intl.formatMessage(KnowledgeBaseResources.dragAndDropFiles)}</Text>
                                        <Button appearance="primary" onClick={handleButtonClick}>
                                            {intl.formatMessage(KnowledgeBaseResources.browseFiles)}
                                        </Button>
                                    </div>
                                ) : (
                                    <div className={styles.selectedFilesContainer}>
                                        <Text className={styles.selectedFilesTitle}>
                                            {intl.formatMessage(KnowledgeBaseResources.selectedFiles)}
                                        </Text>
                                        <div className={styles.fileList}>
                                            {selectedFiles.map((file, index) => (
                                                <div key={index} className={styles.fileItem}>
                                                    <Text className={styles.fileName}>
                                                        {file.name} ({formatFileSize(file.size)})
                                                    </Text>
                                                    <Button appearance="subtle" onClick={() => handleRemoveFile(index)} size="small">
                                                        {intl.formatMessage(KnowledgeBaseResources.remove)}
                                                    </Button>
                                                </div>
                                            ))}
                                        </div>
                                        <div className={styles.fileActions}>
                                            <Button appearance="secondary" onClick={handleRemoveAllFiles} size="small">
                                                {intl.formatMessage(KnowledgeBaseResources.removeAll)}
                                            </Button>
                                            <Button appearance="primary" onClick={handleButtonClick} size="small">
                                                {intl.formatMessage(KnowledgeBaseResources.addMoreFiles)}
                                            </Button>
                                        </div>
                                    </div>
                                )}
                            </div>

                            <input
                                ref={fileInputRef}
                                type="file"
                                onChange={handleFileInputChange}
                                className={styles.hiddenFileInput}
                                accept=".md,.txt"
                                multiple
                            />
                        </DialogContent>
                        <DialogActions>
                            <Button
                                appearance="primary"
                                onClick={handleUploadAndClose}
                                disabled={selectedFiles.length === 0 || isUploading}
                            >
                                {isUploading
                                    ? intl.formatMessage(KnowledgeBaseResources.uploading)
                                    : intl.formatMessage(KnowledgeBaseResources.uploadFiles)}
                            </Button>
                            <Button appearance="secondary" onClick={() => setIsUploadModalOpen(false)}>
                                {intl.formatMessage(SreAgentResources.cancel)}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </div>
    );
};

export default KnowledgeBase;
