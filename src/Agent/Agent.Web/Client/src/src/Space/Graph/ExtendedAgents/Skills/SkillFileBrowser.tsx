// TODO (clanker): Consider using Fluent's Tree component instead of custom file list rows for better accessibility
// TODO (clanker): Consider using Fluent's Breadcrumb component for the path bar
// TODO (clanker): Consider adjusting the drop zone to match other Fluent upload patterns more closely

import { Button, Input, Link, mergeClasses, Popover, PopoverSurface, PopoverTrigger, Text } from '@fluentui/react-components';
import {
    ArrowLeft16Regular,
    Delete20Regular,
    DocumentAdd20Regular,
    DocumentText16Regular,
    Folder16Regular,
    FolderAdd20Regular,
} from '@fluentui/react-icons';
import { FC, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { SkillFile } from '../../../Contracts/ExtendedAgentGraph';
import { useSkillFileBrowserStyles } from './SkillFileBrowser.styles';

export const SKILL_MD_FILENAME = 'SKILL.md';

export interface FileSystemItem {
    name: string;
    type: 'file' | 'folder';
    path: string;
    isDefault?: boolean;
    fileIndex?: number; // Index in the files array for non-default files
    isEmptyFolder?: boolean; // True if folder is from emptyFolders list
}

interface SkillFileBrowserProps {
    files: SkillFile[];
    onFilesChange: (files: SkillFile[]) => void;
    skillMdContent: string;
    emptyFolders?: string[];
    onEmptyFoldersChange?: (folders: string[]) => void;
    readOnly?: boolean;
    selectedFile: FileSystemItem | null;
    onFileSelect: (file: FileSystemItem | null) => void;
}

const getParentPath = (path: string): string => {
    if (path === '/') return '/';
    const parts = path.split('/').filter(Boolean);
    parts.pop();
    return parts.length === 0 ? '/' : '/' + parts.join('/') + '/';
};

const getDirectoryContents = (
    currentPath: string,
    files: SkillFile[],
    _skillMdContent: string,
    emptyFolders: string[] = []
): FileSystemItem[] => {
    const items: FileSystemItem[] = [];
    const seenFolders = new Set<string>();

    // Add skill.md at root level
    if (currentPath === '/') {
        items.push({
            name: SKILL_MD_FILENAME,
            type: 'file',
            path: '/' + SKILL_MD_FILENAME,
            isDefault: true,
        });
    }

    // Process additional files
    files.forEach((file, index) => {
        // Normalize file path - ensure it starts with /
        const filePath = file.filePath.startsWith('/') ? file.filePath : '/' + file.filePath;
        const fileDir = filePath.substring(0, filePath.lastIndexOf('/') + 1) || '/';

        if (fileDir === currentPath) {
            // File is directly in current directory
            items.push({
                name: file.fileName,
                type: 'file',
                path: filePath,
                fileIndex: index,
            });
        } else if (fileDir.startsWith(currentPath) && fileDir !== currentPath) {
            // File is in a subdirectory - extract the immediate subfolder
            const relativePath = fileDir.substring(currentPath.length);
            const folderName = relativePath.split('/')[0];
            const folderPath = currentPath + folderName + '/';

            if (!seenFolders.has(folderPath)) {
                seenFolders.add(folderPath);
                items.push({
                    name: folderName,
                    type: 'folder',
                    path: folderPath,
                });
            }
        }
    });

    // Add empty folders that are direct children of current path
    emptyFolders.forEach(folderPath => {
        // Normalize folder path
        const normalizedPath = folderPath.startsWith('/') ? folderPath : '/' + folderPath;

        // Skip if this is the current path itself (we're inside this folder)
        if (normalizedPath === currentPath) {
            return;
        }

        const folderDir = getParentPath(normalizedPath);

        if (folderDir === currentPath && !seenFolders.has(normalizedPath)) {
            seenFolders.add(normalizedPath);
            const folderName = normalizedPath.slice(currentPath.length, -1); // Remove trailing /
            items.push({
                name: folderName,
                type: 'folder',
                path: normalizedPath,
                isEmptyFolder: true,
            });
        } else if (normalizedPath.startsWith(currentPath) && folderDir !== currentPath) {
            // Empty folder is nested deeper - show intermediate folder
            const relativePath = normalizedPath.substring(currentPath.length);
            const folderName = relativePath.split('/')[0];
            const intermediatePath = currentPath + folderName + '/';

            if (!seenFolders.has(intermediatePath)) {
                seenFolders.add(intermediatePath);
                items.push({
                    name: folderName,
                    type: 'folder',
                    path: intermediatePath,
                });
            }
        }
    });

    // Sort: folders first, then files alphabetically (but skill.md always first among files)
    items.sort((a, b) => {
        if (a.type !== b.type) {
            return a.type === 'folder' ? -1 : 1;
        }
        if (a.isDefault) return -1;
        if (b.isDefault) return 1;
        return a.name.localeCompare(b.name);
    });

    return items;
};

export const SkillFileBrowser: FC<SkillFileBrowserProps> = ({
    files,
    onFilesChange,
    skillMdContent,
    emptyFolders = [],
    onEmptyFoldersChange,
    readOnly = false,
    selectedFile,
    onFileSelect,
}) => {
    const intl = useIntl();
    const styles = useSkillFileBrowserStyles();

    const [currentPath, setCurrentPath] = useState('/');
    const [isDragOver, setIsDragOver] = useState(false);
    const [newFolderName, setNewFolderName] = useState('');
    const [isNewFolderPopoverOpen, setIsNewFolderPopoverOpen] = useState(false);
    const [newFileName, setNewFileName] = useState('');
    const [isNewFilePopoverOpen, setIsNewFilePopoverOpen] = useState(false);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const folderInputRef = useRef<HTMLInputElement>(null);

    const directoryContents = useMemo(
        () => getDirectoryContents(currentPath, files, skillMdContent, emptyFolders),
        [currentPath, files, skillMdContent, emptyFolders]
    );

    const handleFileUpload = async (fileList: FileList | null, isDirectoryUpload = false) => {
        if (!fileList || readOnly) return;

        const newFiles: SkillFile[] = [];
        const foldersToRemove = new Set<string>();

        // Build a set of existing file paths for deduplication
        const existingPaths = new Set(files.map(f => (f.filePath.startsWith('/') ? f.filePath : '/' + f.filePath)));

        for (let i = 0; i < fileList.length; i++) {
            const file = fileList[i];
            const content = await file.text();

            let filePath: string;
            if (isDirectoryUpload && (file as any).webkitRelativePath) {
                // For directory uploads, use the relative path
                const relativePath = (file as any).webkitRelativePath as string;
                filePath = currentPath === '/' ? '/' + relativePath : currentPath + relativePath;
            } else {
                // Place new files in current directory
                filePath = currentPath === '/' ? '/' + file.name : currentPath + file.name;
            }

            // Skip if file already exists at this path
            if (existingPaths.has(filePath)) {
                continue;
            }

            // Check if this file's directory was an empty folder - mark for removal
            const fileDir = filePath.substring(0, filePath.lastIndexOf('/') + 1) || '/';
            emptyFolders.forEach(ef => {
                if (fileDir.startsWith(ef) || fileDir === ef) {
                    foldersToRemove.add(ef);
                }
            });

            newFiles.push({
                fileName: file.name,
                filePath,
                content,
            });

            // Add to existing paths to handle duplicates within the same upload batch
            existingPaths.add(filePath);
        }

        if (newFiles.length > 0) {
            onFilesChange([...files, ...newFiles]);
        }

        // Remove any empty folders that now have files
        if (foldersToRemove.size > 0 && onEmptyFoldersChange) {
            const updatedEmptyFolders = emptyFolders.filter(f => !foldersToRemove.has(f));
            onEmptyFoldersChange(updatedEmptyFolders);
        }

        // Reset file inputs to allow re-uploading the same file
        if (fileInputRef.current) {
            fileInputRef.current.value = '';
        }
        if (folderInputRef.current) {
            folderInputRef.current.value = '';
        }
    };

    const handleFolderUpload = async (fileList: FileList | null) => {
        await handleFileUpload(fileList, true);
    };

    const handleDragOver = (e: React.DragEvent) => {
        e.preventDefault();
        if (!readOnly) {
            setIsDragOver(true);
        }
    };

    const handleDragLeave = (e: React.DragEvent) => {
        e.preventDefault();
        setIsDragOver(false);
    };

    const handleDrop = (e: React.DragEvent) => {
        e.preventDefault();
        setIsDragOver(false);
        if (!readOnly) {
            handleFileUpload(e.dataTransfer.files);
        }
    };

    const handleRemoveFile = (item: FileSystemItem, e: React.MouseEvent) => {
        e.stopPropagation();
        if (item.isDefault || item.fileIndex === undefined || readOnly) return;

        const newFiles = files.filter((_, i) => i !== item.fileIndex);
        onFilesChange(newFiles);

        // If we deleted the selected file, reset to default
        if (selectedFile?.path === item.path) {
            onFileSelect({
                name: SKILL_MD_FILENAME,
                type: 'file',
                path: '/' + SKILL_MD_FILENAME,
                isDefault: true,
            });
        }
    };

    const handleRemoveFolder = (item: FileSystemItem, e: React.MouseEvent) => {
        e.stopPropagation();
        if (readOnly || !onEmptyFoldersChange) return;

        // Check if folder has any files
        const hasFiles = files.some(file => {
            const filePath = file.filePath.startsWith('/') ? file.filePath : '/' + file.filePath;
            return filePath.startsWith(item.path);
        });

        if (hasFiles) {
            // Folder is not empty - could show a message but for now just don't delete
            return;
        }

        // Check if it's an empty folder or contains nested empty folders
        const foldersToRemove = emptyFolders.filter(f => f === item.path || f.startsWith(item.path));
        if (foldersToRemove.length > 0) {
            const updatedEmptyFolders = emptyFolders.filter(f => !foldersToRemove.includes(f));
            onEmptyFoldersChange(updatedEmptyFolders);
        }
    };

    const handleCreateFolder = () => {
        if (!newFolderName.trim() || readOnly || !onEmptyFoldersChange) return;

        const folderPath = currentPath + newFolderName.trim() + '/';

        // Check if folder already exists
        const folderExists = directoryContents.some(item => item.type === 'folder' && item.path === folderPath);

        if (!folderExists) {
            onEmptyFoldersChange([...emptyFolders, folderPath]);
        }

        setNewFolderName('');
        setIsNewFolderPopoverOpen(false);
    };

    const handleCreateFile = () => {
        if (!newFileName.trim() || readOnly) return;

        const filePath = currentPath === '/' ? '/' + newFileName.trim() : currentPath + newFileName.trim();

        // Check if file already exists
        const fileExists = files.some(f => {
            const existingPath = f.filePath.startsWith('/') ? f.filePath : '/' + f.filePath;
            return existingPath === filePath;
        });

        if (!fileExists) {
            const newFile: SkillFile = {
                fileName: newFileName.trim(),
                filePath,
                content: '',
            };
            onFilesChange([...files, newFile]);
        }

        setNewFileName('');
        setIsNewFilePopoverOpen(false);
    };

    const isFolderEmpty = (item: FileSystemItem): boolean => {
        // Check if any files are in this folder
        const hasFiles = files.some(file => {
            const filePath = file.filePath.startsWith('/') ? file.filePath : '/' + file.filePath;
            return filePath.startsWith(item.path);
        });
        return !hasFiles;
    };

    const handleItemClick = (item: FileSystemItem) => {
        if (item.type === 'folder') {
            setCurrentPath(item.path);
        } else {
            onFileSelect(item);
        }
    };

    const handleNavigateUp = () => {
        const parentPath = getParentPath(currentPath);
        setCurrentPath(parentPath);
    };

    return (
        <div className={styles.container}>
            {/* File List Panel */}
            <div className={styles.fileListPanel}>
                <div className={styles.currentPathBar}>
                    <Text className={styles.pathText}>{currentPath}</Text>
                    {!readOnly && (
                        <div className={styles.pathBarActions}>
                            <Popover open={isNewFilePopoverOpen} onOpenChange={(_, data) => setIsNewFilePopoverOpen(data.open)}>
                                <PopoverTrigger disableButtonEnhancement>
                                    <Button
                                        appearance="subtle"
                                        icon={<DocumentAdd20Regular />}
                                        size="small"
                                        aria-label={intl.formatMessage(ExtendedAgentsGraphResources.newFile)}
                                    />
                                </PopoverTrigger>
                                <PopoverSurface className={styles.newFolderPopover}>
                                    <div className={styles.newFolderForm}>
                                        <Input
                                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.fileName)}
                                            value={newFileName}
                                            onChange={(_, data) => setNewFileName(data.value)}
                                            onKeyDown={e => {
                                                if (e.key === 'Enter') {
                                                    handleCreateFile();
                                                }
                                            }}
                                            size="small"
                                        />
                                        <Button appearance="primary" size="small" onClick={handleCreateFile} disabled={!newFileName.trim()}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.create)}
                                        </Button>
                                    </div>
                                </PopoverSurface>
                            </Popover>
                            {onEmptyFoldersChange && (
                                <Popover open={isNewFolderPopoverOpen} onOpenChange={(_, data) => setIsNewFolderPopoverOpen(data.open)}>
                                    <PopoverTrigger disableButtonEnhancement>
                                        <Button
                                            appearance="subtle"
                                            icon={<FolderAdd20Regular />}
                                            size="small"
                                            aria-label={intl.formatMessage(ExtendedAgentsGraphResources.newFolder)}
                                        />
                                    </PopoverTrigger>
                                    <PopoverSurface className={styles.newFolderPopover}>
                                        <div className={styles.newFolderForm}>
                                            <Input
                                                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.folderName)}
                                                value={newFolderName}
                                                onChange={(_, data) => setNewFolderName(data.value)}
                                                onKeyDown={e => {
                                                    if (e.key === 'Enter') {
                                                        handleCreateFolder();
                                                    }
                                                }}
                                                size="small"
                                            />
                                            <Button
                                                appearance="primary"
                                                size="small"
                                                onClick={handleCreateFolder}
                                                disabled={!newFolderName.trim()}
                                            >
                                                {intl.formatMessage(ExtendedAgentsGraphResources.create)}
                                            </Button>
                                        </div>
                                    </PopoverSurface>
                                </Popover>
                            )}
                        </div>
                    )}
                </div>

                <div className={styles.fileList}>
                    {/* Back navigation */}
                    {currentPath !== '/' && (
                        <div className={styles.backRow} onClick={handleNavigateUp}>
                            <div className={styles.fileNameCell}>
                                <ArrowLeft16Regular />
                                <Text className={styles.fileName}>..</Text>
                            </div>
                        </div>
                    )}

                    {/* Directory contents */}
                    {directoryContents.map(item => (
                        <div
                            key={item.path}
                            className={mergeClasses(
                                item.type === 'folder' ? styles.folderRow : styles.fileRow,
                                selectedFile?.path === item.path && styles.fileRowSelected
                            )}
                            onClick={() => handleItemClick(item)}
                        >
                            <div className={styles.fileNameCell}>
                                {item.type === 'folder' ? <Folder16Regular /> : <DocumentText16Regular />}
                                <Text className={styles.fileName}>{item.name}</Text>
                                {item.isDefault && (
                                    <Text className={styles.defaultFileBadge}>
                                        ({intl.formatMessage(ExtendedAgentsGraphResources.defaultFile)})
                                    </Text>
                                )}
                            </div>
                            {!item.isDefault && item.type === 'file' && !readOnly && (
                                <Button
                                    appearance="subtle"
                                    icon={<Delete20Regular />}
                                    size="small"
                                    onClick={e => handleRemoveFile(item, e)}
                                    aria-label={intl.formatMessage(SreAgentResources.delete)}
                                />
                            )}
                            {item.type === 'folder' && !readOnly && isFolderEmpty(item) && onEmptyFoldersChange && (
                                <Button
                                    appearance="subtle"
                                    icon={<Delete20Regular />}
                                    size="small"
                                    onClick={e => handleRemoveFolder(item, e)}
                                    aria-label={intl.formatMessage(SreAgentResources.delete)}
                                />
                            )}
                        </div>
                    ))}
                </div>

                {/* Drop zone */}
                {!readOnly && (
                    <div className={styles.dropZoneContainer}>
                        <div
                            className={mergeClasses(styles.dropZone, isDragOver && styles.dropZoneDragOver)}
                            onDragOver={handleDragOver}
                            onDragLeave={handleDragLeave}
                            onDrop={handleDrop}
                        >
                            <Text>
                                {intl.formatMessage(ExtendedAgentsGraphResources.dragFilesHere)}{' '}
                                <Link onClick={() => fileInputRef.current?.click()}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.browseForFiles)}
                                </Link>
                                {' · '}
                                <Link onClick={() => folderInputRef.current?.click()}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.uploadFolder)}
                                </Link>
                            </Text>
                        </div>
                        <input
                            ref={fileInputRef}
                            type="file"
                            onChange={e => handleFileUpload(e.target.files)}
                            className={styles.hiddenFileInput}
                            multiple
                        />
                        <input
                            ref={folderInputRef}
                            type="file"
                            onChange={e => handleFolderUpload(e.target.files)}
                            className={styles.hiddenFileInput}
                            /* @ts-expect-error webkitdirectory is not in the type definitions */
                            webkitdirectory=""
                            multiple
                        />
                    </div>
                )}
            </div>
        </div>
    );
};
