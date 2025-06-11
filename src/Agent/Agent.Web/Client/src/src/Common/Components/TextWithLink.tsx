import { Link, Text } from '@fluentui/react-components';
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
};

export const TextWithLink = memo((props: ITextWithLinkProps) => {
    const intl = useIntl();

    return (
        <div style={{ display: 'inline-block' }}>
            <Text style={{ marginRight: 4 }}>{props.text}</Text>
            <span style={{ display: 'inline-block' }}>
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
