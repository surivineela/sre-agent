import { Menu, MenuItem, MenuList, MenuPopover, MenuTrigger, SplitButton, Text } from '@fluentui/react-components';
import { Document16Regular, Globe16Regular, WebAssetRegular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import { KnowledgeSettingsResources } from '../../../Strings/SREAgentResources';
import { useKnowledgeSettingsEmptyStateStyles } from '../Styles/KnowledgeSettings.styles';

interface KnowledgeSettingsEmptyStateProps {
    onAddFile: () => void;
    onAddWebPage: () => void;
    onAddRepository: () => void;
}

export const KnowledgeSettingsEmptyState: FC<KnowledgeSettingsEmptyStateProps> = ({ onAddFile, onAddWebPage, onAddRepository }) => {
    const intl = useIntl();
    const styles = useKnowledgeSettingsEmptyStateStyles();

    return (
        <div className={styles.container}>
            <img
                src={resolveResourceIcon('KnowledgeBase')}
                className={styles.illustration}
                alt={intl.formatMessage(KnowledgeSettingsResources.knowledgeBaseTitle)}
            />
            <div className={styles.textContainer}>
                <Text className={styles.title}>{intl.formatMessage(KnowledgeSettingsResources.groundResponsesTitle)}</Text>
                <Text className={styles.description}>{intl.formatMessage(KnowledgeSettingsResources.groundResponsesDescription)}</Text>
            </div>
            <Menu>
                <MenuTrigger disableButtonEnhancement>
                    {triggerProps => (
                        <SplitButton appearance="primary" primaryActionButton={{ onClick: onAddFile }} menuButton={triggerProps}>
                            {intl.formatMessage(KnowledgeSettingsResources.addKnowledgeSource)}
                        </SplitButton>
                    )}
                </MenuTrigger>
                <MenuPopover>
                    <MenuList>
                        <MenuItem icon={<Document16Regular />} onClick={onAddFile}>
                            {intl.formatMessage(KnowledgeSettingsResources.addFile)}
                        </MenuItem>
                        <MenuItem icon={<Globe16Regular />} onClick={onAddWebPage} disabled>
                            {intl.formatMessage(KnowledgeSettingsResources.addWebPage)}
                        </MenuItem>
                        <MenuItem icon={<WebAssetRegular />} onClick={onAddRepository} disabled>
                            {intl.formatMessage(KnowledgeSettingsResources.addRepository)}
                        </MenuItem>
                    </MenuList>
                </MenuPopover>
            </Menu>
        </div>
    );
};
