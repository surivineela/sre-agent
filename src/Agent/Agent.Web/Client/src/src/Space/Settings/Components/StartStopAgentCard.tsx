import { Button, Card, Text } from '@fluentui/react-components';
import { Play16Regular, RecordStop16Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { useSettingsStyles } from '../Styles/Settings.styles';

export interface StartStopAgentCardProps {
    isAgentStopped: boolean;
    onStart: () => void;
    onStop: () => void;
}

export const StartStopAgentCard = ({ isAgentStopped, onStart, onStop }: StartStopAgentCardProps) => {
    const intl = useIntl();
    const styles = useSettingsStyles();

    return (
        <Card style={styles.basicsCardStyle}>
            <div style={styles.actionSectionStyle}>
                <div style={styles.actionTextContainerStyle}>
                    <div style={styles.sectionTitleStyle}>
                        {intl.formatMessage(isAgentStopped ? SreAgentResources.startAgent : SreAgentResources.stopAgent)}
                    </div>
                    <Text style={styles.sectionDescriptionStyle}>
                        {intl.formatMessage(
                            isAgentStopped ? SreAgentResources.startAgentDescription : SreAgentResources.stopAgentDescription
                        )}
                    </Text>
                </div>
                <Button
                    appearance="outline"
                    icon={isAgentStopped ? <Play16Regular /> : <RecordStop16Regular />}
                    onClick={isAgentStopped ? onStart : onStop}
                >
                    {intl.formatMessage(isAgentStopped ? SreAgentResources.start : SreAgentResources.stop)}
                </Button>
            </div>
        </Card>
    );
};
