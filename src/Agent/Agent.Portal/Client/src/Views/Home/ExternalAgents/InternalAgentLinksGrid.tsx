import {
    Button,
    Card,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Field,
    Image,
    Input,
    Link,
    makeStyles,
    SearchBox,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    useTableFeatures,
    useTableSort,
} from '@fluentui/react-components';
import { Add16Regular, Delete16Regular } from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { CopyButton } from '../../../Common/Components/CopyButton';
import { ALLOWED_AGENT_DOMAIN_SUFFIXES } from '../../../Common/Constants/AllowedAgentDomains';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { useLocalStorage } from '../../../Common/Hooks/useLocalStorage';
import { usePersistentNavigate } from '../../../Common/Hooks/usePersistentNavigate';
import { safeCompare } from '../../../Common/Utilities/String';
import { PortalResources } from '../../../Strings/Resources';

// TODO: Known bug with link in this component not actually updating the view unless you change tabs or refresh the page.
// May just be in dev, but *every*thing was tried. It's not the link or routing or tab rendering or anything, JUST something
// about being in this component. Good luck.

interface InternalAgentLinkItem {
    /** Functions as the UID */
    uri: string;
    displayName: string;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        width: '100%',
        maxWidth: '1200px',
        flex: '1',
        minHeight: '0',
    },
    controlsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    actionButtons: {
        display: 'flex',
        gap: '12px',
    },
    controlsRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    leftControls: {
        display: 'flex',
        gap: '12px',
        alignItems: 'center',
    },
    searchBox: {
        width: '250px',
    },
    card: {
        padding: '20px',
        display: 'flex',
        flexDirection: 'column',
        flex: '1',
        minHeight: '0',
    },
    dataGrid: {
        flex: '1',
        overflowY: 'auto',
    },
    emptyState: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        padding: '20px',
    },
});

