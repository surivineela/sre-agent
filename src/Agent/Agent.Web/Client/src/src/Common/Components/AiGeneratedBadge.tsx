import { Badge } from '@fluentui/react-components';
import { Sparkle16Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

export const AiGeneratedBadge = () => {
    const intl = useIntl();
    return (
        <Badge color="informative" shape="rounded" icon={<Sparkle16Regular />} style={{ height: 24 }}>
            {intl.formatMessage(SreAgentResources.aiGeneratedHyphenated)}
        </Badge>
    );
};
