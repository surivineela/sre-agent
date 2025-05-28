import React, { useEffect, useMemo } from 'react'; // Removed useState as React.useState is used
import { Stack, Text, PrimaryButton, MessageBar, MessageBarType, Nav, INavLinkGroup, INavStyles, INavLink, Spinner, SpinnerSize, StackItem, mergeStyles } from '@fluentui/react';
import MonacoEditor, { Monaco } from '@monaco-editor/react';
import {
    listAllContainers,
    getAllDocumentIds,
    getDocumentById,
    upsertDocument
} from '../Services/Request';
import { useMutation, useQuery } from '@tanstack/react-query';
import LoadingErrorWrapper from './LoadingErrorWrapper';
import { useSharedUrlParams } from '../Context/UrlParamsProvider'; // Import the shared hook

const CosmosDbEditor: React.FC = () => {
    const [selectedContainer, setSelectedContainer] = React.useState<string | undefined>();
    const [selectedDocumentId, setSelectedDocumentId] = React.useState<string | undefined>();
    const [documentContent, setDocumentContent] = React.useState<string>('');
    const [validationError, setValidationError] = React.useState<string | null>(null);
    const urlParams = useSharedUrlParams(); // Use the shared hook

    const handleDocumentContentChange = (newContent: string | undefined) => {
        setDocumentContent(newContent || '');
    };

    const navWrapperClass = mergeStyles({
        selectors: {
            '.ms-FocusZone': {
                height: '100%',
            },
        },
    });

    const navStyles: Partial<INavStyles> = {
        root: {
            width: 250,
            height: '100%',
            overflowY: 'hidden',
        },
        groupContent: {
            marginBottom: "10px",
            overflowY: 'auto',
            height: 'calc(100% - 70px)',
            minHeight: '200px',
        },
        link: {
            whiteSpace: "normal",
            lineHeight: "normal",
            height: "auto",
            minHeight: "44px",
            padding: "0 0 0 5px",
        },
        chevronButton: {
            whiteSpace: "normal",
            lineHeight: "normal",
            height: "auto",
            margin: "0px",
            fontSize: "15px",
            fontWeight: 600,
            paddingBottom: "8px",
            display: "flex",
            alignItems: "center",
        },
        chevronIcon: {
            fontSize: "10px",
            fontWeight: 600,
        },
        compositeLink: {
            marginTop: "3px",
            marginBottom: "3px",
            lineHeight: "normal",
        },
        group: {
            height: '100%',
        }
    };

    const {
        data: containers = [],
        // isLoading: isListContainersLoading, // Unused
        status: listContainersStatus,
        error: listContainersError
    } = useQuery({
        queryKey: ['listAllContainers'],
        queryFn: async () => {
            const res = await listAllContainers();
            return res;
        }
    });

    const containerNavLinks = useMemo((): INavLinkGroup[] => {
        if (!containers || containers.length === 0) return [];
        const links: INavLink[] = containers.map(containerName => ({
            name: containerName,
            url: '', // Required, but not used for navigation here
            key: containerName,
            onClick: () => handleContainerChange(containerName),
        }));
        return [{ name: "Containers", links, isExpanded: true, collapseAriaLabel: "Collapse Containers", expandAriaLabel: "Expand Containers" }];
    }, [containers]);

    const {
        data: documentIds = [],
        status: getAllDocumentIdsStatus,
        error: getAllDocumentIdsError,
        refetch: refreshDocumentIds
    } = useQuery({
        queryKey: ['getAllDocumentIds', selectedContainer],
        queryFn: async () => {
            if (!selectedContainer) return [];
            const res = await getAllDocumentIds(selectedContainer);
            return res;
        },
        enabled: !!selectedContainer, // Ensure this only runs when a container is selected
    });

    const documentNavLinks = useMemo((): INavLinkGroup[] => {
        if (!documentIds || documentIds.length === 0) return [];
        const links: INavLink[] = documentIds.map(docId => ({
            name: docId,
            url: '',
            key: docId,
            onClick: () => handleDocumentIdChange(docId),
        }));
        return [{ name: "Documents", links, isExpanded: true, collapseAriaLabel: "Collapse Documents", expandAriaLabel: "Expand Documents" }];
    }, [documentIds]);

    const {
        mutateAsync: getDocumentContentAsync,
        // reset: resetGetDocumentByIdStatus, // Unused
        isPending: isGetDocumentByIdLoading,
    } = useMutation({
        mutationKey: ['getDocumentById'],
        mutationFn: async (props: { selectedContainer: string, selectedDocumentId: string }) => {
            const res = await getDocumentById(props.selectedContainer, props.selectedDocumentId);
            return JSON.stringify(res, null, 2);
        },
    });

    const {
        mutateAsync: upsertDocumentAsync,
        error: upsertDocumentError,
        // reset: resetUpsertDocumentStatus, // Unused
        isPending: isUpsertDocumentLoading,
    } = useMutation({
        mutationKey: ['upsertDocument'],
        mutationFn: async (props: { selectedContainer: string, documentContent: string }) => {
            await upsertDocument(props.selectedContainer, props.documentContent);
        },
        onSuccess: () => {
            alert('Document saved successfully!');
        }
    })

    useEffect(() => {
        setSelectedDocumentId(undefined); // Reset selected document ID
        if (selectedContainer) {
            refreshDocumentIds(); // Refresh document IDs when container changes
        }
    }, [selectedContainer]);

    useEffect(() => {
        setDocumentContent(''); // Clear document content
    }, [selectedContainer, selectedDocumentId]);

    const handleContainerChange = async (containerName: string) => {
        setSelectedContainer(containerName);
        setSelectedDocumentId(undefined); // Reset document ID when container changes
        setDocumentContent(''); // Clear document content
        // refreshDocumentIds will be called automatically due to the enabled flag in its useQuery
    };

    const handleDocumentIdChange = async (documentId: string) => {
        if (selectedContainer) {
            setSelectedDocumentId(documentId);
            const res = await getDocumentContentAsync({ selectedContainer: selectedContainer, selectedDocumentId: documentId });
            setDocumentContent(res);
        }
    };
    const handleSaveDocument = async () => {
        setValidationError(null); // Reset validation error
        if (selectedContainer && documentContent) {
            // Try to infer ID from content if selectedDocumentId is not set (for new documents)
            let docIdToSave = selectedDocumentId;
            if (!docIdToSave) {
                try {
                    const parsedContent = JSON.parse(documentContent);
                    if (parsedContent.id) {
                        docIdToSave = parsedContent.id;
                    }
                } catch (e) {
                    setValidationError('Document content is not valid JSON. Cannot infer ID for new document.');
                    return;
                }
            }

            if (!docIdToSave) {
                setValidationError('Document ID is missing. Please select a document or ensure the JSON content has an \'id\' property.');
                return;
            }
        }
        await upsertDocumentAsync({ selectedContainer, documentContent });
    }

    return (
        <Stack horizontal tokens={{ childrenGap: 20 }} styles={{ root: { padding: 20, paddingBottom: 0, alignItems: 'flex-start', height: 'calc(100% - 20px)', overflow: 'hidden' } }}>
            <Stack horizontal tokens={{ childrenGap: 10 }} styles={{ root: { width: 'auto', height: '100%', overflow: 'hidden' } }}>
                <Stack tokens={{ childrenGap: 10 }} styles={{ root: { width: '250px', height: '100%', display: 'flex', flexDirection: 'column' } }}>
                    <Text variant="xLarge">CosmosDB Editor</Text>
                    {Object.keys(urlParams).length > 0 && (
                        <Stack>
                            <Text variant="mediumPlus">URL Parameters:</Text>
                            {Object.entries(urlParams).map(([key, value]) => (
                                <Text key={key}>{`${key}: ${value}`}</Text>
                            ))}
                        </Stack>
                    )}
                    <Stack className={navWrapperClass} styles={{ root: { flexGrow: 1, overflow: 'hidden' } }}>
                        <LoadingErrorWrapper status={listContainersStatus} error={listContainersError} renderLoading="Loading CosmosDB Containers..." renderError="Failed to load containers.">
                            <Nav groups={containerNavLinks} styles={navStyles} selectedKey={selectedContainer} />
                        </LoadingErrorWrapper>
                    </Stack>
                </Stack>
                {selectedContainer &&
                    <Stack tokens={{ childrenGap: 10 }} styles={{ root: { width: '250px', height: '100%', display: 'flex', flexDirection: 'column', marginTop: '35px' } }}>
                        <Stack className={navWrapperClass} styles={{ root: { flexGrow: 1, overflow: 'hidden' } }}>
                            <LoadingErrorWrapper status={getAllDocumentIdsStatus} error={getAllDocumentIdsError} renderLoading="Loading Document IDs..." renderError="Failed to load document IDs.">
                                <Nav groups={documentNavLinks} styles={navStyles} selectedKey={selectedDocumentId} />
                            </LoadingErrorWrapper>
                        </Stack>
                    </Stack>
                }
            </Stack>

            <Stack tokens={{ childrenGap: 10 }} styles={{ root: { flexGrow: 1, height: '100%', display: 'flex', flexDirection: 'column' } }}>
                {(validationError || upsertDocumentError) &&
                    <MessageBar messageBarType={MessageBarType.error} isMultiline>
                        {validationError ? validationError : "Failed to save document"}
                    </MessageBar>
                }
                <StackItem grow styles={{ root: { position: 'relative', border: '1px solid #ccc', flexGrow: 1 } }}>
                    {selectedContainer && selectedDocumentId ? (
                        <CosmosDbMonacoEditorComponent
                            documentContent={documentContent}
                            onChange={handleDocumentContentChange}
                            isLoading={isGetDocumentByIdLoading}
                        />
                    ) : selectedContainer ? (
                        <Stack verticalAlign="center" horizontalAlign="center" styles={{ root: { height: '100%' } }}>
                            <Text styles={{ root: { textAlign: 'center' } }}>Select a document to view or edit its content.</Text>
                        </Stack>
                    ) : (
                        <Stack verticalAlign="center" horizontalAlign="center" styles={{ root: { height: '100%' } }}>
                            <Text styles={{ root: { textAlign: 'center' } }}>Select a container to get started.</Text>
                        </Stack>
                    )}
                </StackItem>
                {selectedContainer && selectedDocumentId && !isGetDocumentByIdLoading && (
                    <PrimaryButton
                        onClick={handleSaveDocument}
                        disabled={isUpsertDocumentLoading || !documentContent}
                        text="Save Document"
                        styles={{ root: { marginTop: 10 } }}
                    />
                )}
            </Stack>
        </Stack>
    );
};

const CosmosDbMonacoEditorComponent = (props: {
    documentContent: string;
    onChange: (value: string | undefined) => void;
    isLoading: boolean;
}) => {

    const handleEditorChange = (value: string | undefined) => {
        props.onChange(value);
    };

    const handleEditorDidMount = (editor: any, monaco: Monaco) => {
        monaco.languages.json.jsonDefaults.setDiagnosticsOptions({
            validate: true,
            schemas: [], // No specific schema by default for generic CosmosDB docs
        });
        editor.focus();
    };

    if (props.isLoading) {
        return (
            <Stack verticalAlign="center" horizontalAlign="center" styles={{ root: { height: '100%' } }}>
                <Spinner label="Loading document content..." size={SpinnerSize.large} />
            </Stack>
        );
    }

    return (
        <MonacoEditor
            height="100%"
            language="json"
            theme="vs-dark"
            value={props.documentContent}
            onChange={handleEditorChange}
            onMount={handleEditorDidMount}
            options={{
                automaticLayout: true,
                formatOnType: true,
                formatOnPaste: true,
                fontSize: 15,
                wordWrap: "on",
                minimap: { enabled: false }
            }}
        />
    );
};

export default CosmosDbEditor;
