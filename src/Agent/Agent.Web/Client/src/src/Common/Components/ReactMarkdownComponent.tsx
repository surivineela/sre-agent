import {
    createTableColumn,
    Link,
    makeStyles,
    Subtitle1,
    Subtitle2,
    Table,
    TableBody,
    TableCell,
    TableCellLayout,
    TableColumnDefinition,
    TableColumnId,
    TableColumnSizingOptions,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    Title3,
    tokens,
    useTableColumnSizing_unstable,
    useTableFeatures,
    useTableSort,
} from '@fluentui/react-components';
import React, { memo, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { getAgentHeaders } from '../Helpers/headers';
import CopyButton from './CopyButton';

/**
 * Resolves file URLs by prepending threadId when necessary.
 * URLs in format api/files/{path} become api/files/{threadId}/{path}
 * URLs already containing a threadId (GUID format) are left unchanged.
 */
const resolveFileUrl = (url: string | undefined, threadId?: string): string | undefined => {
    if (!url) return url;

    // Only process api/files URLs
    const isApiFileUrl = url.startsWith('/api/files/') || url.startsWith('api/files/');
    if (!isApiFileUrl || !threadId) return url;

    // Normalize URL (remove leading slash for parsing)
    const normalizedUrl = url.startsWith('/') ? url.substring(1) : url;
    const parts = normalizedUrl.split('/');

    // Format: api/files/{path} -> needs threadId
    // Format: api/files/{guid}/{path} -> already has threadId
    if (parts.length >= 3) {
        const potentialGuid = parts[2];
        // Check if third segment is already a GUID (threadId)
        const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(potentialGuid);
        if (!isGuid) {
            // Insert threadId: api/files/{path} -> api/files/{threadId}/{path}
            const path = parts.slice(2).join('/');
            return `/api/files/${threadId}/${path}`;
        }
    }

    return url.startsWith('/') ? url : `/${url}`;
};

/**
 * Image component that fetches images with authentication headers for API URLs.
 * Falls back to standard <img> for external URLs or data URIs.
 */
const AuthenticatedImage = ({ src, alt, threadId, ...props }: React.ImgHTMLAttributes<HTMLImageElement> & { threadId?: string }) => {
    const intl = useIntl();
    const [blobUrl, setBlobUrl] = useState<string | undefined>();
    const [error, setError] = useState(false);

    // Resolve the URL with threadId if needed
    const resolvedSrc = resolveFileUrl(src, threadId);

    // Determine if this URL requires authentication (API file paths)
    const requiresAuth = resolvedSrc?.startsWith('/api/files/') || resolvedSrc?.startsWith('api/files/');

    useEffect(() => {
        if (!resolvedSrc || !requiresAuth) {
            return;
        }

        let isMounted = true;
        const controller = new AbortController();

        const fetchImage = async () => {
            try {
                const response = await fetch(resolvedSrc, {
                    headers: getAgentHeaders(),
                    signal: controller.signal,
                });

                if (!response.ok) {
                    throw new Error(`Failed to fetch image: ${response.statusText}`);
                }

                const blob = await response.blob();
                if (isMounted) {
                    const url = URL.createObjectURL(blob);
                    setBlobUrl(url);
                }
            } catch (err) {
                if (isMounted && err instanceof Error && err.name !== 'AbortError') {
                    console.error('Error fetching authenticated image:', err);
                    setError(true);
                }
            }
        };

        fetchImage();

        return () => {
            isMounted = false;
            controller.abort();
            if (blobUrl) {
                URL.revokeObjectURL(blobUrl);
            }
        };
    }, [resolvedSrc, requiresAuth]);

    // For non-authenticated URLs, render directly
    if (!requiresAuth) {
        return <img src={resolvedSrc} alt={alt} {...props} style={{ maxWidth: '100%', height: 'auto', ...props.style }} />;
    }

    // Show error state
    if (error) {
        return <span title={`Failed to load image: ${resolvedSrc}`}>{intl.formatMessage(SreAgentResources.imageFailedToLoad)}</span>;
    }

    // Show loading or render authenticated image
    if (!blobUrl) {
        return <span>{intl.formatMessage(SreAgentResources.loadingImage)}</span>;
    }

    return <img src={blobUrl} alt={alt} {...props} style={{ maxWidth: '100%', height: 'auto', ...props.style }} />;
};

const useStyles = makeStyles({
    chatRoot: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '1px 16px',
        borderRadius: tokens.borderRadiusXLarge,
    },
    codeInline: {
        backgroundColor: tokens.colorNeutralBackground4,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'inline-block',
        padding: '2px 6px',
        borderRadius: tokens.borderRadiusMedium,
    },
    codeBlockWrapper: {
        position: 'relative' as const,
        backgroundColor: tokens.colorNeutralBackground4,
        borderRadius: tokens.borderRadiusLarge,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        marginTop: tokens.spacingVerticalS,
        marginBottom: tokens.spacingVerticalS,
    },
    codeBlockCopyButton: {
        position: 'absolute' as const,
        top: '8px',
        right: '8px',
        opacity: 0.7,
        ':hover': {
            opacity: 1,
        },
    },
    codeBlockInPre: {
        backgroundColor: tokens.colorTransparentBackground,
        fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Monaco, monospace',
        fontSize: '14px',
        display: 'block',
        whiteSpace: 'pre',
        color: tokens.colorNeutralForeground1,
        lineHeight: '1.6',
    },
    pre: {
        overflowX: 'auto',
        overflowY: 'auto',
        maxHeight: '400px',
        backgroundColor: tokens.colorTransparentBackground,
        borderRadius: tokens.borderRadiusLarge,
        padding: '14px',
        margin: 0,
    },
    h3: { fontWeight: '600', fontSize: '14px', lineHeight: '20px' },
    h2: { fontWeight: '600', fontSize: '16px', lineHeight: '22px' },
    h1: { fontWeight: '600', fontSize: '20px', lineHeight: '26px' },
    blockquote: {
        borderLeft: `4px solid ${tokens.colorNeutralStroke1}`,
        paddingLeft: '10px',
        marginLeft: '0',
        marginRight: '0',
        marginBottom: '10px',
        fontStyle: 'italic',
    },
    ol: {
        lineHeight: '26px',
    },
    ul: {
        lineHeight: '26px',
    },
    tableWrapper: {
        overflowX: 'auto',
        maxWidth: '100%',
        marginTop: tokens.spacingVerticalM,
        marginBottom: tokens.spacingVerticalM,
        '& table': {
            tableLayout: 'auto',
            minWidth: 'max-content',
        },
        '& th, & td': {
            whiteSpace: 'nowrap',
            paddingRight: tokens.spacingHorizontalM,
        },
    },
});

