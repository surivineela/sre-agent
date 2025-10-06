import { makeStyles, tokens } from '@fluentui/react-components';
import { CheckmarkCircle16Filled, SpinnerIos16Filled, Warning16Filled } from '@fluentui/react-icons';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { InvestigationStatus } from '../../../Common/Contracts/DataPlane/Thread';
import { IncidentManagementResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { getIncidentStatusColor, getIncidentStatusIntlString, mapEmptyStatus } from '../Utilities';

const useStyles = makeStyles({
    setUp: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS },
    greenCheckIcon: { color: tokens.colorPaletteGreenForeground1 },
    warningIcon: { color: tokens.colorStatusWarningForeground2 },
    spinnerIcon: { color: tokens.colorBrandForeground1 },
    bar: { width: '4px', height: '20px', borderRadius: tokens.borderRadiusCircular },
    value: { fontSize: '13px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
});

interface StatusLabelCommonProps {
    value?: number;
}
interface InvestigationStatusLabelProps extends StatusLabelCommonProps {
    type: 'investigationStatus';
    status: InvestigationStatus;
}
interface IncidentStatusLabelProps extends StatusLabelCommonProps {
    type: 'incidentStatus';
    status: IncidentStatus;
}

export type StatusLabelProps = InvestigationStatusLabelProps | IncidentStatusLabelProps;

export const StatusLabel: FC<StatusLabelProps> = ({ type, status }) => {
    const styles = useStyles();
    const intl = useIntl();
    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);

    const investigationStatusValues = useMemo(() => {
        if (type === 'investigationStatus') {
            switch (status) {
                case InvestigationStatus.pendingUserInput:
                    return {
                        icon: (
                            <Warning16Filled
                                className={styles.warningIcon}
                                aria-label={intl.formatMessage(IncidentManagementResources.pendingUserInput)}
                            />
                        ),
                        text: intl.formatMessage(IncidentManagementResources.pendingUserInput),
                    };
                case InvestigationStatus.inProgress:
                    return {
                        icon: (
                            <SpinnerIos16Filled
                                className={styles.spinnerIcon}
                                aria-label={intl.formatMessage(IncidentManagementResources.inProgress)}
                            />
                        ),
                        text: intl.formatMessage(IncidentManagementResources.inProgress),
                    };
                case InvestigationStatus.complete:
                    return {
                        icon: (
                            <CheckmarkCircle16Filled
                                className={styles.greenCheckIcon}
                                aria-label={intl.formatMessage(IncidentManagementResources.completed)}
                            />
                        ),
                        text: intl.formatMessage(IncidentManagementResources.completed),
                    };
            }
        }
        return { icon: undefined, text: undefined };
    }, [type, status, styles.warningIcon, styles.spinnerIcon, styles.greenCheckIcon, intl]);

    const incidentStatusValues = useMemo(() => {
        if (type === 'incidentStatus') {
            const mappedStatus = !status ? mapEmptyStatus(incidentPlatformType) : status;
            const intlString = getIncidentStatusIntlString(mappedStatus);
            const color = getIncidentStatusColor(mappedStatus);

            if (color && intlString) {
                return {
                    icon: <div className={styles.bar} style={{ backgroundColor: color }} />,
                    text: intl.formatMessage(intlString),
                };
            }
        }

        return { icon: undefined, text: undefined };
    }, [type, status, incidentPlatformType, intl, styles.bar]);

    const { icon, text } = type === 'investigationStatus' ? investigationStatusValues : incidentStatusValues;

    return !icon || !text ? (
        '-'
    ) : (
        <div className={styles.setUp}>
            {icon}
            <div className={styles.value}>{text}</div>
        </div>
    );
};
