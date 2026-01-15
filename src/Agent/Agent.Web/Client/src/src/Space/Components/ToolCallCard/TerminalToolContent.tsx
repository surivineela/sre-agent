import { makeStyles, tokens } from '@fluentui/react-components';
import { CheckmarkCircle16Regular, DismissCircle16Filled } from '@fluentui/react-icons';
import { memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { CopyButton } from '../../../Common/Components/CopyButton';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { TerminalToolContentProps } from './types';
import { useToolCallStyles } from './useToolCallStyles';

const useLocalStyles = makeStyles({
    exitCodeBar: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '6px 12px',
        fontSize: '12px',
        fontFamily: 'Consolas, Monaco, monospace',
    },
    exitCodeSuccess: {
        backgroundColor: tokens.colorPaletteGreenBackground1,
        color: tokens.colorPaletteGreenForeground1,
    },
    exitCodeError: {
        backgroundColor: tokens.colorPaletteRedBackground1,
        color: tokens.colorPaletteRedForeground1,
    },
    exitCodeIcon: {
        display: 'flex',
        alignItems: 'center',
    },
});

/**
 * Content renderer for terminal execution results.
 * Displays command output in a terminal-style view.
 */
const TerminalToolContent = ({ result }: TerminalToolContentProps) => {
    const classes = useToolCallStyles();
    const localClasses = useLocalStyles();
    const intl = useIntl();

    // Combine output for copy
    const copyContent = useMemo(() => {
        let content = '';
        if (result.output) content += result.output;
        if (result.error) {
            if (content) content += '\n\n';
            content += `Error: ${result.error}`;
        }
        return content || result.command;
    }, [result]);

    const isError = result.status === 'Failed' || (result.exitCode !== undefined && result.exitCode !== 0);
    const exitCode = result.exitCode ?? (result.status === 'Failed' ? 1 : 0);

    // Exit code bar component
    const exitCodeBar = useMemo(() => {
        if (result.isBackground || result.status === 'Running') {
            return null;
        }
        return (
            <div className={`${localClasses.exitCodeBar} ${isError ? localClasses.exitCodeError : localClasses.exitCodeSuccess}`}>
                <span className={localClasses.exitCodeIcon}>{isError ? <DismissCircle16Filled /> : <CheckmarkCircle16Regular />}</span>
                Exited with code {exitCode}
            </div>
        );
    }, [result, isError, exitCode, localClasses]);

    // Background command - minimal display
    if (result.isBackground) {
        return (
            <>
                <div className={classes.contentHeader}>
                    <div className={classes.contentHeaderLeft}>
                        <span className={classes.commandText}>{result.command}</span>
                    </div>
                </div>
                <div className={classes.terminalOutput}>
                    Started in background{result.sessionId ? ` (session: ${result.sessionId})` : ''}.{'\n'}Use terminal state to check
                    output later.
                </div>
            </>
        );
    }

    // No output case
    const hasOutput = result.output || result.error;
    if (!hasOutput) {
        return (
            <>
                <div className={classes.contentHeader}>
                    <div className={classes.contentHeaderLeft}>
                        <span className={classes.commandText}>{result.command}</span>
                    </div>
                </div>
                {exitCodeBar}
                <div className={classes.terminalOutput} style={{ fontStyle: 'italic', opacity: 0.7 }}>
                    {intl.formatMessage(SreAgentResources.commandCompletedNoOutput)}
                </div>
            </>
        );
    }

    return (
        <>
            {/* Header with command */}
            <div className={classes.contentHeader}>
                <div className={classes.contentHeaderLeft}>
                    <span className={classes.commandText}>{result.command}</span>
                </div>
                <CopyButton textToCopy={copyContent} />
            </div>

            {/* Exit code bar */}
            {exitCodeBar}

            {/* Command output */}
            <div className={classes.terminalOutput}>
                {result.output && <span className={classes.successText}>{result.output}</span>}
                {result.output && result.error && '\n\n'}
                {result.error && <span className={classes.errorText}>Error: {result.error}</span>}
            </div>
        </>
    );
};

export default memo(TerminalToolContent);
