import { makeStyles, Spinner, Text } from '@fluentui/react-components';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { ActivitiesResources } from '../../Strings/SREAgentResources';

const useKnowledgeGraphBuildStatusStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'center',
        gap: '5px',
        maxWidth: '1000px',
        margin: 'auto',
        paddng: '0px 10px',
    },
});

const KnowledgeGraphBuildStatus = () => {
    const { root } = useKnowledgeGraphBuildStatusStyles();
    const { isKnowledgeGraphBuildCompleted, hasChatPermissions, progressPercent } = useContext(KnowledgeGraphBuildStatusContext);
    const intl = useIntl();

    return isKnowledgeGraphBuildCompleted && hasChatPermissions ? null : (
        <div className={root}>
            {hasChatPermissions && <Spinner size={'extra-tiny'} />}
            <Text block={true}>
                {hasChatPermissions && intl.formatMessage(ActivitiesResources.knowledgeGraphBuildStatus, { percent: progressPercent })}
            </Text>
        </div>
    );
};

export default memo(KnowledgeGraphBuildStatus);
