import { makeStyles, Text, tokens } from '@fluentui/react-components';
import { memo, useMemo } from 'react';
import { getStatusPillComponentStyleProperties, getStatusPillComponentText } from './Utility';

const useStyles = makeStyles({
    statusContainer: {
        padding: '2px 10px',
        borderRadius: tokens.borderRadiusCircular,
        display: 'flex',
        flexDirection: 'row',
        gap: tokens.spacingHorizontalXS,
        alignItems: 'center',
        width: 'fit-content',
        flex: '0 0 auto',
    },
});

const NodeStatusPill = ({ status, showIcon }: { status?: string | null; showIcon: boolean }) => {
    const { statusContainer } = useStyles();

    const statusProps = useMemo(() => {
        const text = getStatusPillComponentText(status);
        const styleProperties = getStatusPillComponentStyleProperties(status);

        if (text && styleProperties) {
            return {
                ...styleProperties,
                text: text,
            };
        }
    }, [status]);

    return (
        statusProps && (
            <div
                className={statusContainer}
                style={{
                    backgroundColor: statusProps.backgroundColor,
                    border: statusProps.borderColor ? `1.5px solid ${statusProps.borderColor}` : 'none',
                }}
            >
                {showIcon && <statusProps.icon style={{ color: statusProps.iconFontColor ? statusProps.iconFontColor : 'undefined' }} />}
                <Text weight={'semibold'} style={{ color: statusProps.statusTextFontColor }}>
                    {statusProps.text}
                </Text>
            </div>
        )
    );
};

export default memo(NodeStatusPill);
