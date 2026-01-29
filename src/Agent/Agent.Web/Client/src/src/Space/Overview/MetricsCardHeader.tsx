import { Body1Strong, Caption1, tokens } from '@fluentui-copilot/react-copilot';
import { CardHeader, makeStyles } from '@fluentui/react-components';
import { FC, memo, ReactNode } from 'react';
import RefreshButton from './RefreshButton';

export interface MetricsCardHeaderProps {
    title: string;
    subtitle?: string;
    children?: ReactNode;
    refresh: () => Promise<unknown>;
}

const useStyles = makeStyles({
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        width: '100%',
    },
    headerFarItems: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
    },
});

export const MetricsCardHeader: FC<MetricsCardHeaderProps> = ({ title, subtitle, refresh, children }) => {
    const styles = useStyles();

    return (
        <CardHeader
            header={
                <div className={styles.header}>
                    <Body1Strong>{title}</Body1Strong>
                    <div className={styles.headerFarItems}>
                        <RefreshButton refresh={refresh} />
                        {children}
                    </div>
                </div>
            }
            description={subtitle ? <Caption1>{subtitle}</Caption1> : undefined}
        />
    );
};

export default memo(MetricsCardHeader);
