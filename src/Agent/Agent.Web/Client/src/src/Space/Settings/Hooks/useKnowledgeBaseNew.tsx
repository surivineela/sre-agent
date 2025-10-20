import { TableColumnDefinition, TableColumnId, createTableColumn, useTableFeatures, useTableSort } from '@fluentui/react-components';
import React, { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentMemoryClient } from '../../../Common/Clients/AgentMemoryClient';
import { KnowledgeBaseResources } from '../../../Strings/SREAgentResources';

export interface UploadedFile {
    name: string;
    size: number;
    dataClassification: string;
    lastModified: string;
}

export interface UseKnowledgeBaseReturn {
    // State
    selectedFiles: File[];
    uploadedFiles: UploadedFile[];
    originalUploadedFiles: UploadedFile[];
    selectedUploadedFileKeys: string[];
    isLoadingFiles: boolean;
    isUploading: boolean;
    isDeletingFiles: boolean;
    isDragOver: boolean;
    searchText: string;

    // Handlers
    handleFileSelect: (files: FileList) => void;
    handleFileInputChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
    handleButtonClick: () => void;
    handleDragOver: (event: React.DragEvent<HTMLDivElement>) => void;
    handleDragLeave: (event: React.DragEvent<HTMLDivElement>) => void;
    handleDrop: (event: React.DragEvent<HTMLDivElement>) => void;
    handleRemoveFile: (index: number) => void;
    handleRemoveAllFiles: () => void;
    handleUploadFiles: () => Promise<void>;
    handleBulkDeleteFiles: () => Promise<void>;
    onUpdateUploadedFileSelection: (selectedKeys: string[]) => void;
    handleRefresh: () => void;
    setSearchText: (searchText: string) => void;
    handleEditFile: (fileName: string, editData?: { newName: string; dataClassification: string }) => void;
    handleDeleteSingleFile: (fileName: string) => Promise<void>;
    handleDownloadFile: (fileName: string) => void;

    // Utils
    formatFileSize: (bytes: number) => string;
    isValidFileType: (file: File) => boolean;

    // Table features
    tableFeatures: ReturnType<typeof useTableFeatures<UploadedFile>>;
    headerSortProps: (columnId: TableColumnId) => any;
    getColumnWidth: (columnId: string) => { width: string; minWidth: string } | { width: string };

    // Refs
    fileInputRef: React.RefObject<HTMLInputElement>;
}

