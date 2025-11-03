import { Badge } from '@fluentui/react-components';
import { memo, useMemo } from 'react';
import { getStatusPillComponentStyleProperties, getStatusPillComponentText } from './Utility';

const NodeStatusPill = ({ status, showIcon }: { status?: string | null; showIcon: boolean }) => {
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

    return status ? (
        <div>
            <Badge color={statusProps?.color || 'brand'} icon={statusProps?.icon && showIcon ? <statusProps.icon /> : undefined}>
                {statusProps?.text}
            </Badge>
        </div>
    ) : null;
};

export default memo(NodeStatusPill);
