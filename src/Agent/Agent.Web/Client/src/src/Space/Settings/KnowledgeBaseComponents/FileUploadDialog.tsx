import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Field,
    Input,
    Link,
    mergeClasses,
    Table,
    TableBody,
    TableCell,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    Textarea,
} from '@fluentui/react-components';
import { Add16Regular, Delete20Regular, Document20Regular, DocumentText16Regular } from '@fluentui/react-icons';
import { FC, RefObject, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { KnowledgeBaseResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useKnowledgeBaseStyles } from '../Styles/KnowledgeBase.styles';

const ACCEPTED_FILE_TYPES = '.md,.txt';

type DialogStep = 'upload' | 'create';

interface FileUploadDialogProps {
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    selectedFiles: File[];
    isDragOver: boolean;
    isUploading: boolean;
    fileInputRef: RefObject<HTMLInputElement>;
    onDragOver: (event: React.DragEvent<HTMLDivElement>) => void;
    onDragLeave: (event: React.DragEvent<HTMLDivElement>) => void;
    onDrop: (event: React.DragEvent<HTMLDivElement>) => void;
    onBrowseClick: () => void;
    onFileInputChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
    onRemoveFile: (index: number) => void;
    onUpload: () => void;
    onCancel: () => void;
    onCreateFile: () => void;
}

export const FileUploadDialog: FC<FileUploadDialogProps> = ({
    isOpen,
    onOpenChange,
    selectedFiles,
    isDragOver,
    isUploading,
    fileInputRef,
    onDragOver,
    onDragLeave,
    onDrop,
    onBrowseClick,
    onFileInputChange,
    onRemoveFile,
    onUpload,
    onCancel,
    onCreateFile,
}) => {
    const intl = useIntl();
    const styles = useKnowledgeBaseStyles();
    const [currentStep, setCurrentStep] = useState<DialogStep>('upload');
    const [createFileName, setCreateFileName] = useState('');
    const [createFileText, setCreateFileText] = useState('');

    const handleCreateFileClick = useCallback(() => {
        setCurrentStep('create');
    }, []);

    const handleBackClick = useCallback(() => {
        setCurrentStep('upload');
    }, []);

    const handleNextClick = useCallback(() => {
        setCurrentStep('upload');
        onCreateFile();
    }, [onCreateFile]);

    const handleCancel = useCallback(() => {
        setCurrentStep('upload');
        setCreateFileName('');
        setCreateFileText('');
        onCancel();
    }, [onCancel]);

    const isCreateFormValid = createFileName.trim() !== '' && createFileText.trim() !== '';

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle>
                        <div className={styles.dialogTitleContainer}>
                            <Document20Regular />
                            {currentStep === 'upload'
                                ? intl.formatMessage(KnowledgeBaseResources.addFile)
                                : intl.formatMessage(KnowledgeBaseResources.createTextFile)}
                        </div>
                    </DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        {currentStep === 'upload' ? (
                            <>
                                <Text>{intl.formatMessage(KnowledgeBaseResources.fileSizeLimit)}</Text>

                                <div
                                    className={mergeClasses(styles.dropZone, isDragOver ? styles.dropZoneDragOver : styles.dropZoneIdle)}
                                    onDragOver={onDragOver}
                                    onDragLeave={onDragLeave}
                                    onDrop={onDrop}
                                >
                                    <div className={styles.emptyDropZone}>
                                        <img
                                            src={resolveResourceIcon('folder')}
                                            className={styles.folderIcon}
                                            alt={intl.formatMessage(KnowledgeBaseResources.folder)}
                                        />
                                        <Text>
                                            {intl.formatMessage(KnowledgeBaseResources.dragFilesHere)}{' '}
                                            <Link onClick={onBrowseClick}>{intl.formatMessage(KnowledgeBaseResources.browseForFiles)}</Link>
                                        </Text>
                                    </div>
                                </div>

                                <Button
                                    appearance="outline"
                                    icon={<Add16Regular />}
                                    onClick={handleCreateFileClick}
                                    className={styles.createFileButton}
                                >
                                    {intl.formatMessage(KnowledgeBaseResources.createFile)}
                                </Button>

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
                                                                    onClick={() => onRemoveFile(index)}
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
                                    onChange={onFileInputChange}
                                    className={styles.hiddenFileInput}
                                    accept={ACCEPTED_FILE_TYPES}
                                    multiple
                                />
                            </>
                        ) : (
                            <>
                                <Text>{intl.formatMessage(KnowledgeBaseResources.createTextFileDescription)}</Text>

                                <Field
                                    label={intl.formatMessage(KnowledgeBaseResources.fileNameLabel)}
                                    required
                                    className={styles.formField}
                                >
                                    <Input
                                        value={createFileName}
                                        onChange={(_, data) => setCreateFileName(data.value)}
                                        placeholder={intl.formatMessage(KnowledgeBaseResources.fileNamePlaceholder)}
                                    />
                                </Field>

                                <Field label={intl.formatMessage(KnowledgeBaseResources.textLabel)} required className={styles.formField}>
                                    <Textarea
                                        value={createFileText}
                                        onChange={(_, data) => setCreateFileText(data.value)}
                                        placeholder={intl.formatMessage(KnowledgeBaseResources.textPlaceholder)}
                                        className={styles.createFileTextarea}
                                        resize="vertical"
                                    />
                                </Field>
                            </>
                        )}
                    </DialogContent>
                </DialogBody>
                <DialogActions className={styles.dialogFooter}>
                    {currentStep === 'upload' ? (
                        <>
                            <Button appearance="secondary" onClick={handleCancel}>
                                {intl.formatMessage(SreAgentResources.cancel)}
                            </Button>
                            <Button appearance="primary" onClick={onUpload} disabled={selectedFiles.length === 0 || isUploading}>
                                {isUploading
                                    ? intl.formatMessage(KnowledgeBaseResources.uploading)
                                    : intl.formatMessage(KnowledgeBaseResources.addFile)}
                            </Button>
                        </>
                    ) : (
                        <>
                            <Button appearance="secondary" onClick={handleBackClick}>
                                {intl.formatMessage(SreAgentResources.back)}
                            </Button>
                            <div className={styles.dialogFooterRightButtons}>
                                <Button appearance="primary" onClick={handleNextClick} disabled={!isCreateFormValid}>
                                    {intl.formatMessage(SreAgentResources.next)}
                                </Button>
                                <Button appearance="secondary" onClick={handleCancel}>
                                    {intl.formatMessage(SreAgentResources.cancel)}
                                </Button>
                            </div>
                        </>
                    )}
                </DialogActions>
            </DialogSurface>
        </Dialog>
    );
};
