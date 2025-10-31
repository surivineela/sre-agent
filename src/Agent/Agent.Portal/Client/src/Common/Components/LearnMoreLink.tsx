import { Link, makeStyles } from '@fluentui/react-components';
import { OpenFilled } from '@fluentui/react-icons';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

const useLearnMoreLinkStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'center',
        alignItems: 'center',
        gap: '4px',
    },
});

interface LearnMoreLinkProps {
    linkText?: string;
    url: string;
    dontOpenInNewTab?: true;
    dontShowIcon?: true;
    className?: string;
}

/**
 * <Link /> wrapper
 *
 * By default:
 * - Opens links in new tabs (`dontOpenInNewTab`)
 * - Shows icon (`dontShowIcon`)
 */
export const LearnMoreLink = (props: LearnMoreLinkProps) => {
    const { linkText, url, dontOpenInNewTab, dontShowIcon, className } = props;

    const intl = useIntl();
    const learnMoreLinkStyles = useLearnMoreLinkStyles();

    const coreLinkProps = useMemo(() => {
        const openInNewTabProps = {
            target: '_blank',
            rel: 'noopener noreferrer',
        };

        return dontOpenInNewTab ? {} : openInNewTabProps;
    }, [dontOpenInNewTab]);

    return (
        <Link href={url} {...coreLinkProps}>
            <span className={learnMoreLinkStyles.root}>
                <span className={className}>{linkText ?? intl.formatMessage(PortalResources.getMoreInfo)}</span>
                {dontShowIcon ? null : <OpenFilled fontSize={16} />}
            </span>
        </Link>
    );
};
