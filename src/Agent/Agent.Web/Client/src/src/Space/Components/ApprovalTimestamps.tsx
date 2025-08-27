import { Text } from '@fluentui/react-components';
import { FormattedMessage } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { formatTimestampShort } from './Utility';

interface ApprovalTimestampsProps {
    created: string | number | Date;
    started?: string | number | Date;
    ended?: string | number | Date;
}

export const ApprovalTimestamps = ({ created, started, ended }: ApprovalTimestampsProps) => {
    const showDuration = !!started && !!ended;
    let durationText: string | null = null;
    if (showDuration) {
        const s = new Date(started).getTime();
        const e = new Date(ended).getTime();
        const seconds = Math.max(0, Math.round((e - s) / 1000));
        durationText = `${seconds} sec`;
    }

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <Text size={200}>
                <b>
                    <FormattedMessage {...SreAgentResources.created} />:
                </b>{' '}
                {formatTimestampShort(created)}
            </Text>
            {started && (
                <Text size={200}>
                    <b>
                        <FormattedMessage {...SreAgentResources.started} />:
                    </b>{' '}
                    {formatTimestampShort(started)}
                </Text>
            )}
            {ended && (
                <Text size={200}>
                    <b>
                        <FormattedMessage {...SreAgentResources.completed} />:
                    </b>{' '}
                    {formatTimestampShort(ended)}
                </Text>
            )}
            {durationText && (
                <Text size={200}>
                    <b>
                        <FormattedMessage {...SreAgentResources.duration} />:
                    </b>{' '}
                    {durationText}
                </Text>
            )}
        </div>
    );
};

export default ApprovalTimestamps;
