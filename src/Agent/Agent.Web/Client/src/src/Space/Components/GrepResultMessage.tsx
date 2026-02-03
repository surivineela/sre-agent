import { Badge, makeStyles, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { ChevronDown20Regular, ChevronRight20Regular, Document20Regular, Search24Regular } from '@fluentui/react-icons';
import { memo, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { CopyButton } from '../../Common/Components/CopyButton';
import { GrepFileResult, GrepLineMatch, GrepSearchResult, MatchRange } from '../../Common/Contracts/DataPlane/GrepResult';
import { SreAgentResources } from '../../Strings/SREAgentResources';

interface GrepResultMessageProps {
    grepSearchResult: GrepSearchResult;
}

const useStyles = makeStyles({
    // Card wrapper for the entire component
    card: {
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: '12px',
        transitionProperty: 'background-color, border-color',
        transitionDuration: '0.15s',
        transitionTimingFunction: 'ease',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
            border: `1px solid ${tokens.colorNeutralStroke1Hover}`,
        },
    },
    // Card header with icon and content
    cardHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        cursor: 'pointer',
    },
    cardHeaderNoResults: {
        cursor: 'default',
    },
    // Icon container (40px square, rounded)
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '40px',
        height: '40px',
        borderRadius: '8px',
        backgroundColor: tokens.colorNeutralBackground4,
        flexShrink: 0,
    },
    icon: {
        color: tokens.colorNeutralForeground3,
    },
    // Content area next to icon
    headerContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        flex: 1,
        minWidth: 0,
    },
    primaryText: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        flexWrap: 'wrap',
    },
    queryText: {
        fontFamily: 'Consolas, Monaco, monospace',
        color: tokens.colorNeutralForeground1,
        fontWeight: 600,
    },
    secondaryText: {
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
    },
    noResultsText: {
        color: tokens.colorNeutralForeground4,
        fontStyle: 'italic',
        fontSize: '12px',
    },
    chevronIcon: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
    },
    // Expanded content container
    expandedContainer: {
        marginTop: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '8px',
        backgroundColor: tokens.colorNeutralBackground1,
        overflow: 'hidden',
    },
    resultsHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '8px 12px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    resultsHeaderLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    fileHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '6px 12px',
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground2Hover,
        },
    },
    fileIcon: {
        color: tokens.colorNeutralForeground3,
        flexShrink: 0,
    },
    filePath: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '13px',
        color: tokens.colorNeutralForeground1,
        flex: 1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    matchBadge: {
        marginLeft: 'auto',
        flexShrink: 0,
    },
    codeContainer: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        lineHeight: '18px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderTop: `1px solid ${tokens.colorNeutralStroke3}`,
    },
    codeLine: {
        display: 'flex',
        minHeight: '18px',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground2Hover,
        },
    },
    lineNumber: {
        minWidth: '48px',
        padding: '0 8px',
        textAlign: 'right',
        color: tokens.colorNeutralForeground4,
        userSelect: 'none',
        borderRight: `1px solid ${tokens.colorNeutralStroke3}`,
        flexShrink: 0,
    },
    lineContent: {
        padding: '0 12px',
        whiteSpace: 'pre',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        flex: 1,
    },
    contextLine: {
        color: tokens.colorNeutralForeground4,
    },
    matchLine: {
        color: tokens.colorNeutralForeground1,
    },
    matchHighlight: {
        backgroundColor: '#fff3cd',
        color: tokens.colorNeutralForeground1,
        borderRadius: '2px',
        padding: '0 1px',
    },
    copyButton: {
        marginLeft: '8px',
    },
});

/**
 * Renders line content with match highlighting
 */
const HighlightedContent = memo(
    ({ content, matchRanges, isContext }: { content: string; matchRanges: MatchRange[]; isContext: boolean }) => {
        const classes = useStyles();

        if (isContext || matchRanges.length === 0) {
            return <span>{content}</span>;
        }

        const parts: React.ReactNode[] = [];
        let lastEnd = 0;

        // Sort ranges by start position
        const sortedRanges = [...matchRanges].sort((a, b) => a.start - b.start);

        sortedRanges.forEach((range, index) => {
            // Add text before match
            if (range.start > lastEnd) {
                parts.push(<span key={`pre-${index}`}>{content.slice(lastEnd, range.start)}</span>);
            }

            // Add highlighted match
            parts.push(
                <span key={`match-${index}`} className={classes.matchHighlight}>
                    {content.slice(range.start, range.end)}
                </span>
            );

            lastEnd = range.end;
        });

        // Add remaining text
        if (lastEnd < content.length) {
            parts.push(<span key="suffix">{content.slice(lastEnd)}</span>);
        }

        return <>{parts}</>;
    }
);

/**
 * Single file result with collapsible code view
 */
