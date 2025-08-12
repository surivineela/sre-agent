import { Button, Dropdown, MessageBar, MessageBarIntent, Option, ProgressBar, makeStyles, tokens } from '@fluentui/react-components';
import React, { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentMemoryClient } from '../../Common/Clients/AgentMemoryClient';
import { FileUploadResources, SettingsTabResources } from '../../Strings/SREAgentResources';
import { useSettingsStyles } from './Styles/Settings.styles';

interface UploadError {
    fileName: string;
    errorMessage: string;
}

interface FileUploadProps {
    standalone?: boolean;
}

enum DocumentCategory {
    General = 'general',
    TroubleshootingGuide = 'troubleshooting-guide',
    StandardOperatingProcedures = 'standard-operating-procedures',
    SystemDesign = 'system-design',
}

type CategoryOption = { key: DocumentCategory; text: string };

const useFileUploadStyles = makeStyles({
    dragDropArea: {
        border: `2px dashed ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusLarge,
        padding: '32px',
        textAlign: 'center',
        backgroundColor: tokens.colorNeutralBackground2,
        cursor: 'pointer',
        marginBottom: '24px',
        transition: 'border-color 0.2s ease',
        ':hover': {
            border: `2px dashed ${tokens.colorBrandStroke1}`,
        },
        // Allow keyboard focus ring
        ':focus-visible': {
            outline: `2px solid ${tokens.colorBrandStroke1}`,
            outlineOffset: '2px',
        },
    },
    dragDropHeader: {
        margin: '0 0 8px 0',
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    dragDropText: {
        margin: '0',
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase300,
    },
    description: {
        margin: '0 0 16px 0',
        color: tokens.colorNeutralForeground2,
        lineHeight: '20px',
    },
    categoryDescription: {
        margin: '8px 0 0 0',
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
    },
    uploadErrors: {
        marginBottom: '24px',
    },
    uploadErrorsTitle: {
        margin: '0 0 16px 0',
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    hiddenInput: {
        display: 'none',
    },
    categoryDropdown: {
        width: '300px',
    },
});

const FileUpload: FC<FileUploadProps> = ({ standalone = false }) => {
    const intl = useIntl();
    const settingsStyles = useSettingsStyles();
    const styles = useFileUploadStyles();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    // Keep a stable client instance
    const agentMemoryClient = useMemo(() => AgentMemoryClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

    const [isUploading, setIsUploading] = useState(false);
    const [uploadMessage, setUploadMessage] = useState<string | null>(null);
    const [uploadMessageType, setUploadMessageType] = useState<MessageBarIntent>('info');
    const [uploadErrors, setUploadErrors] = useState<UploadError[]>([]);
    const [selectedCategory, setSelectedCategory] = useState<DocumentCategory>(DocumentCategory.General);

    // Prevent state updates after unmount
    const isMountedRef = useRef(true);
    useEffect(() => {
        return () => {
            isMountedRef.current = false;
        };
    }, []);

    const inputRef = useRef<HTMLInputElement>(null);

    const categoryOptions: CategoryOption[] = useMemo(
        () => [
            { key: DocumentCategory.General, text: intl.formatMessage(FileUploadResources.categoryGeneral) },
            { key: DocumentCategory.TroubleshootingGuide, text: intl.formatMessage(FileUploadResources.categoryTroubleshootingGuide) },
            {
                key: DocumentCategory.StandardOperatingProcedures,
                text: intl.formatMessage(FileUploadResources.categoryStandardOperatingProcedures),
            },
            { key: DocumentCategory.SystemDesign, text: intl.formatMessage(FileUploadResources.categorySystemDesign) },
        ],
        [intl]
    );

    const handleFileUpload = useCallback(
        async (files: FileList) => {
            if (!files || files.length === 0) return;

            setIsUploading(true);
            setUploadMessage(null);
            setUploadErrors([]);

            const formData = new FormData();

            // Add all files; server validates
            for (let i = 0; i < files.length; i++) {
                formData.append('files', files[i]);
            }

            // Add category for all files
            formData.append('category', selectedCategory);

            // Add triggerIndexing parameter (defaults to true for immediate indexing)
            formData.append('triggerIndexing', 'true');

            try {
                const result = await agentMemoryClient.uploadFiles(formData);

                if (!isMountedRef.current) return;

                if (result.isSuccessful) {
                    const successMessage = result.content?.message || intl.formatMessage(FileUploadResources.uploadSuccess);
                    setUploadMessage(successMessage);
                    setUploadMessageType('success');
                    setUploadErrors([]); // Clear any previous errors
                } else {
                    // Handle partial success scenario where some files uploaded but others failed
                    const details = Array.isArray(result.content?.detail) ? (result.content.detail as UploadError[]) : [];
                    const uploaded = Array.isArray(result.content?.uploaded) ? result.content.uploaded : [];

                    if (details.length) {
                        setUploadErrors(details);
                    }

                    if (uploaded.length > 0) {
                        // Some files succeeded, show partial success message
                        setUploadMessage(`${uploaded.length} of ${files.length} files uploaded successfully`);
                        setUploadMessageType('warning');
                    } else {
                        // Complete failure
                        const errorMessage = result.error?.message || result.content?.error || 'Unknown error';
                        setUploadMessage(intl.formatMessage(FileUploadResources.uploadError, { error: errorMessage }));
                        setUploadMessageType('error');
                    }
                }
            } catch (error) {
                if (!isMountedRef.current) return;
                const errorMessage = error instanceof Error ? error.message : 'Unknown error';
                setUploadMessage(intl.formatMessage(FileUploadResources.uploadError, { error: errorMessage }));
                setUploadMessageType('error');
            } finally {
                if (isMountedRef.current) {
                    setIsUploading(false);
                }
            }
        },
        [intl, agentMemoryClient, selectedCategory]
    );

    const handleFileInputChange = useCallback(
        (event: React.ChangeEvent<HTMLInputElement>) => {
            const files = event.target.files;
            if (files) {
                handleFileUpload(files);
            }
        },
        [handleFileUpload]
    );

    const handleDragOver = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        event.stopPropagation();
    }, []);

    const handleDrop = useCallback(
        (event: React.DragEvent<HTMLDivElement>) => {
            event.preventDefault();
            event.stopPropagation();
            const files = event.dataTransfer.files;
            if (files) {
                handleFileUpload(files);
            }
        },
        [handleFileUpload]
    );

    const handleBrowseClick = useCallback(() => {
        inputRef.current?.click();
    }, []);

    const handleDropzoneKeyDown = useCallback(
        (e: React.KeyboardEvent<HTMLDivElement>) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                handleBrowseClick();
            }
        },
        [handleBrowseClick]
    );

    return (
        <div style={standalone ? settingsStyles.navPivotContainer : undefined} data-testid="file-upload-root">
            <div>
                <div style={settingsStyles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.fileUpload)}</div>

                <div style={{ marginBottom: '24px' }}>
                    <p className={styles.description}>{intl.formatMessage(FileUploadResources.uploadDescription)}</p>
                </div>

                <div style={{ marginBottom: '24px' }}>
                    <Dropdown
                        placeholder={intl.formatMessage(FileUploadResources.categoryLabel)}
                        value={categoryOptions.find(opt => opt.key === selectedCategory)?.text}
                        onOptionSelect={(_, data) => {
                            if (data.optionValue) {
                                setSelectedCategory(data.optionValue as DocumentCategory);
                            }
                        }}
                        disabled={isUploading}
                        className={styles.categoryDropdown}
                    >
                        {categoryOptions.map(option => (
                            <Option key={option.key} value={option.key}>
                                {option.text}
                            </Option>
                        ))}
                    </Dropdown>
                    <p className={styles.categoryDescription}>{intl.formatMessage(FileUploadResources.categoryDescription)}</p>
                </div>

                <div
                    className={styles.dragDropArea}
                    onDragOver={handleDragOver}
                    onDrop={handleDrop}
                    onClick={handleBrowseClick}
                    onKeyDown={handleDropzoneKeyDown}
                    role="button"
                    tabIndex={0}
                    aria-label={intl.formatMessage(FileUploadResources.dragAndDrop)}
                    data-testid="file-dropzone"
                >
                    <div style={{ marginBottom: '16px' }}>
                        <h3 className={styles.dragDropHeader}>{intl.formatMessage(FileUploadResources.dragAndDrop)}</h3>
                        <p className={styles.dragDropText}>{intl.formatMessage(FileUploadResources.supportedFormats)}</p>
                    </div>

                    <Button
                        appearance="primary"
                        disabled={isUploading}
                        onClick={e => {
                            e.stopPropagation();
                            handleBrowseClick();
                        }}
                    >
                        {intl.formatMessage(FileUploadResources.selectFiles)}
                    </Button>

                    <input
                        ref={inputRef}
                        id="file-input"
                        type="file"
                        multiple
                        // Hint (optional): align with your supported formats text
                        // accept=".pdf,.doc,.docx,.txt,.md"
                        onChange={handleFileInputChange}
                        onClick={e => {
                            // allow selecting the same file twice
                            (e.currentTarget as HTMLInputElement).value = '';
                        }}
                        className={styles.hiddenInput}
                        data-testid="file-input"
                    />
                </div>

                {isUploading && (
                    <div style={{ marginBottom: '24px' }}>
                        {/* Indeterminate since we don't track incremental progress */}
                        <ProgressBar />
                        <p style={{ marginTop: '8px', fontSize: tokens.fontSizeBase300, color: tokens.colorNeutralForeground2 }}>
                            {intl.formatMessage(FileUploadResources.uploading)}
                        </p>
                    </div>
                )}

                {uploadMessage && (
                    <MessageBar
                        intent={uploadMessageType}
                        style={{ marginBottom: '24px' }}
                        // Make SRs announce result changes
                        aria-live="polite"
                        data-testid="upload-message"
                    >
                        {uploadMessage}
                    </MessageBar>
                )}

                {uploadErrors.length > 0 && (
                    <div className={styles.uploadErrors}>
                        <h4 className={styles.uploadErrorsTitle}>Upload Errors:</h4>
                        {uploadErrors.map((error, index) => (
                            <MessageBar key={index} intent="error" style={{ marginBottom: '12px' }} data-testid={`upload-error-${index}`}>
                                <strong>{error.fileName}:</strong> {error.errorMessage}
                            </MessageBar>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
};

export default FileUpload;
