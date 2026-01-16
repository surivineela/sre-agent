import { Badge } from '@fluentui/react-components';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

export const PreviewBadge = () => {
    const intl = useIntl();
    return (
        <Badge color="brand" appearance="tint" shape="rounded" size="small">
            {intl.formatMessage(PortalResources.previewCapitalized)}
        </Badge>
    );
};
