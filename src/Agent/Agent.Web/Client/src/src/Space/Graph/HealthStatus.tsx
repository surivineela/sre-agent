import { Image, Link, makeStyles, mergeClasses, Spinner, Text, tokens } from '@fluentui/react-components';
import { memo } from 'react';
import { FormattedMessage } from 'react-intl';
import { AppHealth } from '../../Strings/SREAgentResources';

interface HealthStatusProps {
    health?: string;
    showReportButton?: boolean;
    onClickReportButton?: () => Promise<void>;
    isSendingReport?: boolean;
}

const useStyles = makeStyles({
    container: {
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalS,
    },
    healthStatus: {
        gap: tokens.spacingHorizontalXS,
    },
    common: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'center',
    },
});

const HealthStatus = ({ health, showReportButton, onClickReportButton, isSendingReport }: HealthStatusProps) => {
    const { container, healthStatus, common } = useStyles();

    let healthIconSrc = '';
    let healthText: { defaultMessage: string; id: string } | undefined = undefined;
    let isNodeUnhealthy = false;
    switch (health?.toLowerCase()) {
        case 'unhealthy':
            healthIconSrc = './failed.svg';
            isNodeUnhealthy = true;
            healthText = AppHealth.unhealthy;
            break;
        case 'healthy':
            healthIconSrc = './success.svg';
            healthText = AppHealth.healthy;
            break;
        case 'degraded':
            healthIconSrc = './warning.svg';
            healthText = AppHealth.degraded;
            break;
    }

    return health ? (
        <div className={mergeClasses(container, common)}>
            <div className={mergeClasses(common, healthStatus)}>
                {healthIconSrc && <Image src={healthIconSrc} width={16} height={16} />}
                <Text wrap={false}>{healthText ? <FormattedMessage {...healthText} /> : health}</Text>
            </div>
            {showReportButton &&
                isNodeUnhealthy &&
                (isSendingReport ? (
                    <div className={container}>
                        <Spinner size={'small'} />
                        <span>
                            <FormattedMessage {...AppHealth.sendingReport} />
                        </span>
                    </div>
                ) : (
                    <Link
                        onClick={() => {
                            onClickReportButton?.();
                        }}
                    >
                        <FormattedMessage {...AppHealth.reportUnhealthyNode} />
                    </Link>
                ))}
        </div>
    ) : null;
};

export default memo(HealthStatus);
