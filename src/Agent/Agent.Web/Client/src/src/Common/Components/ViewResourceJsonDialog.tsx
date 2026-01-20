import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Dropdown,
    Label,
    Option,
    Skeleton,
    SkeletonItem,
    Spinner,
    Text,
    makeStyles,
    tokens,
} from '@fluentui/react-components';
import MonacoEditor from '@monaco-editor/react';
import { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { ThemeMode } from '../AzPortalProxy/Models/ITheme';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import MakeArmCall from '../Clients/ArmClient';
import ResourceProviderClient from '../Clients/ResourceProviderClient';
import { ArmResourceDescriptor } from '../Helpers/ResourceDescriptors';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import CopyButton from './CopyButton';

const useStyles = makeStyles({
    dialogSurface: {
        maxWidth: '900px',
        width: '900px',
        height: '80vh',
        maxHeight: '80vh',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        flex: 1,
        minHeight: 0,
        overflow: 'hidden',
    },
    apiVersionContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    dropdown: {
        minWidth: '250px',
    },
    editorContainer: {
        flex: 1,
        minHeight: '400px',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium,
        overflow: 'hidden',
        position: 'relative',
    },
    loadingOverlay: {
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        opacity: 0.8,
        zIndex: 1,
    },
    errorContainer: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        height: '400px',
        color: tokens.colorPaletteRedForeground1,
    },
    dialogActions: {
        justifyContent: 'flex-end',
    },
});

interface ViewResourceJsonDialogProps {
    resourceId: string;
    isOpen: boolean;
    onClose: () => void;
}

// Cache for provider API versions to avoid repeated calls
const apiVersionsCache = new Map<string, string[]>();

