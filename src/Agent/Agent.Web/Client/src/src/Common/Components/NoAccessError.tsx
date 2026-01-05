import { makeStyles, shorthands, tokens } from '@fluentui/react-components';
import { DocumentLockRegular } from '@fluentui/react-icons';
import { FC, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import CopyButton from './CopyButton';

/** Props for NoAccessError */
export interface NoAccessErrorProps {
    /** Permission name the current user is missing (e.g. "Agent.View") */
    requiredPermission: string;
    /** Resource (Agent) id user tried to access */
    resourceId: string;
    /** Optional: override heading text */
    headingText?: string;
}

const useStyles = makeStyles({
    root: {
        flex: '1 0 auto',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        textAlign: 'center',
        minHeight: '60vh',
        gap: '15px',
        ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalXXL),
    },
    icon: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
    },
    heading: {
        fontSize: tokens.fontSizeHero700,
        fontWeight: tokens.fontWeightSemibold,
        margin: 0,
    },
    instruction: {
        fontSize: tokens.fontSizeBase300,
        margin: 0,
        maxWidth: '620px',
        color: tokens.colorNeutralForeground2,
    },
    detailsBox: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
        gap: tokens.spacingVerticalXS,
        maxWidth: '520px',
        width: '100%',
        textAlign: 'left',
        marginTop: tokens.spacingVerticalM,
    },
    detailGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
        fontSize: tokens.fontSizeBase200,
    },
    detailLabel: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase200,
        margin: 0,
    },
    detailValue: {
        margin: 0,
        wordBreak: 'break-word',
        fontSize: tokens.fontSizeBase200,
        fontFamily: 'monospace',
    },
    actions: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        marginTop: tokens.spacingVerticalS,
    },
    compactRoot: {
        minHeight: 'unset',
        alignItems: 'flex-start',
        textAlign: 'left',
        padding: 0,
    },
});

export const NoAccessError: FC<NoAccessErrorProps> = ({ requiredPermission, resourceId, headingText }) => {
    const styles = useStyles();
    const intl = useIntl();

    const heading = headingText ?? intl.formatMessage(SreAgentResources.youDoNotHaveAccess);

    const errorDetailsJson = useMemo(
        () =>
            JSON.stringify(
                {
                    resourceId,
                    missingPermission: requiredPermission,
                },
                null,
                2
            ),
        [resourceId, requiredPermission]
    );

    return (
        <div className={styles.root} role="alert" aria-live="assertive" data-testid="no-access-error">
            <div className={styles.icon} aria-hidden="true">
                <DocumentLockRegular fontSize={56} />
            </div>
            <h1 className={styles.heading}>{heading}</h1>
            <div className={styles.instruction}>
                {intl.formatMessage(SreAgentResources.accessHelpInstruction)}
                <CopyButton textToCopy={errorDetailsJson} />
            </div>
            <div className={styles.detailsBox} aria-label={intl.formatMessage(SreAgentResources.errorDetails)}>
                <div className={styles.detailGroup}>
                    <p className={styles.detailLabel}>{intl.formatMessage(SreAgentResources.detailsResourceId)}</p>
                    <p className={styles.detailValue}>{resourceId}</p>
                    <p className={styles.detailLabel}>{intl.formatMessage(SreAgentResources.detailsPermission)}</p>
                    <p className={styles.detailValue}>{requiredPermission}</p>
                    <p className={styles.detailLabel}>{intl.formatMessage(SreAgentResources.detailsAccess)}</p>
                    <p className={styles.detailValue}>{intl.formatMessage(SreAgentResources.detailsAccessNoAccess)}</p>
                </div>
            </div>
        </div>
    );
};

export default NoAccessError;