const FileResultItem = memo(({ file, defaultOpen }: { file: GrepFileResult; defaultOpen: boolean }) => {
    const classes = useStyles();
    const intl = useIntl();
    const [isOpen, setIsOpen] = useState(defaultOpen);

    const copyContent = useMemo(() => {
        return file.matches
            .filter(m => !m.isContext)
            .map(m => `${file.filePath}:${m.lineNumber}: ${m.content}`)
            .join('\n');
    }, [file]);

    const matchLabel =
        file.matchCount === 1 ? intl.formatMessage(SreAgentResources.grepMatch) : intl.formatMessage(SreAgentResources.grepMatches);

    return (
        <div>
            <div className={classes.fileHeader} onClick={() => setIsOpen(!isOpen)}>
                {isOpen ? <ChevronDown20Regular className={classes.fileIcon} /> : <ChevronRight20Regular className={classes.fileIcon} />}
                <Document20Regular className={classes.fileIcon} />
                <span className={classes.filePath}>{file.filePath}</span>
                <Badge appearance="outline" color="informative" className={classes.matchBadge}>
                    {file.matchCount} {matchLabel}
                </Badge>
                <div className={classes.copyButton} onClick={e => e.stopPropagation()}>
                    <CopyButton textToCopy={copyContent} />
                </div>
            </div>

            {isOpen && (
                <div className={classes.codeContainer}>
                    {file.matches.map((match: GrepLineMatch, index: number) => (
                        <div key={`${match.lineNumber}-${index}`} className={classes.codeLine}>
                            <div className={classes.lineNumber}>{match.lineNumber}</div>
                            <div className={mergeClasses(classes.lineContent, match.isContext ? classes.contextLine : classes.matchLine)}>
                                <HighlightedContent content={match.content} matchRanges={match.matchRanges} isContext={match.isContext} />
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
});

const GrepResultMessage = ({ grepSearchResult }: GrepResultMessageProps) => {
    const classes = useStyles();
    const intl = useIntl();
    const [isExpanded, setIsExpanded] = useState(false);

    const allContentForCopy = useMemo(() => {
        return grepSearchResult.files
            .flatMap(file => file.matches.filter(m => !m.isContext).map(m => `${file.filePath}:${m.lineNumber}: ${m.content}`))
            .join('\n');
    }, [grepSearchResult]);

    const hasResults = grepSearchResult.files.length > 0;

    return (
        <div className={classes.card}>
            {/* Card header with icon container */}
            <div
                className={mergeClasses(classes.cardHeader, !hasResults && classes.cardHeaderNoResults)}
                onClick={() => hasResults && setIsExpanded(!isExpanded)}
            >
                <div className={classes.iconContainer}>
                    <Search24Regular className={classes.icon} />
                </div>
                <div className={classes.headerContent}>
                    <div className={classes.primaryText}>
                        <Text>{intl.formatMessage(SreAgentResources.grepSearchedFor)}</Text>
                        <Text className={classes.queryText}>{grepSearchResult.query}</Text>
                        {grepSearchResult.isRegex && (
                            <Badge appearance="outline" size="small" color="subtle">
                                {intl.formatMessage(SreAgentResources.grepRegex)}
                            </Badge>
                        )}
                    </div>
                    {hasResults ? (
                        <Text className={classes.secondaryText}>
                            {intl.formatMessage(SreAgentResources.grepMatchesFound, {
                                count: grepSearchResult.totalMatches,
                                fileCount: grepSearchResult.files.length,
                            })}
                        </Text>
                    ) : (
                        <Text className={classes.noResultsText}>{intl.formatMessage(SreAgentResources.grepNoResults)}</Text>
                    )}
                </div>
                {hasResults &&
                    (isExpanded ? (
                        <ChevronDown20Regular className={classes.chevronIcon} />
                    ) : (
                        <ChevronRight20Regular className={classes.chevronIcon} />
                    ))}
            </div>

            {/* Expanded results inside the card */}
            {isExpanded && hasResults && (
                <div className={classes.expandedContainer}>
                    {/* Results header with copy button */}
                    <div className={classes.resultsHeader}>
                        <div className={classes.resultsHeaderLeft}>
                            <Text weight="semibold">
                                {intl.formatMessage(SreAgentResources.grepMatchesFound, {
                                    count: grepSearchResult.totalMatches,
                                    fileCount: grepSearchResult.files.length,
                                })}
                            </Text>
                            {grepSearchResult.isRegex && (
                                <Badge appearance="outline" size="small">
                                    {intl.formatMessage(SreAgentResources.grepRegex)}
                                </Badge>
                            )}
                        </div>
                        <CopyButton textToCopy={allContentForCopy} />
                    </div>

                    {/* File results - all collapsed by default */}
                    {grepSearchResult.files.map(file => (
                        <FileResultItem key={file.filePath} file={file} defaultOpen={false} />
                    ))}
                </div>
            )}
        </div>
    );
};

export default memo(GrepResultMessage);
