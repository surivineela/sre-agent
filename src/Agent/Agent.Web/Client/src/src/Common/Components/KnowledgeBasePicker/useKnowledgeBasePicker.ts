import { RefObject, useCallback, useContext, useMemo, useRef, useState } from 'react';
import { SreAgentContext } from '../../../Space/Contracts/Context';

export interface KnowledgeSource {
    id: string;
    type: 'repository' | 'file' | 'webpage';
    name: string;
    url?: string;
    lastModified?: string;
}

export interface UseKnowledgeBasePickerResult {
    // Dialog states
    isFileDialogOpen: boolean;
    setIsFileDialogOpen: (open: boolean) => void;
    isWebPageDialogOpen: boolean;
    setIsWebPageDialogOpen: (open: boolean) => void;
    isRepositoryDialogOpen: boolean;
    setIsRepositoryDialogOpen: (open: boolean) => void;

    // File upload
    selectedFiles: File[];
    isDragOver: boolean;
    isUploading: boolean;
    fileInputRef: RefObject<HTMLInputElement>;
    handleFileDragOver: (event: React.DragEvent<HTMLDivElement>) => void;
    handleFileDragLeave: (event: React.DragEvent<HTMLDivElement>) => void;
    handleFileDrop: (event: React.DragEvent<HTMLDivElement>) => void;
    handleBrowseClick: () => void;
    handleFileInputChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
    handleRemoveFile: (index: number) => void;
    handleFileUpload: () => void;
    handleFileDialogCancel: () => void;
    handleCreateFile: () => void;

    // Web page
    handleAddWebPage: (url: string, name: string, description?: string) => void;
    handleWebPageDialogCancel: () => void;

    // Repository
    handleRepositorySuccess: () => void;
    agentName: string | undefined;
    agentLocation: string | undefined;

    // Knowledge sources state
    knowledgeSources: KnowledgeSource[];
    setKnowledgeSources: (sources: KnowledgeSource[]) => void;
}

interface UseKnowledgeBasePickerProps {
    initialSources?: KnowledgeSource[];
    onSourcesChange?: (sources: KnowledgeSource[]) => void;
}

export const useKnowledgeBasePicker = (props?: UseKnowledgeBasePickerProps): UseKnowledgeBasePickerResult => {
    const { initialSources = [], onSourcesChange } = props ?? {};

    const { agentObj } = useContext(SreAgentContext);

    const [isFileDialogOpen, setIsFileDialogOpen] = useState(false);
    const [isWebPageDialogOpen, setIsWebPageDialogOpen] = useState(false);
    const [isRepositoryDialogOpen, setIsRepositoryDialogOpen] = useState(false);

    const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
    const [isDragOver, setIsDragOver] = useState(false);
    const [isUploading] = useState(false);
    const fileInputRef = useRef<HTMLInputElement>(null);

    const [knowledgeSources, setKnowledgeSourcesState] = useState<KnowledgeSource[]>(initialSources);

    const agentName = useMemo(() => agentObj?.name, [agentObj]);
    const agentLocation = useMemo(() => agentObj?.location, [agentObj]);

    const setKnowledgeSources = useCallback(
        (sources: KnowledgeSource[]) => {
            setKnowledgeSourcesState(sources);
            onSourcesChange?.(sources);
        },
        [onSourcesChange]
    );

    const handleFileDragOver = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragOver(true);
    }, []);

    const handleFileDragLeave = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragOver(false);
    }, []);

    const handleFileDrop = useCallback((event: React.DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragOver(false);
        const files = Array.from(event.dataTransfer.files);
        setSelectedFiles(prev => [...prev, ...files]);
    }, []);

    const handleBrowseClick = useCallback(() => {
        fileInputRef.current?.click();
    }, []);

    const handleFileInputChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        const files = event.target.files ? Array.from(event.target.files) : [];
        setSelectedFiles(prev => [...prev, ...files]);
    }, []);

    const handleRemoveFile = useCallback((index: number) => {
        setSelectedFiles(prev => prev.filter((_, i) => i !== index));
    }, []);

    const handleFileUpload = useCallback(() => {
        const newSources: KnowledgeSource[] = selectedFiles.map(file => ({
            id: `file-${Date.now()}-${file.name}`,
            type: 'file',
            name: file.name,
            lastModified: new Date().toISOString(),
        }));

        setKnowledgeSources([...knowledgeSources, ...newSources]);
        setSelectedFiles([]);
        setIsFileDialogOpen(false);
    }, [selectedFiles, knowledgeSources, setKnowledgeSources]);

    const handleFileDialogCancel = useCallback(() => {
        setSelectedFiles([]);
        setIsFileDialogOpen(false);
    }, []);

    const handleCreateFile = useCallback(() => {
        setIsFileDialogOpen(false);
    }, []);

    const handleAddWebPage = useCallback(
        (url: string, name: string) => {
            const newSource: KnowledgeSource = {
                id: `webpage-${Date.now()}-${name}`,
                type: 'webpage',
                name,
                url,
                lastModified: new Date().toISOString(),
            };

            setKnowledgeSources([...knowledgeSources, newSource]);
            setIsWebPageDialogOpen(false);
        },
        [knowledgeSources, setKnowledgeSources]
    );

    const handleWebPageDialogCancel = useCallback(() => {
        setIsWebPageDialogOpen(false);
    }, []);

    const handleRepositorySuccess = useCallback(() => {
        setIsRepositoryDialogOpen(false);
    }, []);

    return {
        // Dialog states
        isFileDialogOpen,
        setIsFileDialogOpen,
        isWebPageDialogOpen,
        setIsWebPageDialogOpen,
        isRepositoryDialogOpen,
        setIsRepositoryDialogOpen,

        // File upload
        selectedFiles,
        isDragOver,
        isUploading,
        fileInputRef,
        handleFileDragOver,
        handleFileDragLeave,
        handleFileDrop,
        handleBrowseClick,
        handleFileInputChange,
        handleRemoveFile,
        handleFileUpload,
        handleFileDialogCancel,
        handleCreateFile,

        // Web page
        handleAddWebPage,
        handleWebPageDialogCancel,

        // Repository
        handleRepositorySuccess,
        agentName,
        agentLocation,

        // Knowledge sources state
        knowledgeSources,
        setKnowledgeSources,
    };
};
