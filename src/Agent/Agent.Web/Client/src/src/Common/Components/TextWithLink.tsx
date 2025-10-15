import { Link, makeStyles, mergeClasses, Text } from '@fluentui/react-components';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { LearnMoreLink } from './LearnMoreLink';

export type ITextWithLinkProps = {
    text: string;
    /** `'Learn more'` by default */
    linkText?: string;
    /** If specified, `onClick` would be ignored */
    linkUrl?: string;
    disabled?: boolean;
    /** `onClick` is used if `linkUrl` is not specified */
    onClick?: () => void;
    /** Applies only when `linkUrl` is in place */
    dontShowLearnMoreLinkIcon?: true;
    textClassName?: string;
    linkClassName?: string;
};

const useTextWithLinkStyles = makeStyles({
    container: {
        display: 'inline-block',
    },
    text: {
        marginRight: '4px',
    },
    link: {
        display: 'inline-block',
    },
});

export const TextWithLink = memo((props: ITextWithLinkProps) => {
    const intl = useIntl();
    const styles = useTextWithLinkStyles();

    return (
        <div className={styles.container}>
            <Text className={mergeClasses(styles.text, props.textClassName)}>{props.text}</Text>
            <span className={styles.link}>
                {props.linkUrl ? (
                    <LearnMoreLink url={props.linkUrl} linkText={props.linkText} dontShowIcon={props.dontShowLearnMoreLinkIcon} />
                ) : (
                    <Link onClick={props.onClick} disabled={props.disabled}>
                        {props.linkText || intl.formatMessage(SreAgentResources.learnMore)}
                    </Link>
                )}
            </span>
        </div>
    );
});
