import { memo } from "react";
import { Image, Link, makeStyles, Spinner } from "@fluentui/react-components";

interface HealthStatusProps {
    health?: string;
    showReportButton?: boolean;
    onClickReportButton?: () => Promise<void>;
    isSendingReport?: boolean;
}

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'center',
        gap: '5px'
    }
});

const HealthStatus = ({ health, showReportButton, onClickReportButton, isSendingReport }: HealthStatusProps) => {

    const { container } = useStyles();

    let healthIconSrc = "";
    let isNodeUnhealthy = false;
    switch (health?.toLowerCase()) {
        case "unhealthy":
            healthIconSrc = "./failed.svg";
            isNodeUnhealthy = true;
            break;
        case "healthy":
            healthIconSrc = "./success.svg";
            break;
        case "degraded":
            healthIconSrc = "./warning.svg";
            break;
    }

    return health ? <div className={container}>
        {healthIconSrc && <Image src={healthIconSrc} width={16} height={16} />}
        <span>{health}</span>
        {
            showReportButton && isNodeUnhealthy && (
                isSendingReport ?
                    <div className={container}>
                        <Spinner size={'small'} />
                        <span>{'Sending a report...'}</span>
                    </div> :
                    <Link onClick={() => {
                        onClickReportButton?.();
                    }}>{'Report unhealthy node'}</Link>
            )
        }
    </div> : null;
};

export default memo(HealthStatus);