import { initializeIcons, MessageBar, MessageBarType } from '@fluentui/react';
import { Spinner } from '@fluentui/react-components';
import { FC, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import Url from '../../Common/Helpers/Url';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { IncidentManagementPlatform } from '../Contracts/IncidentManagement';
import { getIncidentManagementPlatform } from '../Settings/Hooks/useIncidentManagementSettings';
import IncidentManagementSettings from '../Settings/IncidentManagementSettings';
import { HandlerCreateOrEditInfo, OperationStatus } from './CreateIncidentHandler/Contracts';
import CreateIncidentHandler from './CreateIncidentHandler/CreateIncidentHandler';
import CreateIncidentHandlerConsolidated from './CreateIncidentHandler/CreateIncidentHandlerConsolidated';
import IncidentManagementHome from './IncidentManagementHome';

const IncidentManagement: FC = () => {
    const intl = useIntl();
    const { agentObj, agentLoading, agentLoadFailure } = useContext(SreAgentContext);
    const [incidentManagementPlatform, setIncidentManagementPlatform] = useState<IncidentManagementPlatform>();

    const [iconsInitialized, setIconsInitialized] = useState(false);
    const [showSettings, setShowSettings] = useState(false);
    const [handlerCreateOrEditInfo, setHandlerCreateOrEditInfo] = useState<HandlerCreateOrEditInfo>();
    const [handlerOperationStatus, setHandlerOperationStatus] = useState<OperationStatus | undefined>(undefined);

    const consolidatedCreateFlagValue = useMemo(() => Url.getFeatureValue('consolidatedcreate') === 'true', []);
    const consolidatedCreateConfigSettingValue = useConfigSetting(SettingNames.ConsolidatedCreate);

    const useConsolidatedCreate = useMemo(
        () => consolidatedCreateFlagValue || consolidatedCreateConfigSettingValue,
        [consolidatedCreateFlagValue, consolidatedCreateConfigSettingValue]
    );

    const keepSettingsOpen = useMemo(() => {
        return (
            incidentManagementPlatform === IncidentManagementPlatform.Disconnected ||
            incidentManagementPlatform === IncidentManagementPlatform.AzMonitor
        );
    }, [incidentManagementPlatform]);

    useEffect(() => {
        if (keepSettingsOpen) {
            setShowSettings(true);
        }
    }, [keepSettingsOpen]);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    useEffect(() => {
        setIncidentManagementPlatform(agentObj ? getIncidentManagementPlatform(agentObj) : undefined);
    }, [agentObj]);

    if (agentLoading || !iconsInitialized) {
        return (
            <div style={{ height: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Spinner size="huge" />
            </div>
        );
    }

    if (agentLoadFailure) {
        return (
            <MessageBar messageBarType={MessageBarType.error}>
                {intl.formatMessage(IncidentManagementResources.incidentManagementLoadFailure, { errorMessage: agentLoadFailure })}
            </MessageBar>
        );
    }

    if (keepSettingsOpen || showSettings) {
        return <IncidentManagementSettings close={() => setShowSettings(false)} keepOpen={keepSettingsOpen} integrated={true} />;
    }

    if (handlerCreateOrEditInfo) {
        return useConsolidatedCreate ? (
            <CreateIncidentHandlerConsolidated
                exitToHome={() => setHandlerCreateOrEditInfo(undefined)}
                setHandlerOperationStatus={setHandlerOperationStatus}
                handlerCreateOrEditInfo={handlerCreateOrEditInfo}
            />
        ) : (
            <CreateIncidentHandler
                exitToHome={() => setHandlerCreateOrEditInfo(undefined)}
                setHandlerOperationStatus={setHandlerOperationStatus}
                handlerCreateOrEditInfo={handlerCreateOrEditInfo}
            />
        );
    }

    return (
        <IncidentManagementHome
            handlerOperationStatus={handlerOperationStatus}
            openHandlerCreate={setHandlerCreateOrEditInfo}
            openSettings={() => setShowSettings(true)}
            useConsolidatedCreate={useConsolidatedCreate}
        />
    );
};

export default IncidentManagement;