export const InternalAgentLinksGrid = () => {
    const intl = useIntl();
    const styles = useStyles();
    const navigate = usePersistentNavigate();

    const { value: internalAgentLinks, setValue: setInternalAgentLinks } = useLocalStorage<InternalAgentLinkItem[]>(
        'sreAgentLinks',
        [],
        TelemetrySource.HomeBrowseView
    );
    const [searchText, setSearchText] = useState('');
    const [selectedAgentLinkUris, setSelectedAgentLinkUris] = useState<Set<string>>(new Set());
    const [showCreateDialog, setShowCreateDialog] = useState<boolean>(false);
    const [showDeleteConfirmDialog, setShowDeleteConfirmDialog] = useState<boolean>(false);
    const [newAgentLinkDisplayName, setNewAgentLinkDisplayName] = useState<string>('');
    const [newAgentLinkUri, setNewAgentLinkUri] = useState<string>('');

    const selectedAgentLinks = useMemo(() => {
        return internalAgentLinks.filter(agent => selectedAgentLinkUris.has(agent.uri));
    }, [internalAgentLinks, selectedAgentLinkUris]);

    const columns: TableColumnDefinition<InternalAgentLinkItem>[] = useMemo(
        () => [
            createTableColumn<InternalAgentLinkItem>({
                columnId: 'agent',
                compare: (a, b) => safeCompare(a.displayName, b.displayName),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.agent)}</Text>,
                renderCell: item => (
                    <TableCellLayout
                        media={<Image src="SreAgent.svg" width={16} height={16} alt={intl.formatMessage(PortalResources.azureSreAgent)} />}
                    >
                        <Link
                            onClick={() => {
                                navigate(`/externalagents/${encodeURIComponent(item.displayName)}/${encodeURIComponent(item.uri)}`);
                            }}
                        >
                            {item.displayName}
                        </Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<InternalAgentLinkItem>({
                columnId: 'uri',
                compare: (a, b) => safeCompare(a.uri, b.uri),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.uri)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: '4px' }}>
                            <Link href={item.uri} target="_blank" rel="noopener noreferrer">
                                {item.uri}
                            </Link>
                            <CopyButton textToCopy={item.uri} />
                        </div>
                    </TableCellLayout>
                ),
            }),
        ],
        [intl, navigate]
    );

    const filteredItems = useMemo(() => {
        if (!searchText) return internalAgentLinks;

        const lowerSearch = searchText.toLowerCase();
        return internalAgentLinks.filter(
            item => item.displayName.toLowerCase().includes(lowerSearch) || item.uri.toLowerCase().includes(lowerSearch)
        );
    }, [internalAgentLinks, searchText]);

    const {
        getRows,
        sort: { getSortDirection, toggleColumnSort, sort },
    } = useTableFeatures(
        {
            columns,
            items: filteredItems,
        },
        [
            useTableSort({
                defaultSortState: { sortColumn: 'agent', sortDirection: 'ascending' },
            }),
        ]
    );

    const headerSortProps = (columnId: string | number) => ({
        onClick: (e: React.MouseEvent) => {
            toggleColumnSort(e, columnId);
        },
        sortDirection: getSortDirection(columnId),
    });

    const rows = sort(getRows());

    const onSelectionChange = useCallback((_: unknown, data: { selectedItems: Set<string | number> }) => {
        setSelectedAgentLinkUris(data.selectedItems as Set<string>);
    }, []);

    const newAgentUriErrorMessage = useMemo<string | undefined>(() => {
        const trimmedUri = newAgentLinkUri.trim();
        const httpsString = 'https://';
        if (!trimmedUri.startsWith(httpsString)) {
            return intl.formatMessage(PortalResources.uriMustStartWith, { value: httpsString });
        }

        if (!ALLOWED_AGENT_DOMAIN_SUFFIXES.some(suffix => trimmedUri.endsWith(suffix))) {
            return intl.formatMessage(PortalResources.uriMustEndWithAllowedDomain);
        }

        if (internalAgentLinks.some(agent => agent.uri === trimmedUri)) {
            return intl.formatMessage(PortalResources.uriNotUnique);
        }

        return undefined;
    }, [newAgentLinkUri, internalAgentLinks, intl]);

    const addAgentLink = useCallback(() => {
        const newAgentLinkItems = [...internalAgentLinks, { uri: newAgentLinkUri.trim(), displayName: newAgentLinkDisplayName }];

        setInternalAgentLinks(newAgentLinkItems);

        setShowCreateDialog(false);
        setNewAgentLinkDisplayName('');
        setNewAgentLinkUri('');
    }, [internalAgentLinks, newAgentLinkDisplayName, newAgentLinkUri, setInternalAgentLinks]);

    const deleteAgentLinks = useCallback(
        (selectedAgentLinks: InternalAgentLinkItem[]) => {
            const newAgentLinkItems = internalAgentLinks.filter(agent => !selectedAgentLinks.some(selected => selected.uri === agent.uri));

            setInternalAgentLinks(newAgentLinkItems);
        },
        [internalAgentLinks, setInternalAgentLinks]
    );

    return (
        <div className={styles.container}>
            <div className={styles.controlsContainer}>
                <div className={styles.actionButtons}>
                    <Button icon={<Add16Regular />} appearance="primary" onClick={() => setShowCreateDialog(true)}>
                        {intl.formatMessage(PortalResources.add)}
                    </Button>
                    <Button
                        icon={<Delete16Regular />}
                        onClick={() => setShowDeleteConfirmDialog(true)}
                        disabled={selectedAgentLinks.length === 0}
                    >
                        {intl.formatMessage(PortalResources.delete)}
                    </Button>
                </div>

                <div className={styles.controlsRow}>
                    <div className={styles.leftControls}>
                        <SearchBox
                            placeholder={intl.formatMessage(PortalResources.searchAgentLinks)}
                            className={styles.searchBox}
                            value={searchText}
                            onChange={(_, data) => setSearchText(data.value)}
                        />
                    </div>
                </div>
            </div>

            <Card className={styles.card}>
                <DataGrid
                    items={rows}
                    columns={columns}
                    sortable
                    getRowId={item => (item as any).item.uri}
                    className={styles.dataGrid}
                    style={{ flex: filteredItems.length === 0 ? 'unset' : 1 }}
                    selectionMode="multiselect"
                    selectedItems={selectedAgentLinkUris}
                    onSelectionChange={onSelectionChange}
                >
                    <DataGridHeader>
                        <DataGridRow
                            selectionCell={{
                                checkboxIndicator: { 'aria-label': intl.formatMessage(PortalResources.selectAllAgentLinks) },
                            }}
                        >
                            {({ renderHeaderCell, columnId }) => (
                                <DataGridHeaderCell {...headerSortProps(columnId)}>{renderHeaderCell()}</DataGridHeaderCell>
                            )}
                        </DataGridRow>
                    </DataGridHeader>
                    <DataGridBody<InternalAgentLinkItem>>
                        {({ item, rowId }) => (
                            <DataGridRow<InternalAgentLinkItem>
                                key={rowId}
                                selectionCell={{
                                    checkboxIndicator: {
                                        'aria-label': intl.formatMessage(PortalResources.selectAgentLink, {
                                            name: (item as any).item.displayName,
                                        }),
                                    },
                                }}
                            >
                                {({ renderCell }) => <DataGridCell>{renderCell((item as any).item)}</DataGridCell>}
                            </DataGridRow>
                        )}
                    </DataGridBody>
                </DataGrid>

                {filteredItems.length === 0 && (
                    <div className={styles.emptyState}>
                        <Text>{intl.formatMessage(PortalResources.noResultsFound)}</Text>
                    </div>
                )}
            </Card>

            <Dialog open={showCreateDialog} onOpenChange={(_e, data) => setShowCreateDialog(data.open)}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(PortalResources.addAgentLink)}</DialogTitle>
                        <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                            <Field label={intl.formatMessage(PortalResources.displayName)} required>
                                <Input
                                    value={newAgentLinkDisplayName}
                                    onChange={(_e, data) => setNewAgentLinkDisplayName(data.value)}
                                    aria-label={intl.formatMessage(PortalResources.displayName)}
                                    aria-required="true"
                                />
                            </Field>
                            <Field
                                label={intl.formatMessage(PortalResources.uri)}
                                validationMessage={newAgentUriErrorMessage}
                                validationState={newAgentUriErrorMessage ? 'error' : undefined}
                                required
                            >
                                <Input
                                    value={newAgentLinkUri}
                                    onChange={(_e, data) => setNewAgentLinkUri(data.value)}
                                    placeholder="https://my-agent.region.azuresre.ai"
                                    aria-label={intl.formatMessage(PortalResources.uri)}
                                    aria-required="true"
                                    aria-invalid={!!newAgentUriErrorMessage}
                                />
                            </Field>
                        </DialogContent>
                        <DialogActions>
                            <Button
                                appearance="primary"
                                onClick={() => addAgentLink()}
                                disabled={!!newAgentUriErrorMessage || !newAgentLinkDisplayName.trim() || !newAgentLinkUri.trim()}
                            >
                                {intl.formatMessage(PortalResources.add)}
                            </Button>
                            <Button appearance="secondary" onClick={() => setShowCreateDialog(false)}>
                                {intl.formatMessage(PortalResources.cancel)}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            <Dialog open={showDeleteConfirmDialog} onOpenChange={(_e, data) => setShowDeleteConfirmDialog(data.open)}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>
                            {selectedAgentLinks.length === 1
                                ? intl.formatMessage(PortalResources.deleteAgentLink)
                                : intl.formatMessage(PortalResources.deleteAgentLinks)}
                        </DialogTitle>
                        <DialogContent>
                            <Text>
                                {selectedAgentLinks.length === 1
                                    ? intl.formatMessage(PortalResources.confirmDeleteAgentLink, {
                                          name: selectedAgentLinks[0].displayName ?? '',
                                      })
                                    : intl.formatMessage(PortalResources.confirmDeleteAgentLinks)}
                            </Text>
                        </DialogContent>
                        <DialogActions>
                            <Button
                                appearance="primary"
                                onClick={() => {
                                    setShowDeleteConfirmDialog(false);
                                    deleteAgentLinks(selectedAgentLinks);
                                    setSelectedAgentLinkUris(new Set());
                                }}
                            >
                                {intl.formatMessage(PortalResources.yes)}
                            </Button>
                            <Button appearance="secondary" onClick={() => setShowDeleteConfirmDialog(false)}>
                                {intl.formatMessage(PortalResources.no)}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </div>
    );
};

export default InternalAgentLinksGrid;