interface TableData {
    headers: string[];
    rows: string[][];
}

interface SortableTableProps {
    tableData: TableData;
}

type TableItem = {
    [key: string]: string;
};

interface ReactMarkdownComponentProps {
    content?: string | null;
    className?: string;
    variant?: 'chat' | 'panel' | 'default';
    threadId?: string;
}

interface MarkdownNode {
    type: string;
    tagName?: string;
    children?: MarkdownNode[];
    value?: string;
}

interface ReactMarkdownTableProps {
    node?: MarkdownNode;
    children?: React.ReactNode;
    [key: string]: any;
}

const isElementNode = (node: MarkdownNode): node is MarkdownNode & { type: 'element'; tagName: string } => {
    return node.type === 'element' && typeof node.tagName === 'string';
};

const isTextNode = (node: MarkdownNode): node is MarkdownNode & { type: 'text'; value: string } => {
    return node.type === 'text' && typeof node.value === 'string';
};

const hasChildren = (node: MarkdownNode): node is MarkdownNode & { children: MarkdownNode[] } => {
    return Array.isArray(node.children);
};

// Helper function to extract text from AST nodes
const extractText = (children: MarkdownNode[] | undefined): string => {
    if (!children || !Array.isArray(children)) return '';

    return children
        .map((child: MarkdownNode) => {
            if (isTextNode(child)) {
                return child.value;
            }
            if (isElementNode(child)) {
                const inlineElements = ['strong', 'em', 'code', 'a'];
                if (inlineElements.includes(child.tagName) && hasChildren(child)) {
                    return extractText(child.children);
                }
                if (hasChildren(child)) {
                    return extractText(child.children);
                }
            }
            return '';
        })
        .join('');
};

