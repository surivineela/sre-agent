import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Dropdown,
    Input,
    Label,
    makeStyles,
    MessageBar,
    MessageBarBody,
    Option,
    Skeleton,
    SkeletonItem,
    Spinner,
    tokens,
} from '@fluentui/react-components';
import MonacoEditor from '@monaco-editor/react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';
import { ResourceClient } from '../../Clients/ResourceClient';
import { CopyButton } from '../../Components/CopyButton';
import { TelemetrySource } from '../../Constants/Telemetry';
import { useUserPreferences } from '../../Contexts/UserPreferencesContext';
import { ArmObj } from '../../Contracts/Arm';
import { useResourceApiVersions } from '../../Hooks/useResourceApiVersions';

const useStyles = makeStyles({
    fieldGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    inputWithCopy: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    input: {
        flex: 1,
    },
    dropdown: {
        minWidth: '250px',
    },
    jsonContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        flex: 1,
        minHeight: 0,
    },
    monacoContainer: {
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
    dialogSurface: {
        width: '900px',
        maxWidth: '90vw',
        height: '80vh',
        maxHeight: '800px',
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
    errorBar: {
        marginBottom: tokens.spacingVerticalS,
    },
});

interface ViewResourceJsonDialogProps {
    open: boolean;
    resourceId: string;
    telemetrySource: TelemetrySource;
    onClose: () => void;
}

export const ViewResourceJsonDialog = ({ open, resourceId, telemetrySource, onClose }: ViewResourceJsonDialogProps) => {
    const intl = useIntl();
    const styles = useStyles();
    const { resolvedTheme } = useUserPreferences();

    const [selectedApiVersion, setSelectedApiVersion] = useState<string>('');
    const [resourceData, setResourceData] = useState<ArmObj<unknown> | null>(null);
    const [isLoadingResource, setIsLoadingResource] = useState(false);
    const [resourceError, setResourceError] = useState<string | null>(null);
    const fetchCallIdRef = useRef(0);

    const {
        apiVersions,
        latestVersion,
        isLoading: isLoadingVersions,
        error: versionsError,
    } = useResourceApiVersions(resourceId, telemetrySource);

    // Set initial API version when versions are loaded
    useEffect(() => {
        if (latestVersion && !selectedApiVersion) {
            setSelectedApiVersion(latestVersion);
        }
    }, [latestVersion, selectedApiVersion]);

    // Reset state when dialog opens with a new resource
    useEffect(() => {
        if (open) {
            setResourceData(null);
            setResourceError(null);
            setSelectedApiVersion('');
        }
    }, [open, resourceId]);

    const fetchResource = useCallback(
        async (apiVersion: string) => {
            if (!resourceId || !apiVersion) {
                return;
            }

            const currentCallId = ++fetchCallIdRef.current;
            setIsLoadingResource(true);
            setResourceError(null);

            const resourceClient = ResourceClient.getInstance(telemetrySource);
            const response = await resourceClient.getResource(resourceId, apiVersion);

            // Check if this is still the latest call
            if (currentCallId !== fetchCallIdRef.current) {
                return;
            }

            if (response.isSuccessful && response.content) {
                setResourceData(response.content);
                setResourceError(null);
            } else {
                setResourceError(response.error ?? 'Failed to fetch resource');
                setResourceData(null);
            }

            setIsLoadingResource(false);
        },
        [resourceId, telemetrySource]
    );

    // Fetch resource when API version changes
    useEffect(() => {
        if (open && selectedApiVersion) {
            fetchResource(selectedApiVersion);
        }
    }, [open, selectedApiVersion, fetchResource]);

    const handleApiVersionChange = useCallback((_: unknown, data: { optionValue?: string }) => {
        if (data.optionValue) {
            setSelectedApiVersion(data.optionValue);
        }
    }, []);

    const jsonContent = useMemo(() => {
        if (!resourceData) return '';
        return JSON.stringify(resourceData, null, 2);
    }, [resourceData]);

    const formatVersionLabel = useCallback((version: string, isLatest: boolean) => {
        return isLatest ? `${version} (latest)` : version;
    }, []);

    return (
        <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle>{intl.formatMessage(PortalResources.viewJson)}</DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        {versionsError && (
                            <MessageBar intent="error" className={styles.errorBar}>
                                <MessageBarBody>{versionsError}</MessageBarBody>
                            </MessageBar>
                        )}

                        {resourceError && (
                            <MessageBar intent="error" className={styles.errorBar}>
                                <MessageBarBody>{resourceError}</MessageBarBody>
                            </MessageBar>
                        )}

                        <div className={styles.fieldGroup}>
                            <Label>{intl.formatMessage(PortalResources.resourceId)}</Label>
                            <div className={styles.inputWithCopy}>
                                <Input className={styles.input} value={resourceId} readOnly />
                                <CopyButton textToCopy={resourceId} />
                            </div>
                        </div>

                        <div className={styles.fieldGroup}>
                            <Label>{intl.formatMessage(PortalResources.apiVersion)}</Label>
                            <div className={styles.inputWithCopy}>
                                {isLoadingVersions ? (
                                    <Skeleton className={styles.dropdown}>
                                        <SkeletonItem />
                                    </Skeleton>
                                ) : (
                                    <Dropdown
                                        className={styles.dropdown}
                                        value={
                                            selectedApiVersion
                                                ? formatVersionLabel(selectedApiVersion, selectedApiVersion === latestVersion)
                                                : ''
                                        }
                                        selectedOptions={selectedApiVersion ? [selectedApiVersion] : []}
                                        onOptionSelect={handleApiVersionChange}
                                        disabled={apiVersions.length === 0}
                                    >
                                        {apiVersions.map(version => (
                                            <Option key={version} value={version}>
                                                {formatVersionLabel(version, version === latestVersion)}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                )}
                                <CopyButton textToCopy={selectedApiVersion} />
                            </div>
                        </div>

                        <div className={styles.jsonContainer}>
                            <div className={styles.monacoContainer}>
                                {isLoadingResource && (
                                    <div className={styles.loadingOverlay}>
                                        <Spinner size="medium" label={intl.formatMessage(PortalResources.loading)} />
                                    </div>
                                )}
                                <MonacoEditor
                                    value={jsonContent}
                                    language="json"
                                    theme={resolvedTheme === 'dark' ? 'vs-dark' : 'vs'}
                                    options={{
                                        readOnly: true,
                                        automaticLayout: true,
                                        minimap: { enabled: false },
                                        scrollBeyondLastLine: false,
                                        fontSize: 13,
                                        wordWrap: 'on',
                                        lineNumbers: 'on',
                                        folding: true,
                                        renderLineHighlight: 'none',
                                    }}
                                    height="100%"
                                    width="100%"
                                />
                            </div>
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={onClose}>
                            {intl.formatMessage(PortalResources.close)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
