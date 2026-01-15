import { Badge, Text, mergeClasses } from '@fluentui/react-components';
import { ChevronDown20Regular, ChevronRight20Regular, Document20Regular } from '@fluentui/react-icons';
import { memo, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { CopyButton } from '../../../Common/Components/CopyButton';
import { GrepFileResult, GrepLineMatch, MatchRange } from '../../../Common/Contracts/DataPlane/GrepResult';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { GrepToolContentProps } from './types';
import { useToolCallStyles } from './useToolCallStyles';

/**
 * Renders line content with match highlighting.
 */
const HighlightedContent = memo(
    ({ content, matchRanges, isContext }: { content: string; matchRanges: MatchRange[]; isContext: boolean }) => {
        const classes = useToolCallStyles();

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
 * Single file result with collapsible code view.
 */
const FileResultItem = memo(({ file, defaultOpen }: { file: GrepFileResult; defaultOpen: boolean }) => {
    const classes = useToolCallStyles();
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
                <Badge appearance="outline" color="informative" className={classes.infoBadge}>
                    {file.matchCount} {matchLabel}
                </Badge>
                <div className={classes.copyButtonInline} onClick={e => e.stopPropagation()}>
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

/**
 * Content renderer for grep search results.
 * Displays a list of files with collapsible match previews.
 */
const GrepToolContent = ({ result }: GrepToolContentProps) => {
    const classes = useToolCallStyles();
    const intl = useIntl();

    const allContentForCopy = useMemo(() => {
        return result.files
            .flatMap(file => file.matches.filter(m => !m.isContext).map(m => `${file.filePath}:${m.lineNumber}: ${m.content}`))
            .join('\n');
    }, [result]);

    return (
        <>
            {/* Header with total count and copy button */}
            <div className={classes.contentHeader}>
                <div className={classes.contentHeaderLeft}>
                    <Text weight="semibold">
                        {intl.formatMessage(SreAgentResources.grepMatchesFound, {
                            count: result.totalMatches,
                            fileCount: result.files.length,
                        })}
                    </Text>
                    {result.isRegex && (
                        <Badge appearance="outline" size="small">
                            {intl.formatMessage(SreAgentResources.grepRegex)}
                        </Badge>
                    )}
                </div>
                <CopyButton textToCopy={allContentForCopy} />
            </div>

            {/* File results - all collapsed by default */}
            {result.files.map(file => (
                <FileResultItem key={file.filePath} file={file} defaultOpen={false} />
            ))}

            {/* Truncation notice */}
            {result.isTruncated && (
                <div className={classes.truncationNotice}>
                    Results limited to {result.maxResults} matches. Refine your search for more specific results.
                </div>
            )}
        </>
    );
};

export default memo(GrepToolContent);
