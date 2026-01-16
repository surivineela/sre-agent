import { Text, mergeClasses } from '@fluentui/react-components';
import { memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { CopyButton } from '../../../Common/Components/CopyButton';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { ReadFileToolContentProps } from './types';
import { useToolCallStyles } from './useToolCallStyles';

/**
 * Content renderer for file read results.
 * Displays file content with line numbers in a code-style view.
 */
const ReadFileToolContent = ({ result }: ReadFileToolContentProps) => {
    const classes = useToolCallStyles();
    const intl = useIntl();

    // Split content into lines for display
    const lines = useMemo(() => {
        if (!result.content) return [];
        return result.content.split('\n');
    }, [result.content]);

    // Build header text
    const headerText = useMemo(() => {
        if (result.startLine === 1 && result.endLine >= result.totalLines) {
            return `${result.filePath} (${result.totalLines} lines)`;
        }
        return `${result.filePath} (lines ${result.startLine}-${result.endLine} of ${result.totalLines})`;
    }, [result]);

    // Error state
    if (result.error) {
        return (
            <>
                <div className={classes.contentHeader}>
                    <div className={classes.contentHeaderLeft}>
                        <Text weight="semibold">{result.filePath}</Text>
                    </div>
                </div>
                <div className={mergeClasses(classes.terminalOutput, classes.errorText)}>{result.error}</div>
            </>
        );
    }

    // Empty file
    if (lines.length === 0 || (lines.length === 1 && lines[0] === '')) {
        return (
            <>
                <div className={classes.contentHeader}>
                    <div className={classes.contentHeaderLeft}>
                        <Text weight="semibold">{result.filePath}</Text>
                        <Text size={200} style={{ fontStyle: 'italic', opacity: 0.7 }}>
                            (empty file)
                        </Text>
                    </div>
                </div>
            </>
        );
    }

    return (
        <>
            {/* Header with file path and copy button */}
            <div className={classes.contentHeader}>
                <div className={classes.contentHeaderLeft}>
                    <Text weight="semibold">{headerText}</Text>
                </div>
                <CopyButton textToCopy={result.content} />
            </div>

            {/* File content with line numbers */}
            <div className={classes.codeContainer}>
                {lines.map((line, index) => {
                    const lineNumber = result.startLine + index;
                    return (
                        <div key={lineNumber} className={classes.codeLine}>
                            <div className={classes.lineNumber}>{lineNumber}</div>
                            <div className={classes.lineContent}>{line || ' '}</div>
                        </div>
                    );
                })}
            </div>

            {/* Truncation notice */}
            {result.isTruncated && (
                <div className={classes.truncationNotice}>{intl.formatMessage(SreAgentResources.outputTruncatedUseOffset)}</div>
            )}
        </>
    );
};

export default memo(ReadFileToolContent);