export const ViewResourceJsonDialog: FC<ViewResourceJsonDialogProps> = ({ resourceId, isOpen, onClose }) => {
    const styles = useStyles();
    const intl = useIntl();
    const { theme } = useContext(EnvironmentContext);

    const [apiVersions, setApiVersions] = useState<string[]>([]);
    const [isLoadingVersions, setIsLoadingVersions] = useState(true);
    const [versionsError, setVersionsError] = useState<string | null>(null);

    const [selectedApiVersion, setSelectedApiVersion] = useState<string>('');
    const [resourceJson, setResourceJson] = useState<string>('');
    const [isLoadingResource, setIsLoadingResource] = useState(false);
    const [resourceError, setResourceError] = useState<string | null>(null);

    const fetchCallIdRef = useRef(0);

    const isDarkTheme = theme?.mode === ThemeMode.Dark || theme?.name === 'dark';
    const editorTheme = isDarkTheme ? 'vs-dark' : 'vs-light';

    // Parse resource ID to get subscription and provider
    const parsedResource = useMemo(() => {
        try {
            const descriptor = new ArmResourceDescriptor(resourceId);
            // Find provider segment - look for "providers" in the parts
            const providersIndex = descriptor.parts.findIndex(p => p.toLowerCase() === 'providers');
            if (providersIndex === -1 || providersIndex + 1 >= descriptor.parts.length) {
                return null;
            }
            const provider = descriptor.parts[providersIndex + 1]; // e.g., "Microsoft.App"
            const resourceType = descriptor.parts[providersIndex + 2]; // e.g., "agents"
            return {
                subscription: descriptor.subscription,
                provider,
                resourceType,
            };
        } catch {
            return null;
        }
    }, [resourceId]);

    const fetchApiVersions = useCallback(async () => {
        if (!parsedResource) {
            setVersionsError('Invalid resource ID');
            setIsLoadingVersions(false);
            return;
        }

        const cacheKey = `${parsedResource.provider}/${parsedResource.resourceType}`;
        const cached = apiVersionsCache.get(cacheKey);
        if (cached) {
            setApiVersions(cached);
            setSelectedApiVersion(cached[0] || '');
            setIsLoadingVersions(false);
            return;
        }

        setIsLoadingVersions(true);
        setVersionsError(null);

        const response = await ResourceProviderClient.getProvider(parsedResource.subscription, parsedResource.provider);

        if (!response.metadata.success || !response.data) {
            setVersionsError('Failed to fetch API versions');
            setIsLoadingVersions(false);
            return;
        }

        const matchingType = response.data.resourceTypes.find(
            (rt: { resourceType: string; apiVersions: string[] }) =>
                rt.resourceType.toLowerCase() === parsedResource.resourceType.toLowerCase()
        );

        if (!matchingType) {
            setVersionsError(`Resource type "${parsedResource.resourceType}" not found`);
            setIsLoadingVersions(false);
            return;
        }

        // Sort versions newest first
        const sortedVersions = [...matchingType.apiVersions].sort((a, b) => b.localeCompare(a));
        apiVersionsCache.set(cacheKey, sortedVersions);

        setApiVersions(sortedVersions);
        setSelectedApiVersion(sortedVersions[0] || '');
        setIsLoadingVersions(false);
    }, [parsedResource]);

    const fetchResource = useCallback(async () => {
        if (!resourceId || !selectedApiVersion) return;

        const currentCallId = ++fetchCallIdRef.current;
        setIsLoadingResource(true);
        setResourceError(null);

        const response = await MakeArmCall<unknown>({
            resourceId,
            commandName: 'getResourceJson',
            method: 'GET',
            apiVersion: selectedApiVersion,
        });

        if (currentCallId !== fetchCallIdRef.current) return;

        if (response.metadata.success && response.data) {
            setResourceJson(JSON.stringify(response.data, null, 2));
            setResourceError(null);
        } else {
            const errorMessage = response.metadata.error?.message ?? intl.formatMessage(SreAgentResources.failedToLoadResource);
            setResourceError(errorMessage);
            setResourceJson('');
        }

        setIsLoadingResource(false);
    }, [resourceId, selectedApiVersion, intl]);

    // Reset state when dialog opens
    useEffect(() => {
        if (isOpen) {
            setResourceJson('');
            setResourceError(null);
            setSelectedApiVersion('');
            fetchApiVersions();
        }
    }, [isOpen, resourceId, fetchApiVersions]);

    // Fetch resource when API version changes
    useEffect(() => {
        if (isOpen && selectedApiVersion) {
            fetchResource();
        }
    }, [isOpen, selectedApiVersion, fetchResource]);

    const handleApiVersionChange = useCallback((_: unknown, data: { optionValue?: string }) => {
        if (data.optionValue) {
            setSelectedApiVersion(data.optionValue);
        }
    }, []);

    const latestVersion = apiVersions[0] || '';

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && onClose()}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle>{intl.formatMessage(SreAgentResources.viewJson)}</DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <div className={styles.apiVersionContainer}>
                            <Label>{intl.formatMessage(SreAgentResources.apiVersion)}</Label>
                            {isLoadingVersions ? (
                                <Skeleton>
                                    <SkeletonItem style={{ width: 250 }} />
                                </Skeleton>
                            ) : versionsError ? (
                                <Text>{versionsError}</Text>
                            ) : (
                                <Dropdown
                                    className={styles.dropdown}
                                    value={
                                        selectedApiVersion === latestVersion
                                            ? `${selectedApiVersion} ${intl.formatMessage(SreAgentResources.latestVersionSuffix)}`
                                            : selectedApiVersion
                                    }
                                    onOptionSelect={handleApiVersionChange}
                                >
                                    {apiVersions.map(version => {
                                        const label =
                                            version === latestVersion
                                                ? `${version} ${intl.formatMessage(SreAgentResources.latestVersionSuffix)}`
                                                : version;
                                        return (
                                            <Option key={version} value={version} text={label}>
                                                {label}
                                            </Option>
                                        );
                                    })}
                                </Dropdown>
                            )}
                        </div>
                        {resourceError ? (
                            <div className={styles.errorContainer}>
                                <Text>{resourceError}</Text>
                            </div>
                        ) : (
                            <div className={styles.editorContainer}>
                                {isLoadingResource && (
                                    <div className={styles.loadingOverlay}>
                                        <Spinner label={intl.formatMessage(SreAgentResources.loadingResource)} />
                                    </div>
                                )}
                                <MonacoEditor
                                    height="100%"
                                    language="json"
                                    theme={editorTheme}
                                    value={resourceJson || ''}
                                    options={{
                                        readOnly: true,
                                        minimap: { enabled: false },
                                        scrollBeyondLastLine: false,
                                        automaticLayout: true,
                                        folding: true,
                                        lineNumbers: 'on',
                                        wordWrap: 'on',
                                    }}
                                />
                            </div>
                        )}
                    </DialogContent>
                    <DialogActions className={styles.dialogActions}>
                        {resourceJson && <CopyButton textToCopy={resourceJson} buttonAppearance="secondary" showCopyText />}
                        <Button appearance="primary" onClick={onClose}>
                            {intl.formatMessage(SreAgentResources.close)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default ViewResourceJsonDialog;