const SortableTable = memo(({ tableData, className }: SortableTableProps & { className?: string }) => {
    const items: TableItem[] = useMemo(() => {
        return tableData.rows.map((row, index) => {
            const item: TableItem = { id: index.toString() };
            tableData.headers.forEach((header, headerIndex) => {
                item[header] = row[headerIndex] || '';
            });
            return item;
        });
    }, [tableData]);

    const createCompareFunction = (columnHeader: string) => (a: TableItem, b: TableItem) => {
        const aVal = a[columnHeader] || '';
        const bVal = b[columnHeader] || '';

        const aNum = parseFloat(aVal);
        const bNum = parseFloat(bVal);

        if (!isNaN(aNum) && !isNaN(bNum)) {
            return aNum - bNum;
        }

        return aVal.localeCompare(bVal);
    };

    const columns: TableColumnDefinition<TableItem>[] = useMemo(() => {
        return tableData.headers.map(header =>
            createTableColumn<TableItem>({
                columnId: header,
                renderHeaderCell: () => <Text weight="semibold">{header}</Text>,
                compare: createCompareFunction(header),
            })
        );
    }, [tableData.headers]);

    const [columnSizingOptions] = useState<TableColumnSizingOptions>(() => {
        const options: TableColumnSizingOptions = {};
        tableData.headers.forEach(header => {
            options[header] = {
                idealWidth: 150,
                minWidth: 80,
            };
        });
        return options;
    });

    const {
        getRows,
        sort: { getSortDirection, toggleColumnSort, sort },

        columnSizing_unstable,
        tableRef,
    } = useTableFeatures(
        {
            columns,
            items,
        },
        [
            useTableSort({
                defaultSortState: { sortColumn: tableData.headers[0], sortDirection: 'ascending' },
            }),
            useTableColumnSizing_unstable({ columnSizingOptions }),
        ]
    );

    const headerSortProps = (columnId: TableColumnId) => ({
        onClick: (e: React.MouseEvent) => {
            toggleColumnSort(e, columnId);
        },
        sortDirection: getSortDirection(columnId),
    });

    const rows = sort(getRows());

    return (
        <div className={className} style={{ overflowX: 'auto' }}>
            <Table
                sortable
                ref={tableRef}
                {...columnSizing_unstable.getTableProps()}
                style={{ tableLayout: 'auto', minWidth: 'max-content' }}
            >
                <TableHeader>
                    <TableRow>
                        {columns.map(column => (
                            <TableHeaderCell
                                key={column.columnId}
                                {...headerSortProps(column.columnId)}
                                {...columnSizing_unstable.getTableHeaderCellProps(column.columnId)}
                                style={{ whiteSpace: 'nowrap', paddingRight: '16px' }}
                            >
                                {column.renderHeaderCell()}
                            </TableHeaderCell>
                        ))}
                    </TableRow>
                </TableHeader>
                <TableBody>
                    {rows.map(({ item }) => (
                        <TableRow key={item.id}>
                            {tableData.headers.map(header => (
                                <TableCell
                                    key={header}
                                    {...columnSizing_unstable.getTableCellProps(header)}
                                    style={{ whiteSpace: 'nowrap', paddingRight: '16px' }}
                                >
                                    <TableCellLayout>{item[header] || '-'}</TableCellLayout>
                                </TableCell>
                            ))}
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
        </div>
    );
});

const renderMarkdownTable = (props: ReactMarkdownTableProps, proxy: any, tableWrapperClass: string): JSX.Element | null => {
    const { node } = props;

    try {
        let headers: string[] = [];
        const rows: string[][] = [];

        for (const child of node?.children ?? []) {
            if (!isElementNode(child) || !hasChildren(child)) continue;
            if (child.tagName === 'thead') {
                // Extract headers from thead > tr > th
                for (const headerRow of child.children) {
                    if (isElementNode(headerRow) && headerRow.tagName === 'tr' && hasChildren(headerRow)) {
                        headers = headerRow.children
                            .filter((cell): cell is MarkdownNode => isElementNode(cell) && cell.tagName === 'th')
                            .map(cell => extractText(hasChildren(cell) ? cell.children : undefined));
                    }
                }
            } else if (child.tagName === 'tbody') {
                // Extract rows from tbody > tr > td
                for (const bodyRow of child.children) {
                    if (isElementNode(bodyRow) && bodyRow.tagName === 'tr' && hasChildren(bodyRow)) {
                        const row = bodyRow.children
                            .filter((cell): cell is MarkdownNode => isElementNode(cell) && cell.tagName === 'td')
                            .map(cell => extractText(hasChildren(cell) ? cell.children : undefined));

                        if (row.length > 0) {
                            rows.push(row);
                        }
                    }
                }
            }
        }

        return <SortableTable tableData={{ headers, rows }} className={tableWrapperClass} />;
    } catch (error) {
        proxy.log({
            action: 'MarkdownTableParsing',
            actionModifier: 'failed',
            data: {
                errorMessage: error instanceof Error ? error.message : 'Unknown error',
                nodeType: node?.type,
                nodeTagName: node?.tagName,
                childrenCount: node?.children?.length,
            },
        });
        return (
            <div className={tableWrapperClass}>
                <Table
                    style={{
                        tableLayout: 'auto',
                    }}
                >
                    {props.children}
                </Table>
            </div>
        );
    }
};

const ReactMarkdownComponent = ({ content, className, variant = 'default', threadId }: ReactMarkdownComponentProps) => {
    const styles = useStyles();
    const proxy = useContext(AzPortalContext);
    // Note: chatRoot styling removed for chat variant to make agent text appear as plain text (no bubble)
    const rootClass = className;

    return (
        <div className={rootClass}>
            <ReactMarkdown
                remarkPlugins={[remarkGfm]}
                rehypePlugins={[rehypeRaw]}
                components={{
                    h1: ({ children }: any) => (
                        <Title3 as="h1" block className={variant === 'chat' ? styles.h1 : undefined}>
                            {children}
                        </Title3>
                    ),
                    h2: ({ children }: any) => (
                        <Subtitle1 as="h2" block className={variant === 'chat' ? styles.h2 : undefined}>
                            {children}
                        </Subtitle1>
                    ),
                    h3: ({ children }: any) => (
                        <Subtitle2 as="h3" block className={variant === 'chat' ? styles.h3 : undefined}>
                            {children}
                        </Subtitle2>
                    ),
                    h4: ({ children }: any) => (
                        <Subtitle2 as="h4" block>
                            {children}
                        </Subtitle2>
                    ),
                    h5: ({ children }: any) => (
                        <Subtitle2 as="h5" block>
                            {children}
                        </Subtitle2>
                    ),
                    h6: ({ children }: any) => (
                        <Subtitle2 as="h6" block>
                            {children}
                        </Subtitle2>
                    ),
                    p: ({ children }: any) => (
                        <Text as="p" block>
                            {children}
                        </Text>
                    ),
                    strong: ({ children }: any) => (
                        <Text as="strong" weight="semibold">
                            {children}
                        </Text>
                    ),
                    em: ({ children }: any) => (
                        <Text as="em" italic>
                            {children}
                        </Text>
                    ),
                    del: ({ children }: any) => <Text strikethrough>{children}</Text>,
                    // Currently, ReactMarkdown only parses Markdown underlines as <strong>.
                    // It's likely not entirely sound to just replaceAll('__') either...
                    u: ({ children }: any) => <Text underline>{children}</Text>,
                    blockquote: ({ children }: any) => <blockquote className={styles.blockquote}>{children}</blockquote>,
                    a: ({ children, href }: any) => {
                        const resolvedHref = resolveFileUrl(href, threadId);
                        const isApiFileLink = resolvedHref?.startsWith('/api/files/');

                        const handleFileDownload = async (e: React.MouseEvent) => {
                            e.preventDefault();

                            try {
                                const response = await fetch(resolvedHref!, {
                                    headers: getAgentHeaders(),
                                });

                                if (!response.ok) {
                                    throw new Error(`Failed to download file: ${response.statusText}`);
                                }

                                const blob = await response.blob();
                                const url = window.URL.createObjectURL(blob);
                                const a = document.createElement('a');
                                a.href = url;
                                a.download = resolvedHref!.split('/').pop() || 'download';
                                document.body.appendChild(a);
                                a.click();
                                document.body.removeChild(a);
                                window.URL.revokeObjectURL(url);
                            } catch (error) {
                                console.error('Error downloading file:', error);
                                window.open(resolvedHref, '_blank');
                            }
                        };

                        if (isApiFileLink) {
                            return (
                                <Link onClick={handleFileDownload} style={{ cursor: 'pointer' }}>
                                    {children}
                                </Link>
                            );
                        }

                        return (
                            <Link href={resolvedHref} target="_blank" rel="noopener noreferrer">
                                {children}
                            </Link>
                        );
                    },
                    code: (props: any) => {
                        const isInPre = props.node?.parent?.tagName === 'pre';
                        const cls = isInPre ? styles.codeBlockInPre : styles.codeInline;
                        return <code className={cls}>{props.children}</code>;
                    },
                    pre: (props: any) => {
                        // Extract text content for copy button
                        const getTextContent = (node: any): string => {
                            if (typeof node === 'string') return node;
                            if (Array.isArray(node)) return node.map(getTextContent).join('');
                            if (node?.props?.children) return getTextContent(node.props.children);
                            return '';
                        };
                        const codeText = getTextContent(props.children);

                        return (
                            <div className={styles.codeBlockWrapper}>
                                <div className={styles.codeBlockCopyButton}>
                                    <CopyButton textToCopy={codeText} buttonAppearance="transparent" />
                                </div>
                                <pre className={styles.pre}>{props.children}</pre>
                            </div>
                        );
                    },
                    ul: (props: any) => <ul className={styles.ul}>{props.children}</ul>,
                    ol: (props: any) => <ol className={styles.ol}>{props.children}</ol>,
                    li: (props: any) => <li>{props.children}</li>,
                    table: (props: any) => renderMarkdownTable(props, proxy, styles.tableWrapper),
                    // Simple fallback table components
                    thead: ({ children }: any) => <TableHeader>{children}</TableHeader>,
                    tbody: ({ children }: any) => <TableBody>{children}</TableBody>,
                    tr: ({ children }: any) => <TableRow>{children}</TableRow>,
                    th: ({ children }: any) => (
                        <TableHeaderCell>
                            <Text weight="semibold">{children}</Text>
                        </TableHeaderCell>
                    ),
                    td: ({ children }: any) => <TableCell>{children ?? '-'}</TableCell>,
                    img: ({ src, alt, ...props }: any) => <AuthenticatedImage src={src} alt={alt} threadId={threadId} {...props} />,
                }}
            >
                {content}
            </ReactMarkdown>
        </div>
    );
};

export default memo(ReactMarkdownComponent);
