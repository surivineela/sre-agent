import { DateRangeType } from '@fluentui/react-calendar-compat';
import { Button, Field, tokens } from '@fluentui/react-components';
import { DatePicker } from '@fluentui/react-datepicker-compat';
import { CSSProperties, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { formatShortDate } from '../Helpers/Date';

const rangeStyle: CSSProperties = {
    minWidth: 50,
    maxWidth: 50,
    paddingLeft: 0,
    paddingRight: 0,
    fontWeight: tokens.fontWeightRegular,
};

const selectedRangeStyle: CSSProperties = {
    backgroundColor: tokens.colorSubtleBackgroundPressed,
};

/** WIP - DO NOT USE YET */
export const DateRange = () => {
    const intl = useIntl();

    const [dateRangeType, setDateRangeType] = useState<DateRangeType>(DateRangeType.Week);
    const [rangeStartDate, _setRangeStartDate] = useState<Date | undefined>();
    const [rangeEndDate, _setRangeEndDate] = useState<Date | undefined>();

    return (
        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
            <Field label={intl.formatMessage(SreAgentResources.dateRange)} orientation="horizontal">
                <DatePicker
                    calendar={{
                        dateRangeType,
                        onSelectDate: (_date, selectedDateRangeArray) => {
                            console.log('Date range: ', selectedDateRangeArray);
                        },
                    }}
                    formatDate={() => {
                        return `${formatShortDate(rangeStartDate)} - ${formatShortDate(rangeEndDate)}`;
                    }}
                    style={{ maxWidth: 300 }}
                />
            </Field>

            <div style={{ display: 'flex', gap: 6 }}>
                <Button
                    appearance="transparent"
                    onClick={() => {
                        setDateRangeType(DateRangeType.Day);
                        // if (selectedDate) onSelectDate(selectedDate);
                    }}
                    style={{ ...rangeStyle, ...(dateRangeType === DateRangeType.Day ? selectedRangeStyle : undefined) }}
                >
                    {intl.formatMessage(SreAgentResources.dateRange1Day)}
                </Button>
                <Button
                    appearance="transparent"
                    onClick={() => {
                        setDateRangeType(DateRangeType.Week);
                        // if (selectedDate) onSelectDate(selectedDate);
                    }}
                    style={{ ...rangeStyle, ...(dateRangeType === DateRangeType.Week ? selectedRangeStyle : undefined) }}
                >
                    {intl.formatMessage(SreAgentResources.dateRange1Week)}
                </Button>
                <Button
                    appearance="transparent"
                    onClick={() => {
                        setDateRangeType(DateRangeType.Month);
                        // if (selectedDate) onSelectDate(selectedDate);
                    }}
                    style={{ ...rangeStyle, ...(dateRangeType === DateRangeType.Month ? selectedRangeStyle : undefined) }}
                >
                    {intl.formatMessage(SreAgentResources.dateRange1Month)}
                </Button>
            </div>
        </div>
    );
};
