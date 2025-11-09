import { mergeClasses, Text, ToolbarButton } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { ToolsPicker } from '../Common/ToolsPicker/ToolsPicker';
import { useAgentCreateDialogStyles } from './AgentCreateDialog.Styles';
import { ToolsPanelProps } from './Contracts';

export const ToolsPanel: FC<ToolsPanelProps> = ({ close, ...rest }) => {
    const intl = useIntl();
    const styles = useAgentCreateDialogStyles();

    return (
        <div className={mergeClasses(styles.dialogContentWrapper, styles.toolsContentWrapper)}>
            <div className={styles.toolsPickerTitleWrapper}>
                <Text size={400} weight="semibold">
                    {intl.formatMessage(ExtendedAgentsGraphResources.chooseTools)}
                </Text>
                <ToolbarButton appearance="transparent" icon={<Dismiss24Regular />} onClick={close}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.closePanel)}
                </ToolbarButton>
            </div>
            <ToolsPicker {...rest} />
        </div>
    );
};