export const useKnowledgeBase = (portalContext: AzPortalProxy, resourceId: string): UseKnowledgeBaseReturn => {
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const refreshTimeoutRef = useRef<NodeJS.Timeout | null>(null);

    // State
    const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
    const [isDragOver, setIsDragOver] = useState(false);
    const [uploadedFiles, setUploadedFiles] = useState<UploadedFile[]>([]);
    const [isLoadingFiles, setIsLoadingFiles] = useState(false);
    const [isUploading, setIsUploading] = useState(false);
    const [selectedUploadedFileKeys, setSelectedUploadedFileKeys] = useState<string[]>([]);
    const [isDeletingFiles, setIsDeletingFiles] = useState(false);
    const [searchText, setSearchText] = useState<string>('');

    // Client
    const agentMemoryClient = useMemo(() => {
        return AgentMemoryClient.getInstance(sreAgentEndpoint);
    }, [sreAgentEndpoint]);

    // Utility functions
    const isValidFileType = useCallback((file: File) => {
        const extension = file.name.toLowerCase().split('.').pop();
        return extension === 'md' || extension === 'txt';
    }, []);

    const formatFileSize = useCallback((bytes: number) => {
        if (bytes < 1024) {
            return `${bytes} B`;
        } else if (bytes < 1024 * 1024) {
            return `${(bytes / 1024).toFixed(1)} KB`;
        } else {
            return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
        }
    }, []);

    // Filtered uploaded files based on search text
    const filteredUploadedFiles = useMemo(() => {
        if (!searchText) {
            return uploadedFiles;
        }
        return uploadedFiles.filter(file => file.name.toLowerCase().includes(searchText.toLowerCase()));
    }, [uploadedFiles, searchText]);

    // Load uploaded files
    const loadUploadedFiles = useCallback(async () => {
        setIsLoadingFiles(true);
        try {
            const response = await agentMemoryClient.listFiles();

            if (response.isSuccessful) {
                const fileNames = Array.isArray(response.content?.files) ? response.content.files : [];
                const files = fileNames.map(fileName => ({
                    name: fileName,
                    size: 0, // Default value since API doesn't provide this yet
                    dataClassification: 'All', // Default value
                    lastModified: new Date().toISOString(), // Default to current date
                }));
                setUploadedFiles(files);
            } else {
                portalContext.log({
                    action: 'loadUploadedFiles',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to load uploaded files: ${response.error?.message || String(response.error) || 'Unknown error'}`,
                    },
                });
                setUploadedFiles([]);
            }
        } catch (error) {
            portalContext.log({
                action: 'loadUploadedFiles',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    message: `Failed to load uploaded files: ${String(error)}`,
                },
            });
            setUploadedFiles([]);
        } finally {
            setIsLoadingFiles(false);
        }
    }, [agentMemoryClient, portalContext, resourceId]);

    // Debounced refresh function to avoid indexer conflicts
    const scheduleRefresh = useCallback(() => {
        if (refreshTimeoutRef.current) {
            clearTimeout(refreshTimeoutRef.current);
        }
        refreshTimeoutRef.current = setTimeout(() => {
            loadUploadedFiles();
        }, 2500); // Wait 2.5 seconds for server-side processing to complete
    }, [loadUploadedFiles]);

    // File selection handlers
    const handleFileSelect = useCallback(
        (files: FileList) => {
            const fileArray = Array.from(files);
            const validFiles = fileArray.filter(isValidFileType);

            // Append new files to existing ones, avoiding duplicates
            setSelectedFiles(prevFiles => {
                const existingFileNames = prevFiles.map(f => f.name);
                const newFiles = validFiles.filter(file => !existingFileNames.includes(file.name));
                return [...prevFiles, ...newFiles];
            });
        },
        [isValidFileType]
    );

    const handleFileInputChange = useCallback(
        (event: React.ChangeEvent<HTMLInputElement>) => {
            const files = event.target.files;
            if (files) {
                handleFileSelect(files);
            }
        },
        [handleFileSelect]
    );

    const handleButtonClick = useCallback(() => {
        fileInputRef.current?.click();
    }, []);

    // Drag and drop handlers
    const handleDragOver = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragOver(true);
    }, []);

    const handleDragLeave = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragOver(false);
    }, []);

    const handleDrop = useCallback(
        (event: React.DragEvent<HTMLDivElement>) => {
            event.preventDefault();
            setIsDragOver(false);

            const files = event.dataTransfer.files;
            if (files && files.length > 0) {
                handleFileSelect(files);
            }
        },
        [handleFileSelect]
    );

    // File management handlers
    const handleRemoveFile = useCallback(
        (index: number) => {
            const newFiles = selectedFiles.filter((_, i) => i !== index);
            setSelectedFiles(newFiles);
            if (newFiles.length === 0 && fileInputRef.current) {
                fileInputRef.current.value = '';
            }
        },
        [selectedFiles]
    );

    const handleRemoveAllFiles = useCallback(() => {
        setSelectedFiles([]);
        if (fileInputRef.current) {
            fileInputRef.current.value = '';
        }
    }, []);

    // Upload handler
    const handleUploadFiles = useCallback(async () => {
        if (selectedFiles.length === 0) return;

        setIsUploading(true);
        const notificationId = portalContext.startNotification(
            intl.formatMessage(KnowledgeBaseResources.uploadingFiles),
            intl.formatMessage(KnowledgeBaseResources.uploadingFilesMessage)
        );

        try {
            const formData = new FormData();
            selectedFiles.forEach(file => {
                formData.append('files', file);
            });

            const response = await agentMemoryClient.uploadFiles(formData);

            if (response.isSuccessful) {
                setSelectedFiles([]);
                if (fileInputRef.current) {
                    fileInputRef.current.value = '';
                }
                scheduleRefresh();
                portalContext.stopNotification(notificationId, true, intl.formatMessage(KnowledgeBaseResources.filesUploadedSuccessfully));
            } else {
                portalContext.log({
                    action: 'uploadFiles',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to upload files: ${response.error?.message || String(response.error) || 'Unknown error'}`,
                    },
                });
                portalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(KnowledgeBaseResources.uploadFailed, {
                        error: response.error?.message || 'Unknown error',
                    })
                );
            }
        } catch (error) {
            portalContext.log({
                action: 'uploadFiles',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    message: `Failed to upload files: ${String(error)}`,
                },
            });
            portalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(KnowledgeBaseResources.uploadFailed, {
                    error: String(error),
                })
            );
        } finally {
            setIsUploading(false);
        }
    }, [selectedFiles, agentMemoryClient, scheduleRefresh, intl, portalContext, resourceId]);

    // Delete handlers
    const handleBulkDeleteFiles = useCallback(async () => {
        if (selectedUploadedFileKeys.length === 0) return;

        setIsDeletingFiles(true);
        const notificationId = portalContext.startNotification(
            intl.formatMessage(KnowledgeBaseResources.deletingFiles),
            intl.formatMessage(KnowledgeBaseResources.deletingFilesMessage)
        );

        // Add a timeout to ensure the operation doesn't hang indefinitely
        const deletePromise = agentMemoryClient.deleteDocuments(selectedUploadedFileKeys);
        const timeoutPromise = new Promise((_, reject) =>
            setTimeout(() => reject(new Error('Delete operation timed out after 30 seconds')), 30000)
        );

        try {
            const response = (await Promise.race([deletePromise, timeoutPromise])) as any;
            if (response.isSuccessful) {
                // Immediately update local state for instant feedback
                const deletedFiles = response.content?.deleted || selectedUploadedFileKeys;
                setUploadedFiles(prev => prev.filter(file => !deletedFiles.includes(file.name)));
                setSelectedUploadedFileKeys([]);
                scheduleRefresh();

                // Show warning message if some files failed
                if (response.content?.failed && response.content.failed.length > 0) {
                    portalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(KnowledgeBaseResources.someFilesFailedToDelete, {
                            failedFiles: response.content.failed.join(', '),
                        })
                    );
                } else {
                    portalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(KnowledgeBaseResources.filesDeletedSuccessfully)
                    );
                }
            } else {
                portalContext.log({
                    action: 'deleteFiles',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to delete files: ${response.error?.message || String(response.error) || 'Unknown error'}`,
                    },
                });
                portalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(KnowledgeBaseResources.bulkDeleteFailed, {
                        error: response.error?.message || 'Unknown error',
                    })
                );
            }
        } catch (error) {
            portalContext.log({
                action: 'deleteFiles',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    message: `Failed to delete files: ${String(error)}`,
                },
            });
            portalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(KnowledgeBaseResources.bulkDeleteFailed, {
                    error: String(error),
                })
            );
        } finally {
            setIsDeletingFiles(false);
        }

        // Fallback: ensure the state is reset after a maximum time
        setTimeout(() => {
            setIsDeletingFiles(false);
        }, 35000);
    }, [selectedUploadedFileKeys, agentMemoryClient, scheduleRefresh, intl, portalContext, resourceId]);

    // Selection handler
    const onUpdateUploadedFileSelection = useCallback((selectedKeys: string[]) => {
        setSelectedUploadedFileKeys(selectedKeys);
    }, []);

    // Refresh handler
    const handleRefresh = useCallback(() => {
        loadUploadedFiles();
    }, [loadUploadedFiles]);

    // Individual file handlers
    const handleEditFile = useCallback(
        (fileName: string, editData?: { newName: string; dataClassification: string }) => {
            if (editData) {
                // TODO: Implement actual edit functionality with API call
                console.log('Save edit:', {
                    originalName: fileName,
                    newName: editData.newName,
                    dataClassification: editData.dataClassification,
                });
                // TODO: Update the file in the backend
                // TODO: Refresh the file list after successful edit
            } else {
                // Just logging when called without edit data (for opening dialog)
                console.log('Edit file:', fileName);
            }

            portalContext.log({
                action: 'editFile',
                actionModifier: editData ? 'saved' : 'initiated',
                resourceId,
                logLevel: 'info',
                data: { fileName, editData },
            });
        },
        [portalContext, resourceId]
    );

    const handleDeleteSingleFile = useCallback(
        async (fileName: string) => {
            try {
                setIsDeletingFiles(true);
                const response = await agentMemoryClient.deleteDocument(fileName);

                if (response.isSuccessful) {
                    // Remove from local state immediately
                    setUploadedFiles(prev => prev.filter(file => file.name !== fileName));
                    scheduleRefresh(); // Also refresh from server
                    portalContext.log({
                        action: 'deleteSingleFile',
                        actionModifier: 'succeeded',
                        resourceId,
                        logLevel: 'info',
                        data: { fileName },
                    });
                } else {
                    portalContext.log({
                        action: 'deleteSingleFile',
                        actionModifier: 'failed',
                        resourceId,
                        logLevel: 'error',
                        data: { fileName, error: response.error },
                    });
                }
            } catch (error) {
                portalContext.log({
                    action: 'deleteSingleFile',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: { fileName, error: String(error) },
                });
            } finally {
                setIsDeletingFiles(false);
            }
        },
        [agentMemoryClient, scheduleRefresh, portalContext, resourceId]
    );

    const handleDownloadFile = useCallback(
        (fileName: string) => {
            // TODO: Implement download functionality
            console.log('Download file:', fileName);
            portalContext.log({
                action: 'downloadFile',
                actionModifier: 'initiated',
                resourceId,
                logLevel: 'info',
                data: { fileName },
            });
        },
        [portalContext, resourceId]
    );

    // Columns configuration
    const columns = useMemo<TableColumnDefinition<UploadedFile>[]>(() => {
        return [
            createTableColumn<UploadedFile>({
                columnId: 'name',
                compare: (a, b) => a.name.localeCompare(b.name),
                renderHeaderCell: () => <span style={{ fontWeight: 500 }}>{intl.formatMessage(KnowledgeBaseResources.fileName)}</span>,
                renderCell: fileObj => fileObj.name,
            }),
            createTableColumn<UploadedFile>({
                columnId: 'size',
                compare: (a, b) => a.size - b.size,
                renderHeaderCell: () => <span style={{ fontWeight: 500 }}>{intl.formatMessage(KnowledgeBaseResources.fileSize)}</span>,
                renderCell: fileObj => formatFileSize(fileObj.size),
            }),
            createTableColumn<UploadedFile>({
                columnId: 'dataClassification',
                compare: (a, b) => a.dataClassification.localeCompare(b.dataClassification),
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 500 }}>{intl.formatMessage(KnowledgeBaseResources.fileDataClassification)}</span>
                ),
                renderCell: fileObj => fileObj.dataClassification,
            }),
            createTableColumn<UploadedFile>({
                columnId: 'lastModified',
                compare: (a, b) => new Date(a.lastModified).getTime() - new Date(b.lastModified).getTime(),
                renderHeaderCell: () => <span style={{ fontWeight: 500 }}>{intl.formatMessage(KnowledgeBaseResources.lastModified)}</span>,
                renderCell: fileObj => fileObj.lastModified,
            }),
        ];
    }, [intl, formatFileSize]);

    // Table features with sorting
    const tableFeatures = useTableFeatures(
        {
            columns,
            items: filteredUploadedFiles,
        },
        [
            useTableSort({
                defaultSortState: { sortColumn: 'name', sortDirection: 'ascending' },
            }),
        ]
    );

    // Column width configuration
    const getColumnWidth = useCallback((columnId: string) => {
        switch (columnId) {
            case 'name':
                return { width: '250px', minWidth: '250px' };
            case 'size':
                return { width: '150px', minWidth: '150px' };
            case 'dataClassification':
                return { width: '150px', minWidth: '150px' };
            case 'lastModified':
                return { width: '200px', minWidth: '200px' };
            default:
                return { width: 'auto' };
        }
    }, []);

    // Header sort props helper
    const headerSortProps = (columnId: TableColumnId) => ({
        onClick: (e: React.MouseEvent) => {
            tableFeatures.sort.toggleColumnSort(e, columnId);
        },
        sortDirection: tableFeatures.sort.getSortDirection(columnId),
    });

    // Effects
    useEffect(() => {
        loadUploadedFiles();
    }, [loadUploadedFiles]);

    // Cleanup timeout on unmount
    useEffect(() => {
        return () => {
            if (refreshTimeoutRef.current) {
                clearTimeout(refreshTimeoutRef.current);
            }
        };
    }, []);

    return {
        // State
        selectedFiles,
        uploadedFiles: filteredUploadedFiles,
        originalUploadedFiles: uploadedFiles,
        selectedUploadedFileKeys,
        isLoadingFiles,
        isUploading,
        isDeletingFiles,
        isDragOver,
        searchText,

        // Handlers
        handleFileSelect,
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
        handleEditFile,
        handleDeleteSingleFile,
        handleDownloadFile,

        // Utils
        formatFileSize,
        isValidFileType,

        // Table features
        tableFeatures,
        headerSortProps,
        getColumnWidth,

        // Refs
        fileInputRef,
    };
};
