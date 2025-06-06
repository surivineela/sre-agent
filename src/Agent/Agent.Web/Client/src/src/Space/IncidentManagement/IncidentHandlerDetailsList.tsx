import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { IncidentHandler } from '../Contracts/IncidentManagement';

export type ISortedDetailsListColumn = IColumn & {
    sort?: (items: any[], isSortedDescending: boolean) => any[];
    disableColumnClick?: boolean;
};

enum IncidentHandlerListColumnKey {
    selected = 'selected',
    name = 'name',
    id = 'id',
    severity = 'severity',
    dateModified = 'dateModified',
}

export type IncidentHandlerPickerProps = {
    incidentHandlers: IncidentHandler[];
    incidentHandlersLoading: boolean;
};

const IncidentHandlerDetailsList: FC<IncidentHandlerPickerProps> = (props: IncidentHandlerPickerProps) => {
    const { incidentHandlers, incidentHandlersLoading } = props;
    const intl = useIntl();

    const onRenderName = useCallback((_item: IncidentHandler) => {
        return <></>;
    }, []);

    const onRenderId = useCallback((_item: IncidentHandler) => {
        return <></>;
    }, []);

    const onRenderSeverity = useCallback((_item: IncidentHandler) => {
        return <></>;
    }, []);

    const onRenderDateModified = useCallback((_item: IncidentHandler) => {
        return <></>;
    }, []);

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        return [
            {
                key: IncidentHandlerListColumnKey.name,
                name: intl.formatMessage(IncidentManagementResources.incidentHandler),
                fieldName: IncidentHandlerListColumnKey.name,
                isResizable: true,
                minWidth: 300,
                maxWidth: 700,
                onRender: onRenderName,
            },
            {
                key: IncidentHandlerListColumnKey.id,
                name: intl.formatMessage(IncidentManagementResources.id),
                fieldName: IncidentHandlerListColumnKey.id,
                isResizable: true,
                minWidth: 200,
                maxWidth: 400,
                onRender: onRenderId,
            },
            {
                key: IncidentHandlerListColumnKey.severity,
                name: intl.formatMessage(IncidentManagementResources.severity),
                fieldName: IncidentHandlerListColumnKey.severity,
                isResizable: true,
                minWidth: 200,
                maxWidth: 400,
                onRender: onRenderSeverity,
            },
            {
                key: IncidentHandlerListColumnKey.dateModified,
                name: intl.formatMessage(IncidentManagementResources.dateModified),
                fieldName: IncidentHandlerListColumnKey.dateModified,
                isResizable: true,
                minWidth: 200,
                maxWidth: 400,
                onRender: onRenderDateModified,
            },
        ];
    }, [intl, onRenderName, onRenderId, onRenderSeverity, onRenderDateModified]);

    return (
        <div style={{ width: '100%' }}>
            <ShimmeredDetailsList
                columns={columns}
                constrainMode={ConstrainMode.horizontalConstrained}
                items={incidentHandlers}
                layoutMode={DetailsListLayoutMode.justified}
                compact={true}
                enableShimmer={incidentHandlersLoading}
                checkboxVisibility={CheckboxVisibility.hidden}
                useReducedRowRenderer={true}
                styles={{
                    root: {
                        width: '100%',
                    },
                }}
            />
        </div>
    );
};

export default IncidentHandlerDetailsList;
